using ImGuiNET;
using Newtonsoft.Json;
using Pothosware.SoapySDR;
using SoapySpectrum.Extentions;
namespace SoapySpectrum.UI
{
    public static class tab_Cal
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static float inputDB = -40;
        public static Dictionary<string, Tuple<bool, float, int>> cal_gain = new Dictionary<string, Tuple<bool, float, int>>();
        public static double freqStart = 900, freqStop = 1000, freqStep = 10;

        public static string calibrationInfo = "";
        public static bool calibrating = false;
        public static string[] calibrations;
        public static int selectedCalibration = -1;
        public static List<CalibrationPoint> current_cal = new List<CalibrationPoint>();
        public struct CalibrationPoint
        {

            public double frequency;
            //element, <elementvalue,change>
            public Dictionary<string, List<Tuple<float, float>>> elementsData;
            public float results;
        }

        public static void loadCalibration(string name)
        {
            current_cal = JsonConvert.DeserializeObject<List<CalibrationPoint>>(File.ReadAllText(Path.Combine(Configuration.calibrationPath, $"{name}.cal")));
        }

        public static void renderCalibration()
        {
            Theme.Text($"Select Calibration");
            if ((Theme.glowingCombo("sample_rate_Tab", ref selectedCalibration, calibrations, Theme.getTextTheme())))
                loadCalibration(calibrations[selectedCalibration]);


            if (calibrating)
                goto calibrateData;
            Theme.Text($"Create Calibration");
            ImGui.InputFloat("input DB", ref inputDB);
            ImGui.Text($"Gain Elements");

            for (int i = 0; i < tab_Device.availableChannels[0].gains.Count(); i++)
            {

                var gain = tab_Device.availableChannels[0].gains[i];
                if (!cal_gain.Keys.Any(x => x == gain.Item1))
                {
                    cal_gain[gain.Item1] = new Tuple<bool, float, int>(false, 5, 0);
                }
                var range = gain.Item2;
                ImGui.Text($"{gain.Item1}");
                var value = cal_gain[gain.Item1].Item1;
                if (ImGui.Checkbox($"Calibrate {gain.Item1}", ref value))
                    cal_gain[gain.Item1] = new Tuple<bool, float, int>(value, cal_gain[gain.Item1].Item2, cal_gain[gain.Item1].Item3);
                if (value)
                {
                    var step = cal_gain[gain.Item1].Item2;

                    if (ImGui.SliderFloat($"Step accuracy for {gain.Item1} (%)", ref step, 1, 100))
                    {
                        //var accuracy = range.Minimum + (range.Maximum - range.Minimum) * (step / 100);
                        cal_gain[gain.Item1] = new Tuple<bool, float, int>(value, step, cal_gain[gain.Item1].Item3);
                    }
                    var sleep = cal_gain[gain.Item1].Item3;
                    if (ImGui.SliderInt($"Digital Sleep", ref sleep, 1, 2000))
                    {
                        cal_gain[gain.Item1] = new Tuple<bool, float, int>(value, step, sleep);
                    }
                }
                Theme.newLine();
            }
            ImGui.InputDouble("Start Mhz", ref freqStart);
            ImGui.InputDouble("Step Size Mhz", ref freqStep);
            ImGui.InputDouble("Stop Mhz", ref freqStop);
            var references = (freqStop - freqStart) / freqStep;
            var gainReferences = 0.0f;
            foreach (var keyvalue in cal_gain)
                gainReferences += 100 / keyvalue.Value.Item2;

            ImGui.Text($"Total Reference Points\nManual {references}\nAutomatic(including gain elements) {gainReferences * references}");
            //ImGui.Text($"Calibration Status:\n FFT Window {FFTWindow[selectedFFTWINDOW]}\nstill calibrating: {calibrating}");
            if (ImGui.Button("Begin calibration"))
            {
                beginCalibration();
            }
            return;
        calibrateData:
            ImGui.Text("Press Enter to go to next step:");
            ImGui.Text($"Calibrating: {calibrationInfo}");
        }



