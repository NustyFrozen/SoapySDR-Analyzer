using NLog;
using Pothosware.SoapySDR;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using System.ComponentModel;
using Logger = NLog.Logger;

namespace SoapySA.View.tabs;

public partial class DeviceView
{
    public override string tabName => "\uf2db Device";

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly Configuration _Config;

    private readonly SdrDeviceCom _DeviceCom;
    private readonly PerformFft _fftManager;
    public string[] GainValues;

    public bool SIsCorrectIqEnabled = true;
    public bool SIsinterleavingEnabled;
    public string SOsciliatorLeakageSleep = "0";

    #region clocking and stream setup

    /// <summary>Clock sources the driver reports. Most drivers report none, which hides the selector.</summary>
    public string[] ClockSources = Array.Empty<string>();
    public int SSelectedClockSource;

    public string SMasterClockRate = "0";
    public string SSampleRate = "0";
    public string SStreamArgs = string.Empty;

    /// <summary>What the driver says it accepts, shown next to the inputs so the values are not a guess.</summary>
    public string MasterClockRateHint = string.Empty;
    public string SampleRateHint = string.Empty;

    #endregion clocking and stream setup

    public DeviceView(SdrDeviceCom com, Configuration config,PerformFft fftManager)
    {
        _Config = config;
        _fftManager = fftManager;
        _DeviceCom = com;

        GainValues = new string[com.RxGainValues.Count];
        for (var i = 0; i < GainValues.Length; i++)
            GainValues[i] = com.RxGainValues[i].ToString();

        ReadDeviceCapabilities();
        HookConfig();

        //the preset was loaded before this tab existed, so push what it holds at the device now
        ApplyPreset();
    }

    /// <summary>Hands the device everything a preset carries: clocking first, then the gain distribution.</summary>
    private void ApplyPreset()
    {
        ApplyClocking();
        ApplyGains();
    }

    /// <summary>
    ///     Restores each gain element a preset holds. Elements this device does not have are skipped, so a
    ///     preset written against another SDR still loads.
    /// </summary>
    private void ApplyGains()
    {
        foreach (var gainElm in _DeviceCom.RxGains)
        {
            var channel = gainElm.Key.Item1;
            var element = gainElm.Key.Item2;
            var index = gainElm.Value.Item2;

            if (!_Config.RxGains.TryGetValue(Configuration.GainKey(channel, element), out var value))
                continue;

            try
            {
                _DeviceCom.SdrDevice.SetGain(Direction.Rx, channel, element, value);
                if (index >= 0 && index < GainValues.Length)
                    GainValues[index] = value.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error($"could not restore gain {element} -> {ex.Message}");
            }
        }
    }

    /// <summary>Asks the driver what it supports once: none of this changes while the device is open.</summary>
    private void ReadDeviceCapabilities()
    {
        try
        {
            ClockSources = _DeviceCom.SdrDevice.ClockSources?.ToArray() ?? Array.Empty<string>();
            MasterClockRateHint = DescribeRanges(_DeviceCom.SdrDevice.MasterClockRates);
        }
        catch (Exception ex)
        {
            _logger.Warn($"device does not report its clocking -> {ex.Message}");
        }

        try
        {
            var channel = (int)_DeviceCom.RxAntenna.Item1;
            if (_DeviceCom.DeviceRxSampleRates.TryGetValue(channel, out var rates))
                //FetchSdrData appends an open ended range, which says nothing useful here
                SampleRateHint = DescribeRanges(rates.Where(r => r.Maximum < double.MaxValue));
        }
        catch (Exception ex)
        {
            _logger.Warn($"device does not report its sample rates -> {ex.Message}");
        }
    }

    private static string DescribeRanges(IEnumerable<Pothosware.SoapySDR.Range>? ranges)
    {
        if (ranges is null)
            return string.Empty;

        var described = ranges
            .Select(r => r.Minimum == r.Maximum
                ? $"{r.Minimum / 1e6:0.###}M"
                : $"{r.Minimum / 1e6:0.###}M - {r.Maximum / 1e6:0.###}M")
            .Take(6)
            .ToArray();

        return described.Length == 0 ? string.Empty : string.Join(", ", described);
    }

    private void HookConfig()
    {
        SyncFromConfig();

        _Config.PropertyChanged -= OnConfigPropertyChanged;
        _Config.PropertyChanged += OnConfigPropertyChanged;
        _Config.OnConfigLoadBegin += (object? s, EventArgs e) =>
        {
            SOsciliatorLeakageSleep = _Config.LeakageSleep.ToString();
            SIsCorrectIqEnabled = _Config.IqCorrection;
            SIsinterleavingEnabled = _Config.FreqInterleaving;
        };
        //a loaded preset carries device state with it, and the device has to be told
        _Config.OnConfigLoadEnd += (object? s, EventArgs e) => ApplyPreset();
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Configuration.LeakageSleep):
            case nameof(Configuration.IqCorrection):
            case nameof(Configuration.FreqInterleaving):
            case nameof(Configuration.SampleRate):
            case nameof(Configuration.ClockSource):
            case nameof(Configuration.MasterClockRate):
            case nameof(Configuration.StreamArgs):
                SyncFromConfig();
                break;
        }
    }

    private void SyncFromConfig()
    {
        SOsciliatorLeakageSleep = _Config.LeakageSleep.ToString();
        SIsCorrectIqEnabled = _Config.IqCorrection;
        SIsinterleavingEnabled = _Config.FreqInterleaving;

        SSampleRate = _Config.SampleRate.ToString();
        SMasterClockRate = _Config.MasterClockRate.ToString();
        SStreamArgs = _Config.StreamArgs;

        var selected = Array.IndexOf(ClockSources, _Config.ClockSource);
        SSelectedClockSource = selected < 0 ? 0 : selected;
    }

    /// <summary>
    ///     Pushes the clock source and master clock rate at the device. Both are left alone when unset, so
    ///     a fresh preset never overrides what the driver chose for itself.
    /// </summary>
    private void ApplyClocking()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_Config.ClockSource))
                _DeviceCom.SdrDevice.ClockSource = _Config.ClockSource;

            if (_Config.MasterClockRate > 0)
                _DeviceCom.SdrDevice.MasterClockRate = _Config.MasterClockRate;
        }
        catch (Exception ex)
        {
            _logger.Error($"could not apply clocking -> {ex.Message}");
        }
    }

    /// <summary>Re-opens the receive stream, which is the only way new stream arguments take effect.</summary>
    private void RestartStream()
    {
        if (!_fftManager.IsRunning)
            return;

        _fftManager.StopFft();
        _fftManager.BeginFft();
    }
}
