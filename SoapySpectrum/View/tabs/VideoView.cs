using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.tabs;

public partial class VideoView(MainWindowView initiator) : TabViewModel
{

    public override void Render()
    {
        Theme.Text("\uf1fb RBW", Theme.InputTheme);
        Theme.InputTheme.Prefix = "RBW";
        if (Theme.GlowingInput("RBW", ref FftRbw, Theme.InputTheme))
        {
            double rbw = 0;
            if (Global.TryFormatFreq(FftRbw, out rbw))
            {
                if (rbw == 0) return;
                _parent.Configuration.Config[Configuration.SaVar.FftRbw] = rbw;
                _parent.FftManager.ResetIqFilter();
            }
        }

        Theme.NewLine();
        Theme.Text("\uf1fb Segment Averaging", Theme.InputTheme);
        Theme.InputTheme.Prefix = "averaging";
        if (Theme.GlowingInput("fftsegments", ref SFftSegments, Theme.InputTheme))
        {
            var fftSegements = 0;
            if (int.TryParse(SFftSegments, out fftSegements))
                if (fftSegements > 0)
                {
                    _parent.Configuration.Config[Configuration.SaVar.FftSegment] = fftSegements;
                    _parent.FftManager.ResetIqFilter();
                }
                else
                {
                    Logger.Debug("Invalid Amount Of FFT_segments");
                }
            else
                Logger.Debug("Invalid Integer for value segments");
        }

        Theme.Text("(Simillar Effect to VBW)\n" +
                   "more = less noisy, shows less\nnon-periodic signals\nless = show more non-periodic signals\nalso more noisy");
        Theme.NewLine();
        Theme.Text("\uf1fb overlapping", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Overlap Between Segments";
        if (Theme.GlowingInput("fftoverlap", ref SFftOverlap, Theme.InputTheme))
        {
            double fftOverlap = 0;
            if (double.TryParse(SFftOverlap.Replace("%", ""), out fftOverlap))
                if (fftOverlap <= 80 && fftOverlap >= 0)
                {
                    _parent.Configuration.Config[Configuration.SaVar.FftOverlap] = fftOverlap / 100.0;
                    _parent.FftManager.ResetIqFilter();
                }
                else
                {
                    Logger.Debug("overlay is not between 0-80%");
                }
            else
                Logger.Debug("Invalid Integer for value overlap");
        }

        Theme.Text("Range: 0-80%");
        Theme.NewLine();
        Theme.Text("\uf1fb FFT Refresh Rate (hz)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Overlap Between Segments";
        if (Theme.GlowingInput("FFT_refresh_rate", ref DisplayRefreshRate, Theme.InputTheme))
        {
            var refreshRate = 0;
            if (int.TryParse(DisplayRefreshRate, out refreshRate))
                if (refreshRate > 0)
                    _parent.Configuration.Config[Configuration.SaVar.RefreshRate] = 1000 / refreshRate;
                else
                    Logger.Debug("cannot devide by 0");
            else
                Logger.Debug("Invalid value for refresh rate");
        }
    }
}