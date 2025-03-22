using ImGuiNET;
using Pothosware.SoapySDR;
namespace SoapySpectrum.UI
{
    public static class tab_Device
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static int deviceID = -1;
        public static string[] available_Devices = new string[] { "No Devices Found" };
        public static Device sdr_device;
        public static uint selectedChannel = 0;
        public static string selectedAntennas = "TX/RX";
        public static int selectedSampleRate;
        public static string customSampleRate = "0";
        public static float leakageSleep = 0;
        public static bool correctIQ = true;

        //sdr data
        public static StringList anntenas;
        public static uint availableChannels;
        public static Tuple<string, Pothosware.SoapySDR.Range>[] gains;
        public static float[] gains_values;
        public static string[] sensorData;
        public static string[] gainModes;
        public static Dictionary<int, RangeList> frequencyRange = new Dictionary<int, RangeList>();
        public static Dictionary<int, RangeList> sample_rates = new Dictionary<int, RangeList>();
        public static void setupSoapyEnvironment()
        {
            var currentPath = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
            var soapyPath = Path.Combine(currentPath, @"SoapySDR");
            var libsPath = Path.Combine(soapyPath, @"Libs");
            Environment.SetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH", Path.Combine(currentPath, @"SoapySDR\root\SoapySDR\lib\SoapySDR\modules0.8-3\"));
            Environment.SetEnvironmentVariable("SOAPY_SDR_ROOT", Path.Combine(currentPath, @"SoapySDR\root\SoapySDR"));
            Environment.SetEnvironmentVariable("PATH", $"{Environment.GetEnvironmentVariable("PATH")};{soapyPath};{libsPath}");

        }
        static string InsertNewlines(string text, int X)
        {
            if (text.Length <= X) return text; // No need to wrap short items

            List<char> formattedText = new List<char>();
            for (int i = 0; i < text.Length; i++)
            {
                formattedText.Add(text[i]);
                if ((i + 1) % X == 0)
                {
                    formattedText.Add('\n');
                }
            }
            return new string(formattedText.ToArray());
        }
        public static void refreshDevices()
        {
            var devices = Device.Enumerate().ToList();
            List<string> devices_label = new List<string>();
            foreach (var device in devices)
            {
                string idenefiers = string.Empty;
                if (device.ContainsKey("label"))
                    idenefiers += $"label={device["label"]},";

                if (device.ContainsKey("driver"))
                    idenefiers += $"driver={device["driver"]},";

                if (device.ContainsKey("serial"))
                    idenefiers += $"serial={device["serial"]},";

                if (device.ContainsKey("hardware"))
                    idenefiers += $"hardware={device["hardware"]}";



                devices_label.Add(InsertNewlines(idenefiers, 60));
            }
            if (devices_label.Count > 0)
            {
                available_Devices = devices_label.ToArray();
                deviceID = 0;
                updateDevice();
            }
            else available_Devices = new string[] { "No Devices Found" };

        }
        public static void updateDevice()
        {
            PerformFFT.isRunning = false;
            try
            {
                sdr_device = new Device(available_Devices[deviceID]);
                fetchSDR_Data();
                PerformFFT.beginFFT();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to open Device -> {ex.Message}");
            }
        }

