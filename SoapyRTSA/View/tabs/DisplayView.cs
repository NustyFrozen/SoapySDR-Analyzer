using ImGuiNET;
using SoapyRTSA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapyRTSA.View.tabs;

/// <summary>
///     How the persistence behaves: how long a hit lingers, how hard rare hits are pushed up the colour
///     ramp, the transform size and the overlap that decides probability of intercept.
/// </summary>
public sealed class DisplayView : TabViewModel
{
    public override string tabName => $"{FontAwesome5.Filter} Display";

    private static readonly string[] FftSizes = { "256", "512", "1024", "2048", "4096", "8192" };
    private static readonly string[] Windows = { "Rectangular", "Hann", "Hamming", "Blackman-Harris", "Flat Top" };

    private readonly Configuration _config;
    private readonly PerformRtsa _sweep;

    private int _fftIndex = 2;
    private int _windowIndex = 3;
    private float _fade = 1.0f;
    private float _gamma = 3.0f;
    private float _overlap = 75.0f;
    private int _waterfallInterval = 30;

    public DisplayView(Configuration config, PerformRtsa sweep)
    {
        _config = config;
        _sweep = sweep;

        SyncFromConfig();
        _config.OnConfigLoadEnd += (_, _) => SyncFromConfig();
    }

    private void SyncFromConfig()
    {
        _fftIndex = Math.Max(0, Array.IndexOf(FftSizes, _config.FftSize.ToString()));
        _windowIndex = (int)_config.Window;
        _fade = (float)_config.FadeSeconds;
        _gamma = (float)_config.DensityGamma;
        _overlap = _config.OverlapPercent;
        _waterfallInterval = _config.WaterfallIntervalMs;
    }

    public override void Render()
    {
        Theme.Text("FFT Size", Theme.InputTheme);
        if (Theme.GlowingCombo("rtsa_fft_size", ref _fftIndex, FftSizes, Theme.InputTheme))
            if (int.TryParse(FftSizes[_fftIndex], out var size))
                _config.FftSize = size;

        Theme.Text("Window", Theme.InputTheme);
        if (Theme.GlowingCombo("rtsa_window", ref _windowIndex, Windows, Theme.InputTheme))
            _config.Window = (FftWindowType)_windowIndex;

        Theme.NewLine();
        Theme.Text($"Overlap {_overlap:0}%  ->  {_sweep.FramesPerSecond / 1000.0:0.0}k FFT/s", Theme.InputTheme);
        if (Theme.Slider("rtsa_overlap", 0, 95, ref _overlap, Theme.SliderTheme))
            _config.OverlapPercent = (int)_overlap;

        Theme.NewLine();
        Theme.Text($"Fade {_fade:0.##} s", Theme.InputTheme);
        if (Theme.Slider("rtsa_fade", 0.05f, 10.0f, ref _fade, Theme.SliderTheme))
            _config.FadeSeconds = _fade;

        Theme.NewLine();
        Theme.Text($"Density gamma {_gamma:0.##}\n1 shows only what is always there,\nhigher pulls rare hits up out of the floor",
            Theme.InputTheme);
        if (Theme.Slider("rtsa_gamma", 1.0f, 8.0f, ref _gamma, Theme.SliderTheme))
            _config.DensityGamma = _gamma;

        Theme.NewLine();
        var waterfall = _config.ShowWaterfall;
        if (ImGui.Checkbox("Waterfall", ref waterfall))
            _config.ShowWaterfall = waterfall;

        if (waterfall)
        {
            Theme.Text("Waterfall row every (ms)", Theme.InputTheme);
            if (ImGui.InputInt("Waterfall interval", ref _waterfallInterval))
            {
                _waterfallInterval = Math.Clamp(_waterfallInterval, 1, 1000);
                _config.WaterfallIntervalMs = _waterfallInterval;
            }
        }

        Theme.NewLine();
        Theme.ButtonTheme.Text = "Clear Persistence";
        if (Theme.Button("rtsa_clear", Theme.ButtonTheme))
            _sweep.ClearDisplay();

        Theme.NewLine();
        Theme.Text($"Transform: {_sweep.EngineName}", Theme.InputTheme);

        Theme.NewLine();
        Theme.Text(
            $"A burst shorter than one window is only\ncaught whole by some transform, which is\nwhat overlap buys."
                + $"\n\nWindow {_config.FftSize / (_sweep.EffectiveSpan <= 0 ? 1 : _sweep.EffectiveSpan) * 1e6:0.##} us"
                + $"\nHop {_config.FftSize * (100 - _config.OverlapPercent) / 100.0 / (_sweep.EffectiveSpan <= 0 ? 1 : _sweep.EffectiveSpan) * 1e6:0.##} us",
            Theme.InputTheme
        );
    }
}
