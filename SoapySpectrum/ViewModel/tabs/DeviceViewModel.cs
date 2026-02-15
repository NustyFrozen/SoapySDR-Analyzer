using NLog;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using System.ComponentModel;

namespace SoapySA.View.tabs;

public partial class DeviceView
{
    public override string tabName => "\uf2db Device";

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly Configuration _Config;

    private readonly SdrDeviceCom _DeviceCom;
    private readonly PerformFft _fftManager;
    public string[] GainValues;

    private bool _initialized;

    public bool SIsCorrectIqEnabled = true;
    public bool SIsinterleavingEnabled;
    public string SOsciliatorLeakageSleep = "0";

    public DeviceView(SdrDeviceCom com, Configuration config,PerformFft fftManager)
    {
        _Config = config;
        _fftManager = fftManager;
        _DeviceCom = com;
        GainValues = new string[com.RxGainValues.Count];

        HookConfig();
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
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Configuration.LeakageSleep):
            case nameof(Configuration.IqCorrection):
            case nameof(Configuration.FreqInterleaving):
                SyncFromConfig();
                break;
        }
    }

    private void SyncFromConfig()
    {
        SOsciliatorLeakageSleep = _Config.LeakageSleep.ToString();
        SIsCorrectIqEnabled = _Config.IqCorrection;
        SIsinterleavingEnabled = _Config.FreqInterleaving;
    }
}