        static void fetchSDR_Data()
        {
            anntenas = sdr_device.ListAntennas(Direction.Rx, selectedChannel);
            availableChannels = sdr_device.GetNumChannels(Direction.Rx);
            var listgains = sdr_device.ListGains(Direction.Rx, selectedChannel);
            gains = new Tuple<string, Pothosware.SoapySDR.Range>[listgains.Count()];
            gains_values = new float[listgains.Count()];
            int i = 0;
            foreach (var gain in listgains)
            {
                var range = sdr_device.GetGainRange(Direction.Rx, selectedChannel, gain);
                float value = (float)sdr_device.GetGain(Direction.Rx, selectedChannel, gain);
                gains_values[i] = value;
                gains[i++] = new Tuple<string, Pothosware.SoapySDR.Range>(gain, range);
            };
            var sensors = sdr_device.ListSensors();
            sensorData = new string[sensors.Count];
            i = 0;
            foreach (var sensor in sensors)
            {
                sensorData[i++] = $"{sensor}: {sdr_device.ReadSensor(sensor)}";
            }
            i = 0;
            sample_rates.Clear();
            for (; i < availableChannels; i++)
            {
                sample_rates[i] = sdr_device.GetSampleRateRange(Direction.Rx, (uint)i);
                sample_rates[i].Add(new Pothosware.SoapySDR.Range(0, double.MaxValue, 0));
                customSampleRate = sample_rates[i].First().Maximum.ToString();
            }
            i = 0;
            frequencyRange.Clear();
            for (; i < availableChannels; i++)
            {

                frequencyRange.Add(i, sdr_device.GetFrequencyRange(Direction.Rx, (uint)i));


            }
            i = 0;
        }
        public static void renderDeviceData()
        {
            var inputTheme = Theme.getTextTheme();

            if (sdr_device == null) return;

            Theme.Text($"Channel", inputTheme);
            for (uint i = 0; i < availableChannels; i++)
                if (ImGui.RadioButton($"{i}", i == selectedChannel))
                {
                    selectedChannel = i;

                }

            Theme.Text($"Anntena", inputTheme);
            foreach (var antenna in anntenas)
                if (ImGui.RadioButton($"{antenna}", antenna == selectedAntennas))
                {
                    selectedAntennas = antenna;
                    sdr_device.SetAntenna(Direction.Rx, selectedChannel, selectedAntennas);
                }
            Theme.Text($"Sample Rate", inputTheme);

            List<string> sample_rates_choice = new List<string>();
            List<Pothosware.SoapySDR.Range> sample_rate = new List<Pothosware.SoapySDR.Range>();
            foreach (var samplerate in sample_rates[(int)selectedChannel])
            {


                if (samplerate.Minimum == samplerate.Maximum && samplerate.Step == 0)
                {
                    //selection
                    sample_rates_choice.Add(samplerate.Minimum.ToString());
                }
                else
                {
                    //any value between min and max (Input)
                    sample_rate.Add(samplerate);
                }


            }

            if ((Theme.glowingCombo("sample_rate_Tab", ref selectedSampleRate, sample_rates_choice.ToArray(), inputTheme)))
            {
                Configuration.config["sampleRate"] = Convert.ToDouble(sample_rates_choice[selectedSampleRate]);
                PerformFFT.resetIQFilter();
            }
            Theme.Text("Custom Sample Rates", inputTheme);
            if (sample_rate.Count == 0)
            {
                ImGui.Text("None");
            }
            else
            {
                foreach (var rateRange in sample_rate)
                {
                    if (rateRange.Step == 0)
                    {
                        //any value
                        Theme.Text($"Between {rateRange.Minimum} - {rateRange.Maximum}", inputTheme);
                        if (Theme.glowingInput($"rate_range{rateRange.Minimum}_{rateRange.Maximum}", ref customSampleRate, inputTheme))
                        {
                            double customSampleRate = 0;
                            if (double.TryParse(tab_Device.customSampleRate, out customSampleRate))
                            {
                                if (rateRange.Minimum <= customSampleRate && rateRange.Maximum >= customSampleRate)
                                {
                                    Configuration.config["sampleRate"] = Convert.ToDouble(customSampleRate);
                                    PerformFFT.resetIQFilter();
                                }
                                else
                                {
                                    Logger.Error($"Value is not in the range");
                                }

                            }
                            else
                            {
                                Logger.Error($"Value is not A valid double");
                            }
                        }
                    }
                    else
                    {
                        //any value with relation to step
                        double customSampleRate = 0;
                        if (double.TryParse(tab_Device.customSampleRate, out customSampleRate))
                        {
                            customSampleRate = Math.Round(customSampleRate / rateRange.Step) * rateRange.Step;
                            if (rateRange.Minimum <= customSampleRate && rateRange.Maximum >= customSampleRate)
                            {
                                Configuration.config["sampleRate"] = Convert.ToDouble(customSampleRate);
                                PerformFFT.resetIQFilter();
                            }
                            else
                            {
                                Logger.Error($"Value is not in the range");
                            }

                        }
                        else
                        {
                            Logger.Error($"Value is not A valid double");
                        }
                    }
                }
            }
            Theme.Text($"Amplifiers", inputTheme);
            for (int i = 0; i < gains.Count(); i++)
            {
                var gain = gains[i];
                var range = gain.Item2;
                ImGui.Text($"{gain.Item1}");
                if (Theme.slider($"{gain.Item1}", (float)range.Minimum, (float)range.Maximum, ref gains_values[i], sliderTheme))
                {
                    if (range.Step != 0)
                        sdr_device.SetGain(Direction.Rx, selectedChannel, gain.Item1, Math.Round(gains_values[i] / range.Step) * range.Step);
                    else
                    { //free value
                        sdr_device.SetGain(Direction.Rx, selectedChannel, gain.Item1, gains_values[i]);
                    }

                }
            }
            Theme.Text($"Sensors Data", inputTheme);
            var buttonTheme = Theme.getButtonTheme();

            foreach (var sensor in sensorData)
                ImGui.Text(sensor);

            buttonTheme.text = $"Refresh Sensors Data";
            if (Theme.button("Refresh_Sensors", buttonTheme))
            {
                int i = 0;
                foreach (var sensor in sdr_device.ListSensors())
                {
                    sensorData[i++] = $"{sensor}: {sdr_device.ReadSensor(sensor)}";
                }
            }

            Theme.newLine();

        }
        static Theme.glowingInputConfigurator inputTheme = Theme.getTextTheme();
        static Theme.ButtonConfigurator buttonTheme = Theme.getButtonTheme();
        static Theme.SliderInputConfigurator sliderTheme = Theme.getSliderTheme();
        public static void renderDevice()
        {

            Theme.newLine();
            Theme.newLine();
            Theme.newLine();
            buttonTheme.text = "Refresh";
            if (Theme.button("Refresh_Devices", buttonTheme))
                refreshDevices();
            Theme.newLine();
            Theme.Text("SDR", inputTheme);
            if (Theme.glowingCombo("devicetabs", ref deviceID, available_Devices, inputTheme))
                updateDevice();
            Theme.newLine();
            renderDeviceData();
            Theme.newLine();
            Theme.Text("LO/PLL Leakage sleep", inputTheme);
            if (Theme.slider("Leakage", ref leakageSleep, sliderTheme))
            {
                Configuration.config["leakageSleep"] = (int)(leakageSleep * 100);
                Logger.Debug(Configuration.config["leakageSleep"]);
            }
            if (ImGui.Checkbox("IQ correction", ref correctIQ))
                Configuration.config["IQCorrection"] = correctIQ;
        }
    }
}
