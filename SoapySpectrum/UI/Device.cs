﻿using ClickableTransparentOverlay;
using ImGuiNET;
using Pothosware.SoapySDR;
namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        int deviceID = -1;
        string[] available_Devices = new string[] { "No Devices Found" };
        public static Device sdr_device;
        uint selectedChannel = 0;
        string selectedAntennas = "TX/RX";
        int selectedSampleRate;
        string customSampleRate = "0";
        public static void setupSoapyEnvironment()
        {
            Environment.SetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH", @"C:\Program Files (x86)\SoapySDR\lib\SoapySDR\modules0.8-3");
            if (Environment.GetEnvironmentVariable("SOAPY_SDR_ROOT") == null)
            {
                if (Directory.Exists($"C:\\Program Files (x86)\\SoapySDR"))
                {
                    Environment.SetEnvironmentVariable("SOAPY_SDR_ROOT", @"C:\Program Files (x86)\SoapySDR");
                }
                else
                {
                    MessageBox.Show("SOAPY_SDR_ROOT environment not found, please add it to the environment variables");
                    Application.Exit();
                }
            }
        }
        public void refreshDevices()
        {
            var devices = Device.Enumerate().ToList();
            List<string> devices_label = new List<string>();
            foreach (var device in devices)
            {
                string idenefiers = string.Empty;
                foreach (var keyvalue in device.ToList())
                    idenefiers += $"{keyvalue.Key}={keyvalue.Value},";
                devices_label.Add(idenefiers);
            }
            if (devices_label.Count > 0)
                available_Devices = devices_label.ToArray();
            else available_Devices = new string[] { "No Devices Found" };
        }
        public void updateDevice()
        {
            sdr_device = new Device(available_Devices[deviceID]);
            fetchSDR_Data();
        }
        StringList anntenas;
        uint availableChannels;
        Tuple<string, Pothosware.SoapySDR.Range>[] gains;
        float[] gains_values;
        string[] sensorData;
        Dictionary<int, RangeList> frequencyRange = new Dictionary<int, RangeList>();
        Dictionary<int, RangeList> sample_rates = new Dictionary<int, RangeList>();
        void fetchSDR_Data()
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
                customSampleRate = sample_rates[i].First().Maximum.ToString();
            }
            i = 0;
            frequencyRange.Clear();
            for (; i < availableChannels; i++)
            {

                frequencyRange.Add(i, sdr_device.GetFrequencyRange(Direction.Rx, (uint)i));


            }
        }
        void renderDeviceData()
        {
            var inputTheme = ImGuiTheme.getTextTheme();

            if (sdr_device == null) return;

            ImGui.Text($"Channel:");
            for (uint i = 0; i < availableChannels; i++)
                if (ImGui.RadioButton($"{i}", i == selectedChannel))
                {
                    selectedChannel = i;

                }

            ImGui.Text($"Anntena:");
            foreach (var antenna in anntenas)
                if (ImGui.RadioButton($"{antenna}", antenna == selectedAntennas))
                    selectedAntennas = antenna;
            ImGui.Text($"Sample Rate:");

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
            if ((ImGuiTheme.glowingCombo("sample_rate_Tab", ref selectedSampleRate, sample_rates_choice.ToArray(), inputTheme)))
            {
                Configuration.config["sampleRate"] = Convert.ToDouble(sample_rates_choice[selectedSampleRate]);
                PerformFFT.resetIQFilter();
            }
            ImGui.Text("Custom Sample Rates:");
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
                        ImGui.Text($"Between {rateRange.Minimum} - {rateRange.Maximum}");
                        if (ImGuiTheme.glowingInput($"rate_range{rateRange.Minimum}_{rateRange.Maximum}", ref customSampleRate, inputTheme))
                        {
                            double customSampleRate = 0;
                            if (double.TryParse(this.customSampleRate, out customSampleRate))
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
                        if (double.TryParse(this.customSampleRate, out customSampleRate))
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
            ImGui.Text($"Amplifiers:");
            for (int i = 0; i < gains.Count(); i++)
            {
                var gain = gains[i];
                var range = gain.Item2;
                if (ImGui.SliderFloat($"{gain.Item1}", ref gains_values[i], (float)range.Minimum, (float)range.Maximum, "%.3f", ImGuiSliderFlags.AlwaysClamp))
                {
                    sdr_device.SetGain(Direction.Rx, selectedChannel, gain.Item1, Math.Round(gains_values[i] / range.Step) * range.Step);
                    PerformFFT.resetIQFilter();
                }
            }
            ImGui.Text($"Sensors Data:");
            var buttonTheme = ImGuiTheme.getButtonTheme();

            foreach (var sensor in sensorData)
                ImGui.Text(sensor);

            buttonTheme.text = $"Refresh Sensors Data";
            if (ImGuiTheme.button("Refresh_Sensors", buttonTheme))
            {
                int i = 0;
                foreach (var sensor in sdr_device.ListSensors())
                {
                    sensorData[i++] = $"{sensor}: {sdr_device.ReadSensor(sensor)}";
                }
            }

            ImGuiTheme.newLine();
            if (ImGui.Button("LETS GO"))
            {
                PerformFFT.beginFFT();
            }

        }
        public void renderDevice()
        {
            var inputTheme = ImGuiTheme.getTextTheme();
            var buttonTheme = ImGuiTheme.getButtonTheme();
            buttonTheme.text = "Refresh";
            if (ImGuiTheme.button("Refresh_Devices", buttonTheme))
                refreshDevices();
            ImGuiTheme.newLine();
            ImGuiTheme.newLine();
            ImGuiTheme.newLine();
            ImGui.Text("SDR:");
            if (ImGuiTheme.glowingCombo("devicetabs", ref deviceID, available_Devices, inputTheme))
                updateDevice();
            ImGuiTheme.newLine();
            renderDeviceData();
        }
    }
}
