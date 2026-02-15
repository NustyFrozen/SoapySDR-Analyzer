using ImGuiNET;
using Pothosware.SoapySDR;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class DeviceView : TabViewModel
{
    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public void RenderDeviceData()
    {
        if (!_initialized)
        {
            for (var i = 0; i < GainValues.Length; i++)
                GainValues[i] = _DeviceCom.RxGainValues[i].ToString();

            _initialized = true;
        }

        ImGui.Text($"{FontAwesome5.Microchip} {_DeviceCom.Descriptor}\n" +
                   $"CH {_DeviceCom.RxAntenna.Item1}\n" +
                   $"ANT {_DeviceCom.RxAntenna.Item2}");

        Theme.Text("Amplifiers", Theme.InputTheme);

        foreach (var gainElm in _DeviceCom.RxGains)
        {
            if (gainElm.Key.Item1 != _DeviceCom.RxAntenna.Item1)
                continue;

            var range = gainElm.Value.Item1;
            var idx = gainElm.Value.Item2;

            ImGui.Text($"{gainElm.Key.Item2} {range.Minimum} - {range.Maximum}");

            if (Theme.GlowingInput($"{gainElm.Key.Item2}", ref GainValues[idx], Theme.InputTheme))
            {
                if (!double.TryParse(GainValues[idx], out var results) || results < range.Minimum || results > range.Maximum)
                {
                    _logger.Error("invalid Double Value or value out of range");
                }
                else
                {
                    if (range.Step != 0)
                    {
                        _DeviceCom.SdrDevice.SetGain(Direction.Rx, _DeviceCom.RxAntenna.Item1, gainElm.Key.Item2,
                            Math.Round(results / range.Step) * range.Step);
                    }
                    else
                    {
                        // free value
                        _DeviceCom.SdrDevice.SetGain(Direction.Rx, _DeviceCom.RxAntenna.Item1, gainElm.Key.Item2, results);
                    }
                }
            }
        }

        Theme.Text($"Sensors Data\n{_DeviceCom.SensorData}", Theme.InputTheme);

        Theme.ButtonTheme.Text = "Refresh Sensors Data";
        if (Theme.Button("Refresh_Sensors", Theme.ButtonTheme))
        {
            // kept as-is
        }
    }

    public override void Render()
    {
        Theme.NewLine();
        RenderDeviceData();
        Theme.NewLine();

        Theme.Text("LO/PLL Leakage sleep (0-1000ms)", Theme.InputTheme);
        if (Theme.GlowingInput("Leakage", ref SOsciliatorLeakageSleep, Theme.InputTheme))
        {
            if (int.TryParse(SOsciliatorLeakageSleep, out var lo) && lo >= 0 && lo <= 1000)
            {
                _Config.LeakageSleep = lo;
            }

            _logger.Debug(_Config.LeakageSleep);
        }

        if (ImGui.Checkbox("IQ correction", ref SIsCorrectIqEnabled))
            _Config.IqCorrection = SIsCorrectIqEnabled;

        if (ImGui.Checkbox("sweep Interleaving", ref SIsinterleavingEnabled))
        {
            _Config.FreqInterleaving = SIsinterleavingEnabled;
            _fftManager.ResetIqFilter();
        }
    }
}