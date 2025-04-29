using NLog;

namespace SoapySA.View.tabs;

public static class tab_Video
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static int _selectedFFTWindow;
    private static double _additionalWindowArgument = 0.5;
    private static string _additionalText, _displayRefreshRate = "1000", _fftRBW = "1M";
    private static bool _hasWindowArgument;

    public static string[] s_fftWindowCombo = new[] { "Gauss", "FlatTop", "None" };

    public static string s_fftSegments = "1600", s_fftOverlap = "50%", s_fftWindowAdditionalArgument = "0.5";

    public static double[] noWindowFunction(int length)
    {
        var result = new double[length];
        for (var i = 0; i < length; i++)
            result[i] = 1;
        return result;
    }

    public static void selectWindow()
    {
        if (_selectedFFTWindow is 2)
        {
            Func<int, double[]> noFunc = length => noWindowFunction(length);
            Configuration.config[Configuration.saVar.fftWindow] = noFunc;
            PerformFFT.resetIQFilter();
            return;
        }

        var windowClass = Type.GetType("MathNet.Numerics.Window,MathNet.Numerics");
        var method = windowClass.GetMethod(s_fftWindowCombo[_selectedFFTWindow]);
        var methodPeriodic = method;
        Func<int, double[]> windowFunction;

        //check if has a periodic function for non bounded segment
        if (windowClass.GetMethod($"{s_fftWindowCombo[_selectedFFTWindow]}Periodic") is not null)
            methodPeriodic = windowClass.GetMethod($"{s_fftWindowCombo[_selectedFFTWindow]}Periodic");

        //check if required additional argument
        if (_selectedFFTWindow == 0)
        {
            _additionalText = "Sigma:";
            _hasWindowArgument = true;
            windowFunction = length =>
                (double[])method.Invoke(null, new object[] { length, _additionalWindowArgument });
        }
        else
        {
            _hasWindowArgument = false;
            windowFunction = length => (double[])method.Invoke(null, new object[] { length });
        }

        PerformFFT.resetIQFilter();
        Configuration.config[Configuration.saVar.fftWindow] = windowFunction;
    }

    public static void renderVideo()
    {
        Theme.Text("\uf1fb RBW", Theme.inputTheme);
        Theme.inputTheme.prefix = "RBW";
        if (Theme.glowingInput("RBW", ref _fftRBW, Theme.inputTheme))
        {
            double rbw = 0;
            if (tab_Frequency.TryFormatFreq(_fftRBW, out rbw))
            {
                if (rbw == 0) return;
                Configuration.config[Configuration.saVar.fftRBW] = rbw;
                PerformFFT.resetIQFilter();
            }
        }

        Theme.newLine();
        Theme.Text("\uf1fb FFT WINDOW Size", Theme.inputTheme);
        Theme.inputTheme.prefix = "Window Function";
        if (Theme.glowingCombo("fft Window Function", ref _selectedFFTWindow, s_fftWindowCombo, Theme.inputTheme))
            selectWindow();

        if (_hasWindowArgument)
        {
            Theme.newLine();
            Theme.Text($"\uf1fb {_additionalText}", Theme.inputTheme);
            Theme.inputTheme.prefix = "0.5";
            if (Theme.glowingInput("FFT_WINDOW_additional_text", ref s_fftWindowAdditionalArgument, Theme.inputTheme))
                if (double.TryParse(s_fftWindowAdditionalArgument, out _additionalWindowArgument))
                    selectWindow();
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
                    Configuration.config[Configuration.saVar.fftSegment] = fft_segements;
                    PerformFFT.resetIQFilter();
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
                    Configuration.config[Configuration.saVar.fftOverlap] = fft_overlap / 100.0;
                    PerformFFT.resetIQFilter();
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
            long refresh_rate = 0;
            if (long.TryParse(_displayRefreshRate, out refresh_rate))
                if (refresh_rate > 0)
                    Configuration.config[Configuration.saVar.refreshRate] = 1000 / refresh_rate;
                else
                    _logger.Debug("cannot devide by 0");
            else
                _logger.Debug("Invalid value for refresh rate");
        }
    }
}