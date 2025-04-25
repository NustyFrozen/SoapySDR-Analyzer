using ImGuiNET;
using Newtonsoft.Json;
using Pothosware.SoapySDR;
using SoapyRL.Extentions;

namespace SoapyRL.UI
{
    public static class tab_Cal
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public static string[] s_AvailableCal;

        //variables for calibration
        public static float s_inputDB = -40;

        public static Dictionary<string, Tuple<bool, float, int>> s_calGain = new Dictionary<string, Tuple<bool, float, int>>();
        public static double s_freqStart = 900, s_freqStop = 1000, s_freqStep = 10;

        //variables during calibration
        public static string s_calInfo = "";

        public static bool s_isCalibrating = false;
        public static int s_selectedCal = -1;
        public static List<CalibrationPoint> s_currentCalData = new List<CalibrationPoint>();

        public struct CalibrationPoint
        {
            public double frequency;

            //element, <elementvalue,change>
            public Dictionary<string, List<Tuple<float, float>>> elementsData;

            public float results;
        }

        public static void loadCalibration(string name)
        {
            s_currentCalData = JsonConvert.DeserializeObject<List<CalibrationPoint>>(File.ReadAllText(Path.Combine(Configuration.calibrationPath, $"{name}.cal")));
        }

        public static void renderCalibration()
        {
            Theme.Text($"Select Calibration");
            if ((Theme.glowingCombo("sample_rate_Tab", ref s_selectedCal, s_AvailableCal, Theme.getTextTheme())))
                loadCalibration(s_AvailableCal[s_selectedCal]);

            if (s_isCalibrating)
                goto calibrateData;
            Theme.Text($"Create Calibration");
            ImGui.InputFloat("input DB", ref s_inputDB);
            ImGui.Text($"Gain Elements");

            for (int i = 0; i < tab_Device.s_deviceGains.Count(); i++)
            {
                var currentGain = tab_Device.s_deviceGains[i];
                if (!s_calGain.Keys.Any(x => x == currentGain.Item1))
                {
                    s_calGain[currentGain.Item1] = new Tuple<bool, float, int>(false, 5, 0);
                }
                var gainRange = currentGain.Item2;
                ImGui.Text($"{currentGain.Item1}");
                var isCalGain = s_calGain[currentGain.Item1].Item1;
                if (ImGui.Checkbox($"Calibrate {currentGain.Item1}", ref isCalGain))
                    s_calGain[currentGain.Item1] = new Tuple<bool, float, int>(isCalGain, s_calGain[currentGain.Item1].Item2, s_calGain[currentGain.Item1].Item3);
                if (isCalGain)
                {
                    var step = s_calGain[currentGain.Item1].Item2;

                    if (ImGui.SliderFloat($"Step accuracy for {currentGain.Item1} (%)", ref step, 1, 100))
                    {
                        //var accuracy = range.Minimum + (range.Maximum - range.Minimum) * (step / 100);
                        s_calGain[currentGain.Item1] = new Tuple<bool, float, int>(isCalGain, step, s_calGain[currentGain.Item1].Item3);
                    }
                    var sleep = s_calGain[currentGain.Item1].Item3;
                    if (ImGui.SliderInt($"Digital Sleep", ref sleep, 1, 2000))
                    {
                        s_calGain[currentGain.Item1] = new Tuple<bool, float, int>(isCalGain, step, sleep);
                    }
                }
                Theme.newLine();
            }
            ImGui.InputDouble("Start Mhz", ref s_freqStart);
            ImGui.InputDouble("Step Size Mhz", ref s_freqStep);
            ImGui.InputDouble("Stop Mhz", ref s_freqStop);
            var references = (s_freqStop - s_freqStart) / s_freqStep;
            var gainReferences = 0.0f;
            foreach (var keyvalue in s_calGain)
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
            ImGui.Text($"Calibrating: {s_calInfo}");
        }

