using NLog;
using SoapyRTSA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapyRTSA.View.tabs;

/// <summary>
///     Centre and span. There is no hopping here: the radio stares at one centre frequency and the span
///     only ever zooms inside the bandwidth the sample rate already gives us.
/// </summary>
public sealed class FrequencyView : TabViewModel
{
    public override string tabName => $"{FontAwesome5.ArrowRight} Frequency";

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Configuration _config;
    private readonly PerformRtsa _sweep;

    private string _center = "100M";
    private string _span = "0";

    public FrequencyView(Configuration config, PerformRtsa sweep)
    {
        _config = config;
        _sweep = sweep;

        SyncFromConfig();
        _config.OnConfigLoadEnd += (_, _) => SyncFromConfig();
    }

    private void SyncFromConfig()
    {
        _center = _config.CenterFrequency.ToString();
        _span = _sweep.EffectiveSpan.ToString();
    }

    public override void Render()
    {
        Theme.Text("Center Frequency", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Center";
        if (Theme.GlowingInput("rtsa_center", ref _center, Theme.InputTheme))
            if (Global.TryFormatFreq(_center, out var center) && center > 0)
                _config.CenterFrequency = center;

        Theme.Text($"Span (max {_sweep.EffectiveSpan / 1e6:0.###}M)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Span";
        if (Theme.GlowingInput("rtsa_span", ref _span, Theme.InputTheme))
            if (Global.TryFormatFreq(_span, out var span) && span > 0)
                _config.Span = span;

        Theme.NewLine();
        Theme.Text(
            $"Instantaneous bandwidth is the sample rate.\nA narrower span only zooms the display,\n"
                + $"it does not retune the radio.\n\nRBW {_sweep.EffectiveSpan / _config.FftSize / 1e3:0.###} kHz",
            Theme.InputTheme
        );

        Theme.NewLine();
        Theme.ButtonTheme.Text = "Full Span";
        if (Theme.Button("rtsa_full_span", Theme.ButtonTheme))
        {
            _config.Span = 0; //0 means "whatever the rate gives us"
            SyncFromConfig();
            _logger.Info("span reset to the full instantaneous bandwidth");
        }
    }
}
