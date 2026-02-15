using NLog;
using System.ComponentModel;

namespace SoapySA.View.tabs;

public partial class AmplitudeView
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public override string tabName => "\ue473 Amplitude";

    private readonly PerformFft _fftManager;
    private readonly Configuration _config;

    public bool SAutomaticLevelingEnabled;
    public string SDisplayOffset = "0";
    public string SDisplayRefLevel = "-40";
    public string SDisplayStartDb = "-138";
    public string SDisplayStopDb = "0";
    public int SScalePerDivision = 20;

    public AmplitudeView(PerformFft fftmanager, Configuration config)
    {
        _fftManager = fftmanager;
        _config = config;

        HookConfig();
    }

    private void HookConfig()
    {
        SyncFromConfig();

        // keep UI fields in sync if config changes elsewhere
        _config.PropertyChanged -= OnConfigPropertyChanged;
        _config.PropertyChanged += OnConfigPropertyChanged;
        _config.OnConfigLoadBegin += (object? s, EventArgs e) =>
        {
            SDisplayStartDb = _config.GraphStartDb.ToString();
            SDisplayStopDb = _config.GraphStopDb.ToString();
            SDisplayOffset = _config.GraphOffsetDb.ToString();
            SDisplayRefLevel = _config.GraphRefLevel.ToString();
            SAutomaticLevelingEnabled = _config.AutomaticLevel;
            SScalePerDivision = _config.ScalePerDivision;
        };
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Configuration.GraphStartDb):
            case nameof(Configuration.GraphStopDb):
            case nameof(Configuration.GraphOffsetDb):
            case nameof(Configuration.GraphRefLevel):
            case nameof(Configuration.AutomaticLevel):
            case nameof(Configuration.ScalePerDivision):
                SyncFromConfig();
                break;
        }
    }

    private void SyncFromConfig()
    {
        SDisplayStartDb = _config.GraphStartDb.ToString();
        SDisplayStopDb = _config.GraphStopDb.ToString();
        SDisplayOffset = _config.GraphOffsetDb.ToString();
        SDisplayRefLevel = _config.GraphRefLevel.ToString();
        SAutomaticLevelingEnabled = _config.AutomaticLevel;
        SScalePerDivision = _config.ScalePerDivision;
    }
}