        public static void beginCalibration()
        {
            //apply a stable fft
            Configuration.config[Configuration.saVar.fftSize] = 4096;
            Configuration.config[Configuration.saVar.fftSegmentLength] = 20;
            Configuration.config[Configuration.saVar.fftOverlap] = 0.5;

            List<CalibrationPoint> calibrationResults = new List<CalibrationPoint>();
            tab_Trace.s_traces[0].viewStatus = tab_Trace.traceViewStatus.active;
            tab_Trace.s_traces[0].dataStatus = tab_Trace.traceDataStatus.maxHold;
            PerformFFT.resetIQFilter();
            s_isCalibrating = true;
            for (int i = 0; i < tab_Device.s_deviceGains.Count(); i++)
            {
                //resetting all gain values
                var gain = tab_Device.s_deviceGains[i];
                var range = gain.Item2;
                tab_Device.s_sdrDevice.SetGain(Direction.Rx, 0, gain.Item1, range.Minimum);
            }
            new Thread(() =>
            {
                var currentFreq = s_freqStart;
                float currentdB = -200.0f;
                float frequencyFound = 0;
                while (true)
                {
                    var freqStart = (currentFreq * 1e6 - (1e6 / 2.0));
                    var freqStop = (currentFreq * 1e6 + (1e6 / 2.0));
                    if (freqStart >= freqStop ||
                    !tab_Device.s_deviceFrequencyRange[0].ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop)
                    || currentFreq > tab_Cal.s_freqStop)
                    {
                        _logger.Info("$Out Of Boundaries, finishing calibration");
                        break;
                    }
                    Configuration.config[Configuration.saVar.freqStart] = freqStart;
                    Configuration.config[Configuration.saVar.freqStop] = freqStop;
                    s_calInfo = $"Please transmit {s_inputDB} at {currentFreq} and then press enter\n when you see the signal and the FFT is stable\n" +
                    $"\nFound\nFreq: {frequencyFound}\ndB:{currentdB}";
                    KeyValuePair<float, float> max;
                    lock (tab_Trace.s_traces[0].plot)
                    {
                        if (tab_Trace.s_traces[0].plot.Count() < 2) continue; //FFT has not initiated yet
                        max = tab_Trace.findMaxHoldRange(tab_Trace.s_traces[0].plot, (float)freqStart, (float)freqStop);
                    }
                    frequencyFound = max.Key;
                    currentdB = max.Value;
                    if (Imports.GetAsyncKeyState(Keys.Enter))
                    {
                        CalibrationPoint x = new CalibrationPoint() { frequency = currentFreq, results = s_inputDB - currentdB };
                        x.elementsData = new Dictionary<string, List<Tuple<float, float>>>();

                        //calibrate for gain elements
                        foreach (var keyvalue in s_calGain)
                        {
                            if (!keyvalue.Value.Item1) continue;

                            //calibrate changes by every step
                            var element = tab_Device.s_deviceGains.First(x => x.Item1 == keyvalue.Key);
                            var range = element.Item2;
                            for (double i = 0; i < 100; i += keyvalue.Value.Item2)
                            {
                                var accuracy = (float)(range.Minimum + (range.Maximum - range.Minimum) * (i / 100));
                                if (accuracy > range.Maximum) continue; //out of step boundaries
                                tab_Device.s_sdrDevice.SetGain(Direction.Rx, 0, element.Item1, accuracy);
                                PerformFFT.resetIQFilter();

                                //wait for PLL/LO to MAKE SURE IT Actually changes and next FFT Results
                                Thread.Sleep(keyvalue.Value.Item3);
                                while (tab_Trace.s_traces[0].plot.Count() < 2)
                                    Thread.Sleep(keyvalue.Value.Item3);

                                var elementChange = tab_Trace.findMaxHoldRange(tab_Trace.s_traces[0].plot, (float)freqStart, (float)freqStop).Value + currentdB;
                                s_calInfo += $"\n{element.Item1}:{accuracy}|{elementChange}";
                                if (!x.elementsData.Keys.Contains(element.Item1))
                                    x.elementsData.Add(element.Item1, new List<Tuple<float, float>>());

                                x.elementsData[element.Item1].Add(new Tuple<float, float>(accuracy, elementChange));
                            }
                            tab_Device.s_sdrDevice.SetGain(Direction.Rx, 0, range.Minimum);
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
                        currentFreq += s_freqStep;
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
            _logger.Info("Calibration Results:");
            _logger.Info(data);
            File.WriteAllText(Path.Combine(Configuration.calibrationPath,
                $"{tab_Device.s_sdrDevice.DriverKey}-{s_freqStart}-{s_freqStop}-{DateTime.Now.ToString("MM-dd-yyy")}.cal"),
                data);
        }
    }
}