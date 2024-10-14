using ImGuiNET;
using NLog;
using SoapySpectrum.Extentions;
using System.Diagnostics;
using System.IO.Pipes;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using static SoapySpectrum.UI.UI;
namespace SoapySpectrum
{
    internal class SoapyPower
    {
        public static Process soapyPowerPROC;
        public static bool keepStream = false,flashing = false;
        public static Thread soapyProcThread;
        public static double RBW;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        #region Cntrl C
        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        // Delegate type to be used as the Handler Routine for SCCH
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);
        #endregion
        private static void sendCntrlC()
        {

            //https://stackoverflow.com/questions/283128/how-do-i-send-ctrlc-to-a-process-in-c
            if (AttachConsole((uint)soapyPowerPROC.Id))
            {
                SetConsoleCtrlHandler(null, true);
                try
                {
                    if (!Imports.GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                        soapyPowerPROC.WaitForExit();
                }
                finally
                {
                    SetConsoleCtrlHandler(null, false);
                    FreeConsole();
                }

            }
        }
        private static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }
        private static void refreshAfterPIPE()
        {
            new Thread(() =>
            {
                Thread.Sleep(1000);
                UI.UI.clearPlotData();
                
            }).Start();
        }
        private static void sendPipeCommand(string cmd)
        {
            if (PipeCommunication == null) return;
            Logger.Info($"Sending to python binding over pipe --> {cmd}");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write($"\"{cmd}\"");
                PipeCommunication.Write(stream.ToArray(), 0, stream.ToArray().Length);
                byte[] bytes_to_read = new byte[1];
                string message = "";
                do
                {
                    PipeCommunication.Read(bytes_to_read, 0, 1);
                    message += Encoding.UTF8.GetString(bytes_to_read);
                } while (!PipeCommunication.IsMessageComplete);

                if (message == "ack")
                {
                    Logger.Debug("Received ack from python binding, refreshing plot");
                    refreshAfterPIPE();
                }
            }
        }
        public static void changeFrequency() => sendPipeCommand($"change_frequency@{Configuration.config["freqStart"]}@{Configuration.config["freqStop"]}");
        public static void changeGain(string gainName, float value) => sendPipeCommand($"change_gain@{gainName}@{value}");
        public static void changeAverage(int value) => sendPipeCommand($"change_average@{value}");


        static NamedPipeServerStream PipeCommunication;
        public static void beginStream(int FFTSize = 512, string driver = "uhd", string additionalArgs = "-g 0")
        {
            Logger.Debug("Starting Soapy");
            Logger.Debug($"Restarting PIPE");
            if(PipeCommunication is null)
            {
                
                PipeCommunication = new NamedPipeServerStream("SoapySpectrum", PipeDirection.InOut, 1, PipeTransmissionMode.Message);
                Logger.Debug($"Created PIPE to communicate with Soapy Python Binding");
            }
            Logger.Debug($"Restarting Soapy on {Configuration.config["freqStart"]}:{Configuration.config["freqStop"]}");
            soapyProcThread = new Thread(() =>
            {
                soapyPowerPROC = new Process();
                soapyPowerPROC.StartInfo.FileName = "python.exe";
                soapyPowerPROC.StartInfo.Arguments = $"{Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "soapypower\\__main__.py")} -r 52e6 -b {FFTSize} -f {Configuration.config["freqStart"]}:{Configuration.config["freqStop"]} --pow2 -d driver=uhd,master_clock_rate=52e6 -c {additionalArgs} -C 0 -q --reset-stream --crop 30 -n 400 -D constant --fft-window hamming";
                Logger.Debug($"executing --> {soapyPowerPROC.StartInfo.Arguments}");
                soapyPowerPROC.StartInfo.UseShellExecute = false;
                soapyPowerPROC.StartInfo.RedirectStandardOutput = true;
                soapyPowerPROC.StartInfo.RedirectStandardError = true;
                //* Set your output and error (asynchronous) handlers
                soapyPowerPROC.OutputDataReceived += new DataReceivedEventHandler(processSoapyData);
                soapyPowerPROC.ErrorDataReceived += new DataReceivedEventHandler(processSoapyError);
                //* Start process and handlers
                soapyPowerPROC.Start();
                soapyPowerPROC.BeginOutputReadLine();
                soapyPowerPROC.BeginErrorReadLine();
                keepStream = true;
                PipeCommunication.WaitForConnection();//waiting for soapySpectrum to connect to pipe server
                Logger.Debug("Soapy Python Binding Connected to PIPE");
                while (keepStream)
                {
                    Thread.Sleep(1000);
                }
                if (!PipeCommunication.IsConnected)
                    PipeCommunication.Dispose();
                
                sendCntrlC();
                KillProcessAndChildren(soapyPowerPROC.Id);
                soapyPowerPROC.WaitForExit();
                UI.UI.clearPlotData();
            })
            { Priority = ThreadPriority.Highest };
            soapyProcThread.Start();
        }
        private static void processSoapyData(object sendingProcess, DataReceivedEventArgs outLine)
        {
            try
            {
#if DEBUG
                Logger.Debug(outLine.Data);
#endif
                if (outLine == null || outLine.Data == null)
                {

                    keepStream = false;
                    return;
                }
                var data = outLine.Data.Replace("-inf","-114").Split(new[] { ',', }, StringSplitOptions.RemoveEmptyEntries);

                //required for freqRange initialization
                double startFreq = Convert.ToDouble(data[2]),
                       stopFreq = Convert.ToDouble(data[3]);
                RBW = Convert.ToDouble(data[4]);
                var dB = Array.ConvertAll(data.Skip(6).ToArray(), Convert.ToDouble);
                double delimiter = (stopFreq - startFreq) / dB.Length;
                //get all dB data of the sample


                Dictionary<float, float> results = new Dictionary<float, float>();
                for (int sampleIdx = 0; sampleIdx < dB.Length; sampleIdx++)
                {
                    float Frequency = (float)(startFreq + delimiter * sampleIdx);
                    results.Add(Frequency,  getDBCalOffset(Frequency, (float)dB[sampleIdx]));
                }
                UI.UI.updateData(results);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Loading firmware image")) {
                    Logger.Info("Detected new device, begining USRP VGA IMAGE FLASH");
                    flashing = true;
                }
                #if DEBUG
               Logger.Error($"SOAPY-FD {ex.Message} --> {outLine.Data}");
                #endif
            }

        }
       static float mindB = 0;
        static float maxdB = 0;
        public static void initializeMinDB()
        {
            mindB = Configuration.calibrationData.MinBy(x => ((long)x.Value)).Value;
            maxdB = Configuration.calibrationData.MaxBy(x => ((long)x.Value)).Value;
        }
        public static float getDBCalOffset(float freq, float db)
        {
            float results = 0;

            if (Configuration.hasCalibration)
            {
                results = Configuration.calibrationData.MinBy(x => Math.Abs((long)x.Key - freq)).Value;
                if (db + mindB < maxdB)
                {
                    
                    results = db + maxdB;
                }
                else
                {
                    results = db + results;
                }
                return results;
            }

            return db;
        }
        public static void processSoapyError(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (outLine.Data is not null)
            {
                if (outLine.Data.Contains("Loading FPGA image")) flashing = true;
                if (outLine.Data.Contains("done")) flashing = false;
            }
#if DEBUG
            Logger.Error($"SOAPY-ERROR --> {outLine.Data}");
#endif
        }
        public static bool isSoapyPowerRunning()
        {
            return soapyProcThread.IsAlive;
        }
        public static void stopStream()
        {
            Logger.Debug("Stopping stream");
            if (keepStream == false) return;
            keepStream = false;
            while (soapyProcThread.IsAlive || !soapyPowerPROC.HasExited) { }
        }
    }
}
