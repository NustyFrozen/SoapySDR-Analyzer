using ImGuiNET;
using Pothosware.SoapySDR;
namespace SoapySpectrum.UI
{
    public static class tab_Device
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static int deviceID = -1;
        public static string[] available_Devices = new string[] { "No Devices Found" };
        public static string[] d_channels;
        public static int d_selectedChannel = 0;
        public static Device sdr_device;
        public static float leakageSleep = 0;
        public static bool correctIQ = true;
        public static string[] sensorData;

        //sdr data

        public static Dictionary<uint, channelStreamData> availableChannels = new System.Collections.Generic.Dictionary<uint, channelStreamData>();
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

            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to open Device -> {ex.Message}");
            }
        }

        static void fetchSDR_Data()
        {

            uint availableChannels = sdr_device.GetNumChannels(Direction.Rx);
            d_channels = Array.ConvertAll(Enumerable.Range(0, (int)availableChannels).ToArray(), Convert.ToString);
            for (uint channel = 0; channel < availableChannels; channel++)
            {
                channelStreamData chan = new channelStreamData();
                chan.anntenas = sdr_device.ListAntennas(Direction.Rx, channel);
                var listgains = sdr_device.ListGains(Direction.Rx, channel);
                chan.gains = new Tuple<string, Pothosware.SoapySDR.Range>[listgains.Count()];
                chan.gains_values = new float[listgains.Count()];
                int c = 0;
                foreach (var gain in listgains)
                {
                    var range = sdr_device.GetGainRange(Direction.Rx, channel, gain);
                    float value = (float)sdr_device.GetGain(Direction.Rx, channel, gain);
                    chan.gains_values[c] = value;
                    chan.gains[c++] = new Tuple<string, Pothosware.SoapySDR.Range>(gain, range);
                };
                c = 0;

                chan.sample_rates = sdr_device.GetSampleRateRange(Direction.Rx, channel);
                chan.sample_rates.Add(new Pothosware.SoapySDR.Range(0, double.MaxValue, 0));
                chan.customSampleRate = chan.sample_rates.First().Maximum.ToString();
                chan.frequencyRange = sdr_device.GetFrequencyRange(Direction.Rx, channel);
                tab_Device.availableChannels[channel] = chan;
            }

            var sensors = sdr_device.ListSensors();
            sensorData = new string[sensors.Count];
            int i = 0;
            foreach (var sensor in sensors)
            {
                sensorData[i++] = $"{sensor}: {sdr_device.ReadSensor(sensor)}";
            }
        }
        public static void renderDeviceData()
        {
            var inputTheme = Theme.getTextTheme();

            if (sdr_device == null) return;
            Theme.Text($"Channel", inputTheme);
            if (Theme.glowingCombo("channels_Tab", ref d_selectedChannel, d_channels, inputTheme))
                Global.selectedChannel = (uint)Convert.ToInt16(d_channels[(uint)d_selectedChannel]);
            var channel = availableChannels[Global.selectedChannel];
            ImGui.Checkbox($"Active", ref channel.active);
            Theme.Text($"Anntena", inputTheme);
            foreach (var antenna in channel.anntenas)
                if (ImGui.RadioButton($"{antenna}", antenna == channel.selectedAnntena))
                {
                    channel.selectedAnntena = antenna;
                    sdr_device.SetAntenna(Direction.Rx, Global.selectedChannel, channel.selectedAnntena);
                }
            Theme.Text($"Sample Rate", inputTheme);

            List<string> sample_rates_choice = new List<string>();
            List<Pothosware.SoapySDR.Range> sample_rate = new List<Pothosware.SoapySDR.Range>();
            foreach (var samplerate in channel.sample_rates)
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


            if ((Theme.glowingCombo("sample_rate_Tab", ref channel.selectedSampleRate, sample_rates_choice.ToArray(), inputTheme)))
            {
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
                        if (Theme.glowingInput($"rate_range{rateRange.Minimum}_{rateRange.Maximum}", ref channel.customSampleRate, inputTheme))
                        {
                            double customSampleRate = 0;
                            if (double.TryParse(channel.customSampleRate, out customSampleRate))
                            {
                                if (rateRange.Minimum <= customSampleRate && rateRange.Maximum >= customSampleRate)
                                {
                                    channel.sample_rate = Convert.ToDouble(customSampleRate);
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
                        if (double.TryParse(channel.customSampleRate, out customSampleRate))
                        {
                            customSampleRate = Math.Round(customSampleRate / rateRange.Step) * rateRange.Step;
                            if (rateRange.Minimum <= customSampleRate && rateRange.Maximum >= customSampleRate)
                            {
                                channel.sample_rate = Convert.ToDouble(customSampleRate);
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
            for (int i = 0; i < channel.gains.Count(); i++)
            {
                var gain = channel.gains[i];
                var range = gain.Item2;
                ImGui.Text($"{gain.Item1}");
                if (Theme.slider($"{gain.Item1}", (float)range.Minimum, (float)range.Maximum, ref channel.gains_values[i], sliderTheme))
                {
                    if (range.Step != 0)
                        sdr_device.SetGain(Direction.Rx, Global.selectedChannel, gain.Item1, Math.Round(channel.gains_values[i] / range.Step) * range.Step);
                    else
                    { //free value
                        sdr_device.SetGain(Direction.Rx, Global.selectedChannel, gain.Item1, channel.gains_values[i]);
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
            if (ImGui.Button($"Begin sampling"))
            {
                PerformFFT.beginFFT();
            }
            Theme.newLine();
            renderDeviceData();
            Theme.newLine();
            Theme.Text("LO/PLL Leakage sleep", inputTheme);
            if (Theme.slider("Leakage", ref leakageSleep, sliderTheme))
            {
                Configuration.config[saVar.leakageSleep] = (int)(leakageSleep * 100);
                Logger.Debug(Configuration.config[saVar.leakageSleep]);
            }
            if (ImGui.Checkbox("IQ correction", ref correctIQ))
                Configuration.config[saVar.iqCorrection] = correctIQ;
        }
    }
}
