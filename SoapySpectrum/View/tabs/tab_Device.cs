using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapySA.View.tabs;

public class tab_Device(MainWindow initiator, sdrDeviceCOM com)
{
    private MainWindow parent = initiator;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public float s_osciliatorLeakageSleep;
    public bool s_isCorrectIQEnabled = true, s_isinterleavingEnabled;
    public sdrDeviceCOM deviceCOM = com;

    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>



    public void renderDeviceData()
    {
        ImGui.Text($"{FontAwesome5.Microchip} {deviceCOM.Descriptor}\n" +
                   $"CH {deviceCOM.rxAntenna.Item1}\n" +
                   $"ANT {deviceCOM.rxAntenna.Item2}");
        Theme.Text("Amplifiers", Theme.inputTheme);
        foreach (var gainElm in deviceCOM.rxGains)
            if (gainElm.Key.Item1 == deviceCOM.rxAntenna.Item1)
            {
                var gain = deviceCOM.rxGainValues[gainElm.Value.Item2];
                var range = gainElm.Value.Item1;
                ImGui.Text($"{gainElm.Key.Item2}");
                if (Theme.slider($"{gainElm.Key.Item2}", (float)range.Minimum, (float)range.Maximum, ref deviceCOM.rxGainValues[gainElm.Value.Item2],
                        Theme.sliderTheme))
                {
                    if (range.Step != 0)
                        deviceCOM.sdrDevice.SetGain(Direction.Rx, deviceCOM.rxAntenna.Item1, gainElm.Key.Item2,
                            Math.Round(deviceCOM.rxGainValues[gainElm.Value.Item2] / range.Step) * range.Step);
                    else
                        //free value
                        deviceCOM.sdrDevice.SetGain(Direction.Rx, deviceCOM.rxAntenna.Item1, gainElm.Key.Item2, deviceCOM.rxGainValues[gainElm.Value.Item2]);
                }
            }

        Theme.Text($"Sensors Data\n{deviceCOM.sensorData}", Theme.inputTheme);

        Theme.buttonTheme.text = "Refresh Sensors Data";
        if (Theme.button("Refresh_Sensors", Theme.buttonTheme))
        {
            // var i = 0;
            // foreach (var sensor in s_sdrDevice.ListSensors())
            //     _deviceSensorData[i++] = $"{sensor}: {s_sdrDevice.ReadSensor(sensor)}";
        }
    }

    public void renderDevice()
    {
        Theme.newLine();
        renderDeviceData();
        Theme.newLine();
        Theme.Text("LO/PLL Leakage sleep", Theme.inputTheme);
        if (Theme.slider("Leakage", ref s_osciliatorLeakageSleep, Theme.sliderTheme))
        {
            parent.Configuration.config[Configuration.saVar.leakageSleep] = (int)(s_osciliatorLeakageSleep * 100);
            _logger.Debug(parent.Configuration.config[Configuration.saVar.leakageSleep]);
        }

        if (ImGui.Checkbox("IQ correction", ref s_isCorrectIQEnabled))
            parent.Configuration.config[Configuration.saVar.iqCorrection] = s_isCorrectIQEnabled;
        if (ImGui.Checkbox("sweep Interleaving", ref s_isinterleavingEnabled))
        {
            parent.Configuration.config[Configuration.saVar.freqInterleaving] = s_isinterleavingEnabled;
            parent.fftManager.resetIQFilter();
        }
    }
}