        public static void beginCalibration()
        {
            //apply a stable fft
            Configuration.config[saVar.fftSize] = 4096;
            Configuration.config[saVar.fftSegment] = 20;
            Configuration.config[saVar.fftOverlap] = 0.5;

            List<CalibrationPoint> calibrationResults = new List<CalibrationPoint>();
            tab_Trace.traces[0].viewStatus = traceViewStatus.active;
            tab_Trace.traces[0].dataStatus = traceDataStatus.maxHold;
            PerformFFT.resetIQFilter();
            calibrating = true;
            for (int i = 0; i < tab_Device.availableChannels[0].gains.Count(); i++)
            {
                //resetting all gain values
                var gain = tab_Device.availableChannels[0].gains[i];
                var range = gain.Item2;
                tab_Device.sdr_device.SetGain(Direction.Rx, 0, gain.Item1, range.Minimum);
            }
            new Thread(() =>
            {
                var currentFreq = freqStart;
                float currentdB = -200.0f;
                float frequencyFound = 0;
                while (true)
                {
                    var freqStart = (currentFreq * 1e6 - (1e6 / 2.0));
                    var freqStop = (currentFreq * 1e6 + (1e6 / 2.0));
                    if (freqStart >= freqStop ||
                    !tab_Device.availableChannels[0].frequencyRange.ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop)
                    || currentFreq > tab_Cal.freqStop)
                    {
                        Logger.Info("$Out Of Boundaries, finishing calibration");
                        break;
                    }
                    Configuration.config[saVar.freqStart] = freqStart;
                    Configuration.config[saVar.freqStop] = freqStop;
                    calibrationInfo = $"Please transmit {inputDB} at {currentFreq} and then press enter\n when you see the signal and the FFT is stable\n" +
                    $"\nFound\nFreq: {frequencyFound}\ndB:{currentdB}";
                    KeyValuePair<float, float> max;
                    lock (tab_Trace.traces[0].plot)
                    {
                        if (tab_Trace.traces[0].plot.Count() < 2) continue; //FFT has not initiated yet
                        max = tab_Trace.findMaxHoldRange(tab_Trace.traces[0].plot, (float)freqStart, (float)freqStop);
                    }
                    frequencyFound = max.Key;
                    currentdB = max.Value;
                    if (Imports.GetAsyncKeyState(Keys.Enter))
                    {
                        CalibrationPoint x = new CalibrationPoint() { frequency = currentFreq, results = inputDB - currentdB };
                        x.elementsData = new Dictionary<string, List<Tuple<float, float>>>();

                        //calibrate for gain elements
                        foreach (var keyvalue in cal_gain)
                        {


                            if (!keyvalue.Value.Item1) continue;


                            //calibrate changes by every step
                            var element = tab_Device.availableChannels[0].gains.First(x => x.Item1 == keyvalue.Key);
                            var range = element.Item2;
                            for (double i = 0; i < 100; i += keyvalue.Value.Item2)
                            {
                                var accuracy = (float)(range.Minimum + (range.Maximum - range.Minimum) * (i / 100));
                                if (accuracy > range.Maximum) continue; //out of step boundaries
                                tab_Device.sdr_device.SetGain(Direction.Rx, 0, element.Item1, accuracy);
                                PerformFFT.resetIQFilter();

                                //wait for PLL/LO to MAKE SURE IT Actually changes and next FFT Results
                                Thread.Sleep(keyvalue.Value.Item3);
                                while (tab_Trace.traces[0].plot.Count() < 2)
                                    Thread.Sleep(keyvalue.Value.Item3);

                                var elementChange = tab_Trace.findMaxHoldRange(tab_Trace.traces[0].plot, (float)freqStart, (float)freqStop).Value + currentdB;
                                calibrationInfo += $"\n{element.Item1}:{accuracy}|{elementChange}";
                                if (!x.elementsData.Keys.Contains(element.Item1))
                                    x.elementsData.Add(element.Item1, new List<Tuple<float, float>>());

                                x.elementsData[element.Item1].Add(new Tuple<float, float>(accuracy, elementChange));
                            }
                            tab_Device.sdr_device.SetGain(Direction.Rx, 0, range.Minimum);
                            Thread.Sleep(keyvalue.Value.Item3);
                        }

                        //making sure no skipping
                        while (Imports.GetAsyncKeyState(Keys.Enter))
                        {
                            Thread.Sleep(50);
                        }

                        Thread.Sleep(250);

                        //go to next frequency
                        calibrationResults.Add(x);
                        currentFreq += freqStep;
                        PerformFFT.resetIQFilter();
                        currentdB = -200.0f;
                        frequencyFound = 0;
                    }
                    if (Imports.GetAsyncKeyState(Keys.End))
                    {
                        break;
                    }
                }
                saveCalibrationData(calibrationResults);
            }).Start();
        }

        //calibrationData.Add(x.Key, new Tuple<float, float>(x.Value, calculateDBInput(inputDB, x.Value) - Atten));
        public static void saveCalibrationData(List<CalibrationPoint> calibrationResults)
        {
            var data = Newtonsoft.Json.JsonConvert.SerializeObject(calibrationResults);
            data = data.Replace($",", "\n,").Replace("{", "\n{").Replace("}", "\n}");
            Logger.Info("Calibration Results:");
            Logger.Info(data);
            File.WriteAllText(Path.Combine(Configuration.calibrationPath,
                $"{tab_Device.sdr_device.DriverKey}-{freqStart}-{freqStop}-{DateTime.Now.ToString("MM-dd-yyy")}.cal"),
                data);
        }
    }
}
