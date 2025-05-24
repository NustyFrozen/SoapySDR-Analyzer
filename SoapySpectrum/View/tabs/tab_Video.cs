using NLog;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.tabs;

public class tab_Video(MainWindow initiator)
{
    private MainWindow parent = initiator;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public string _displayRefreshRate = "1000", _fftRBW = "0.01M";
    public string s_fftSegments = "1600", s_fftOverlap = "50%", s_fftWindowAdditionalArgument = "0.5";

    public static double[] noWindowFunction(int length)
    {
        var result = new double[length];
        for (var i = 0; i < length; i++)
            result[i] = 1;
        return result;
    }

    public void renderVideo()
    {
        Theme.Text("\uf1fb RBW", Theme.inputTheme);
        Theme.inputTheme.prefix = "RBW";
        if (Theme.glowingInput("RBW", ref _fftRBW, Theme.inputTheme))
        {
            double rbw = 0;
            if (Global.TryFormatFreq(_fftRBW, out rbw))
            {
                if (rbw == 0) return;
                parent.Configuration.config[Configuration.saVar.fftRBW] = rbw;
                parent.fftManager.resetIQFilter();
            }
        }

        Theme.newLine();
        Theme.Text("\uf1fb Segment Averaging", Theme.inputTheme);
        Theme.inputTheme.prefix = "averaging";
        if (Theme.glowingInput("fftsegments", ref s_fftSegments, Theme.inputTheme))
        {
            var fft_segements = 0;
            if (int.TryParse(s_fftSegments, out fft_segements))
                if (fft_segements > 0)
                {
                    parent.Configuration.config[Configuration.saVar.fftSegment] = fft_segements;
                    parent.fftManager.resetIQFilter();
                }
                else
                {
                    _logger.Debug("Invalid Amount Of FFT_segments");
                }
            else
                _logger.Debug("Invalid Integer for value segments");
        }

        Theme.Text("(Simillar Effect to VBW)\n" +
                   "more = less noisy, shows less\nnon-periodic signals\nless = show more non-periodic signals\nalso more noisy");
        Theme.newLine();
        Theme.Text("\uf1fb overlapping", Theme.inputTheme);
        Theme.inputTheme.prefix = "Overlap Between Segments";
        if (Theme.glowingInput("fftoverlap", ref s_fftOverlap, Theme.inputTheme))
        {
            double fft_overlap = 0;
            if (double.TryParse(s_fftOverlap.Replace("%", ""), out fft_overlap))
                if (fft_overlap <= 80 && fft_overlap >= 0)
                {
                    parent.Configuration.config[Configuration.saVar.fftOverlap] = fft_overlap / 100.0;
                    parent.fftManager.resetIQFilter();
                }
                else
                {
                    _logger.Debug("overlay is not between 0-80%");
                }
            else
                _logger.Debug("Invalid Integer for value overlap");
        }

        Theme.Text("Range: 0-80%");
        Theme.newLine();
        Theme.Text("\uf1fb FFT Refresh Rate (hz)", Theme.inputTheme);
        Theme.inputTheme.prefix = "Overlap Between Segments";
        if (Theme.glowingInput("FFT_refresh_rate", ref _displayRefreshRate, Theme.inputTheme))
        {
            Int32 refresh_rate = 0;
            if (Int32.TryParse(_displayRefreshRate, out refresh_rate))
                if (refresh_rate > 0)
                    parent.Configuration.config[Configuration.saVar.refreshRate] = 1000 / refresh_rate;
                else
                    _logger.Debug("cannot devide by 0");
            else
                _logger.Debug("Invalid value for refresh rate");
        }
    }
}