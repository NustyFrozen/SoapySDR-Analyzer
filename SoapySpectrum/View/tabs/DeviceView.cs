using ImGuiNET;
using Pothosware.SoapySDR;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class DeviceView(MainWindowView initiator, SdrDeviceCom com)
{
    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public void RenderDeviceData()
    {
        if (!_initialized)
        {
            for (var i = 0; i < GainValues.Length; i++) GainValues[i] = com.RxGainValues[i].ToString();
            _initialized = true;
        }

        ImGui.Text($"{FontAwesome5.Microchip} {DeviceCom.Descriptor}\n" +
                   $"CH {DeviceCom.RxAntenna.Item1}\n" +
                   $"ANT {DeviceCom.RxAntenna.Item2}");
        Theme.Text("Amplifiers", Theme.InputTheme);
        foreach (var gainElm in DeviceCom.RxGains)
            if (gainElm.Key.Item1 == DeviceCom.RxAntenna.Item1)
            {
                var gain = GainValues[gainElm.Value.Item2];
                var range = gainElm.Value.Item1;
                ImGui.Text($"{gainElm.Key.Item2} {range.Minimum} - {range.Maximum}");
                if (Theme.GlowingInput($"{gainElm.Key.Item2}", ref GainValues[gainElm.Value.Item2],
                        Theme.InputTheme))
                {
                    double results = 0;
                    var valid = double.TryParse(GainValues[gainElm.Value.Item2], out results);
                    valid |= results >= range.Minimum && results <= range.Maximum;
                    if (!valid)
                    {
                        _logger.Error("invalid Double Value or value ot ouf range");
                    }
                    else
                    {
                        if (range.Step != 0)
                            DeviceCom.SdrDevice.SetGain(Direction.Rx, DeviceCom.RxAntenna.Item1, gainElm.Key.Item2,
                                Math.Round(results / range.Step) * range.Step);
                        else
                            //free value
                            DeviceCom.SdrDevice.SetGain(Direction.Rx, DeviceCom.RxAntenna.Item1, gainElm.Key.Item2,
                                results);
                    }
                }
            }

        Theme.Text($"Sensors Data\n{DeviceCom.SensorData}", Theme.InputTheme);

        Theme.ButtonTheme.Text = "Refresh Sensors Data";
        if (Theme.Button("Refresh_Sensors", Theme.ButtonTheme))
        {
            // var i = 0;
            // foreach (var sensor in s_sdrDevice.ListSensors())
            //     _deviceSensorData[i++] = $"{sensor}: {s_sdrDevice.ReadSensor(sensor)}";
        }
    }

    public void RenderDevice()
    {
        Theme.NewLine();
        RenderDeviceData();
        Theme.NewLine();
        Theme.Text("LO/PLL Leakage sleep (0-1000ms)", Theme.InputTheme);
        if (Theme.GlowingInput("Leakage", ref SOsciliatorLeakageSleep, Theme.InputTheme))
        {
            int lo;
            if (int.TryParse(SOsciliatorLeakageSleep, out lo))
                if (lo >= 0 && lo <= 1000)
                    _parent.Configuration.Config[Configuration.SaVar.LeakageSleep] = lo;
            _logger.Debug(_parent.Configuration.Config[Configuration.SaVar.LeakageSleep]);
        }

        if (ImGui.Checkbox("IQ correction", ref SIsCorrectIqEnabled))
            _parent.Configuration.Config[Configuration.SaVar.IqCorrection] = SIsCorrectIqEnabled;
        if (ImGui.Checkbox("sweep Interleaving", ref SIsinterleavingEnabled))
        {
            _parent.Configuration.Config[Configuration.SaVar.FreqInterleaving] = SIsinterleavingEnabled;
            _parent.FftManager.ResetIqFilter();
        }
    }
}