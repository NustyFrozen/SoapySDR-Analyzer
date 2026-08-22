using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using SoapyRTSA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapyRTSA.View.tabs;

/// <summary>
///     Radio setup: gains, clocking, rate and stream arguments. Everything that touches the driver waits
///     for Apply, so a half typed number never reaches the hardware.
/// </summary>
public sealed class DeviceView : TabViewModel
{
    public override string tabName => $"{FontAwesome5.Microchip} Device";

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Configuration _config;
    private readonly SdrDeviceCom _com;
    private readonly PerformRtsa _sweep;

    private readonly string[] _gainValues;
    private readonly string[] _clockSources;
    private int _selectedClockSource;

    private string _sampleRate = "0";
    private string _masterClock = "0";
    private string _streamArgs = string.Empty;
    private string _sampleRateHint = string.Empty;

    public DeviceView(Configuration config, SdrDeviceCom com, PerformRtsa sweep)
    {
        _config = config;
        _com = com;
        _sweep = sweep;

        _gainValues = new string[com.RxGainValues.Count];
        for (var i = 0; i < _gainValues.Length; i++)
            _gainValues[i] = com.RxGainValues[i].ToString();

        try
        {
            _clockSources = com.SdrDevice.ClockSources?.ToArray() ?? Array.Empty<string>();
        }
        catch (Exception exception)
        {
            _logger.Warn($"device does not report clock sources -> {exception.Message}");
            _clockSources = Array.Empty<string>();
        }

        try
        {
            var channel = (int)com.RxAntenna.Item1;
            if (com.DeviceRxSampleRates.TryGetValue(channel, out var rates))
            {
                //FetchSdrData appends an open ended range that says nothing useful
                var real = rates.Where(x => x.Maximum < double.MaxValue).ToArray();
                if (real.Length > 0)
                    _sampleRateHint = $"{real.Min(x => x.Minimum) / 1e6:0.###}M - {real.Max(x => x.Maximum) / 1e6:0.###}M";
            }
        }
        catch (Exception exception)
        {
            _logger.Warn($"device does not report sample rates -> {exception.Message}");
        }

        SyncFromConfig();
        _config.OnConfigLoadEnd += (_, _) =>
        {
            SyncFromConfig();
            ApplyPreset();
        };

        ApplyPreset();
    }

    private void SyncFromConfig()
    {
        _sampleRate = _config.SampleRate > 0 ? _config.SampleRate.ToString() : _com.RxSampleRate.ToString();
        _masterClock = _config.MasterClockRate.ToString();
        _streamArgs = _config.StreamArgs;

        var index = Array.IndexOf(_clockSources, _config.ClockSource);
        _selectedClockSource = index < 0 ? 0 : index;
    }

    /// <summary>Hands the device what a preset carries: clocking first, then the gain distribution.</summary>
    private void ApplyPreset()
    {
        ApplyClocking();

        foreach (var gain in _com.RxGains)
        {
            var channel = gain.Key.Item1;
            var element = gain.Key.Item2;
            var index = gain.Value.Item2;

            if (!_config.RxGains.TryGetValue(Configuration.GainKey(channel, element), out var value))
                continue;

            try
            {
                _com.SdrDevice.SetGain(Direction.Rx, channel, element, value);
                if (index >= 0 && index < _gainValues.Length)
                    _gainValues[index] = value.ToString();
            }
            catch (Exception exception)
            {
                _logger.Error($"could not restore gain {element} -> {exception.Message}");
            }
        }
    }

