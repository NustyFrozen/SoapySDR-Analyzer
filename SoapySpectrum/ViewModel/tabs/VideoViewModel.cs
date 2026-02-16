using NLog;
using System.ComponentModel;

namespace SoapySA.View.tabs;

public partial class VideoView
{
    public override string tabName => "\uf1fe BW";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // UI-bound fields (strings)
    public string DisplayRefreshRate = "1000";
    public string FftRbw = "0.01M";
    public string SFftOverlap = "50%";
    public string SFftSegments = "1600";
    public string SFftWindowAdditionalArgument = "0.5";

    private void HookConfig()
    {
        SyncFromConfig();

        _config.PropertyChanged -= OnConfigPropertyChanged;
        _config.PropertyChanged += OnConfigPropertyChanged;
        _config.OnConfigLoadBegin += (object? s, EventArgs e) =>
        {
            SFftSegments = _config.FftSegment.ToString();
            FftRbw = _config.FftRbw.ToString();
            DisplayRefreshRate = (_config.RefreshRate * 1000).ToString();
            SFftOverlap = (_config.FftOverlap * 100.0).ToString();
        };
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Configuration.FftRbw):
            case nameof(Configuration.FftSegment):
            case nameof(Configuration.FftOverlap):
            case nameof(Configuration.RefreshRate):
                SyncFromConfig();
                break;
        }
    }

    private void SyncFromConfig()
    {
        FftRbw = _config.FftRbw.ToString();
        SFftSegments = _config.FftSegment.ToString();
        SFftOverlap = (_config.FftOverlap * 100.0).ToString() + "%";

        // Kept your old UI behavior: DisplayRefreshRate = (RefreshRate * 1000).ToString();
        DisplayRefreshRate = (_config.RefreshRate * 1000).ToString();
    }

    public static double[] NoWindowFunction(int length)
    {
        var result = new double[length];
        for (var i = 0; i < length; i++)
            result[i] = 1;
        return result;
    }
}