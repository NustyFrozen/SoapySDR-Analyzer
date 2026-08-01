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
                    // snap to the step the element supports, or take the value as typed when it is free
                    var applied = range.Step != 0 ? Math.Round(results / range.Step) * range.Step : results;

                    _DeviceCom.SdrDevice.SetGain(Direction.Rx, _DeviceCom.RxAntenna.Item1, gainElm.Key.Item2, applied);
                    //remember what actually reached the device, so a preset restores this exact split
                    _Config.SetRxGain(_DeviceCom.RxAntenna.Item1, gainElm.Key.Item2, applied);
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

    /// <summary>
    ///     Clocking and stream setup. The clock source lands on the device as soon as it is picked, the
    ///     rest waits for Apply so a half typed rate is never handed to the driver.
    /// </summary>
    private void RenderDeviceSetup()
    {
        if (ClockSources.Length > 0)
        {
            Theme.Text("Clock Source", Theme.InputTheme);
            if (Theme.GlowingCombo("clock_source", ref SSelectedClockSource, ClockSources, Theme.InputTheme))
            {
                _Config.ClockSource = ClockSources[SSelectedClockSource];
                ApplyClocking();
                _logger.Info($"clock source -> {_Config.ClockSource}");
            }
        }

        Theme.Text($"Master Clock (Hz){(MasterClockRateHint.Length > 0 ? $"\n{MasterClockRateHint}" : string.Empty)}",
            Theme.InputTheme);
        Theme.InputTheme.Prefix = "Master Clock";
        Theme.GlowingInput("master_clock", ref SMasterClockRate, Theme.InputTheme);

        Theme.Text($"Sample Rate (Hz){(SampleRateHint.Length > 0 ? $"\n{SampleRateHint}" : string.Empty)}",
            Theme.InputTheme);
        Theme.InputTheme.Prefix = "Sample Rate";
        Theme.GlowingInput("sample_rate", ref SSampleRate, Theme.InputTheme);

        Theme.Text("Stream Args (key=value,...)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Stream Args";
        Theme.GlowingInput("stream_args", ref SStreamArgs, Theme.InputTheme, 128);

        Theme.ButtonTheme.Text = "Apply";
        if (Theme.Button("apply_device_setup", Theme.ButtonTheme))
            ApplyDeviceSetup();
    }

    /// <summary>
    ///     Commits the typed values. Only a changed stream argument re-opens the stream, since that costs a
    ///     restart of the sweep.
    /// </summary>
    private void ApplyDeviceSetup()
    {
        if (Global.TryFormatFreq(SMasterClockRate, out var masterClock) && masterClock >= 0)
            _Config.MasterClockRate = masterClock;
        else
            _logger.Error($"invalid master clock rate '{SMasterClockRate}'");

        //a rejected value keeps the text the user typed, so the typo is visible and fixable
        if (Global.TryFormatFreq(SSampleRate, out var sampleRate) && sampleRate > 0)
            _Config.SampleRate = sampleRate;
        else
            _logger.Error($"invalid sample rate '{SSampleRate}'");

        ApplyClocking();

        var streamArgs = SStreamArgs.Trim();
        if (streamArgs != _Config.StreamArgs)
        {
            _Config.StreamArgs = streamArgs;
            RestartStream();
        }

        //a new master clock moves the rate the device can actually hit, so re-size the FFT around it
        _fftManager.ResetIqFilter();
    }

    public override void Render()
    {
        Theme.NewLine();
        RenderDeviceData();
        Theme.NewLine();

        RenderDeviceSetup();
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