    private void ApplyClocking()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_config.ClockSource))
                _com.SdrDevice.ClockSource = _config.ClockSource;

            if (_config.MasterClockRate > 0)
                _com.SdrDevice.MasterClockRate = _config.MasterClockRate;
        }
        catch (Exception exception)
        {
            _logger.Error($"could not apply clocking -> {exception.Message}");
        }
    }

    public override void Render()
    {
        ImGui.Text($"{FontAwesome5.Microchip} {_com.Descriptor}\nCH {_com.RxAntenna.Item1}  ANT {_com.RxAntenna.Item2}");
        Theme.NewLine();

        RenderGains();
        Theme.NewLine();

        if (_clockSources.Length > 0)
        {
            Theme.Text("Clock Source", Theme.InputTheme);
            if (Theme.GlowingCombo("rtsa_clock", ref _selectedClockSource, _clockSources, Theme.InputTheme))
            {
                _config.ClockSource = _clockSources[_selectedClockSource];
                ApplyClocking();
            }
        }

        Theme.Text("Master Clock (Hz)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Master Clock";
        Theme.GlowingInput("rtsa_master_clock", ref _masterClock, Theme.InputTheme);

        Theme.Text($"Sample Rate (Hz){(_sampleRateHint.Length > 0 ? $"\n{_sampleRateHint}" : string.Empty)}",
            Theme.InputTheme);
        Theme.InputTheme.Prefix = "Sample Rate";
        Theme.GlowingInput("rtsa_sample_rate", ref _sampleRate, Theme.InputTheme);

        Theme.Text("Stream Args (key=value,...)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Stream Args";
        Theme.GlowingInput("rtsa_stream_args", ref _streamArgs, Theme.InputTheme, 128);

        Theme.ButtonTheme.Text = "Apply";
        if (Theme.Button("rtsa_apply_device", Theme.ButtonTheme))
            Apply();

        Theme.NewLine();
        var pinned = _config.PinCores;
        if (ImGui.Checkbox("Pin sweep threads to their own cores", ref pinned))
        {
            _config.PinCores = pinned;
            _logger.Info("core pinning takes effect the next time the sweep starts");
        }
    }

    private void RenderGains()
    {
        Theme.Text("Amplifiers", Theme.InputTheme);

        foreach (var gain in _com.RxGains)
        {
            if (gain.Key.Item1 != _com.RxAntenna.Item1)
                continue;

            var range = gain.Value.Item1;
            var index = gain.Value.Item2;
            if (index < 0 || index >= _gainValues.Length)
                continue;

            ImGui.Text($"{gain.Key.Item2} {range.Minimum} - {range.Maximum}");
            if (!Theme.GlowingInput($"rtsa_gain_{gain.Key.Item2}", ref _gainValues[index], Theme.InputTheme))
                continue;

            if (!double.TryParse(_gainValues[index], out var value) || value < range.Minimum || value > range.Maximum)
            {
                _logger.Error($"gain {gain.Key.Item2} out of range");
                continue;
            }

            //snap to the step the element supports, or take it as typed when it is free
            var applied = range.Step != 0 ? Math.Round(value / range.Step) * range.Step : value;

            try
            {
                _com.SdrDevice.SetGain(Direction.Rx, _com.RxAntenna.Item1, gain.Key.Item2, applied);
                _config.SetRxGain(_com.RxAntenna.Item1, gain.Key.Item2, applied);
            }
            catch (Exception exception)
            {
                _logger.Error($"could not set gain {gain.Key.Item2} -> {exception.Message}");
            }
        }
    }

    private void Apply()
    {
        if (Global.TryFormatFreq(_masterClock, out var masterClock) && masterClock >= 0)
            _config.MasterClockRate = masterClock;
        else
            _logger.Error($"invalid master clock rate '{_masterClock}'");

        if (Global.TryFormatFreq(_sampleRate, out var rate) && rate > 0)
            _config.SampleRate = rate;
        else
            _logger.Error($"invalid sample rate '{_sampleRate}'");

        ApplyClocking();

        var streamArgs = _streamArgs.Trim();
        if (streamArgs == _config.StreamArgs)
            return;

        //stream arguments only take hold when the stream is opened again
        _config.StreamArgs = streamArgs;
        if (!_sweep.IsRunning)
            return;

        _sweep.Stop();
        _sweep.Begin();
    }
}
