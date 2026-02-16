using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.tabs;

public partial class VideoView : TabViewModel
{
    private readonly PerformFft _fftManager;
    private readonly Configuration _config;

    public VideoView(PerformFft fftManager, Configuration config)
    {
        _fftManager= fftManager;
        _config = config;

        HookConfig();
    }

    public override void Render()
    {
        Theme.Text("\uf1fb RBW", Theme.InputTheme);
        Theme.InputTheme.Prefix = "RBW";
        if (Theme.GlowingInput("RBW", ref FftRbw, Theme.InputTheme))
        {
            if (Global.TryFormatFreq(FftRbw, out var rbw))
            {
                if (rbw == 0) return;

                _config.FftRbw = rbw;
                _fftManager.ResetIqFilter();
            }
        }

        Theme.NewLine();
        Theme.Text("\uf1fb Segment Averaging", Theme.InputTheme);
        Theme.InputTheme.Prefix = "averaging";
        if (Theme.GlowingInput("fftsegments", ref SFftSegments, Theme.InputTheme))
        {
            if (int.TryParse(SFftSegments, out var fftSegments))
            {
                if (fftSegments > 0)
                {
                    _config.FftSegment = fftSegments;
                    _fftManager.ResetIqFilter();
                }
                else
                {
                    Logger.Debug("Invalid Amount Of FFT_segments");
                }
            }
            else
            {
                Logger.Debug("Invalid Integer for value segments");
            }
        }

        Theme.Text("(Simillar Effect to VBW)\n" +
                   "more = less noisy, shows less\nnon-periodic signals\nless = show more non-periodic signals\nalso more noisy");

        Theme.NewLine();
        Theme.Text("\uf1fb overlapping", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Overlap Between Segments";
        if (Theme.GlowingInput("fftoverlap", ref SFftOverlap, Theme.InputTheme))
        {
            if (double.TryParse(SFftOverlap.Replace("%", ""), out var fftOverlap))
            {
                if (fftOverlap <= 80 && fftOverlap >= 0)
                {
                    _config.FftOverlap = fftOverlap / 100.0;
                    _fftManager.ResetIqFilter();
                }
                else
                {
                    Logger.Debug("overlay is not between 0-80%");
                }
            }
            else
            {
                Logger.Debug("Invalid Integer for value overlap");
            }
        }

        Theme.Text("Range: 0-80%");
        Theme.NewLine();

        Theme.Text("\uf1fb FFT Refresh Rate (hz)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Refresh Rate";
        if (Theme.GlowingInput("FFT_refresh_rate", ref DisplayRefreshRate, Theme.InputTheme))
        {
            if (int.TryParse(DisplayRefreshRate, out var refreshRateHz))
            {
                if (refreshRateHz > 0)
                {
                    // Kept same semantics as your old code:
                    // store "period" value as 1000 / Hz (integer)
                    _config.RefreshRate = 1000 / refreshRateHz;
                }
                else
                {
                    Logger.Debug("cannot devide by 0");
                }
            }
            else
            {
                Logger.Debug("Invalid value for refresh rate");
            }
        }
    }
}