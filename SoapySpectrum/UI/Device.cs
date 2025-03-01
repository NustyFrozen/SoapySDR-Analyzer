using ClickableTransparentOverlay;
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
        string selectedAntennas = "RX";
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

            try
            {
                sdr_device = new Device(available_Devices[deviceID]);
            }
            catch (Exception ex)
            {
                Logger.Log(NLog.LogLevel.Error, $"updateDevice -> {ex.Message}");
            }

        }
        void renderDeviceData()
        {
            if (sdr_device == null) return;
            try
            {
                var availableChannels = sdr_device.GetNumChannels(Direction.Rx);
                ImGui.Text($"Channel:");
                for (uint i = 0; i < availableChannels; i++)
                    if (ImGui.RadioButton($"{i}", i == selectedChannel))
                        selectedChannel = i;
                ImGui.Text($"Anntena:");
                foreach (var antenna in sdr_device.ListAntennas(Direction.Rx, selectedChannel))
                    if (ImGui.RadioButton($"{antenna}", antenna == selectedAntennas))
                        selectedAntennas = antenna;
                ImGui.Text($"Amplifiers:");
                foreach (var gain in sdr_device.ListGains(Direction.Rx, selectedChannel))
                {
                    var range = sdr_device.GetGainRange(Direction.Rx, selectedChannel, gain);
                    var gainValue = (float)sdr_device.GetGain(Direction.Rx, selectedChannel, gain);
                    if (ImGui.SliderFloat($"{gain} - {gainValue}", ref gainValue, (float)range.Minimum, (float)range.Maximum, "%.3f", ImGuiSliderFlags.AlwaysClamp))
                        sdr_device.SetGain(Direction.Rx, selectedChannel, gain, Math.Round(gainValue / range.Step) * range.Step);

                }
                ImGui.Text($"Sensors Data:");
                foreach (var sensor in sdr_device.ListSensors())
                {
                    ImGui.Text($"{sensor}: {sdr_device.ReadSensor(sensor)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(NLog.LogLevel.Error, $"renderDeviceData -> {ex.Message}");
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
