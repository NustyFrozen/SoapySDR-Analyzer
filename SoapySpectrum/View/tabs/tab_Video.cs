using ImGuiNET;

namespace SoapyRL.UI
{
    public static class tab_Video
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private static int _selectedFFTLength = 2, _selectedFFTWindow = 0;
        private static double _additionalWindowArgument = 0.5;
        private static string _additionalText, _displayRefreshRate = "1000";

        public static string[] s_fftWindowCombo = new string[] {"FlatTop", "None" };

        public static string s_rbw = "1M", s_vbw = "10M";

        public static double[] noWindowFunction(int length)
        {
            double[] result = new double[length];
            for (int i = 0; i < length; i++)
                result[i] = 1;
            return result;
        }

        public static void selectWindow()
        {
            if (_selectedFFTWindow is 1)
            {
                Func<int, double[]> noFunc = length => noWindowFunction(length);
                Configuration.config[Configuration.saVar.fftWindow] = noFunc;
                PerformFFT.resetIQFilter();
                return;
            }
            var windowClass = Type.GetType("MathNet.Numerics.Window,MathNet.Numerics");
            var method = windowClass.GetMethod(s_fftWindowCombo[_selectedFFTWindow]);
            Func<int, double[]> windowFunction;

            //check if has a periodic function for non bounded segment

        windowFunction = length => (double[])method.Invoke(null, new object[] { length });
            
            PerformFFT.resetIQFilter();
            Configuration.config[Configuration.saVar.fftWindow] = windowFunction;
        }

        public static void renderVideo()
        {
            var inputTheme = Theme.getTextTheme();

            Theme.newLine();
            Theme.Text($"\uf1fb FFT WINDOW Size", inputTheme);
            inputTheme.prefix = "Window Function";
            if (Theme.glowingCombo("fft Window Function", ref _selectedFFTWindow, s_fftWindowCombo, inputTheme))
                selectWindow();

            Theme.newLine();
            Theme.Text($"\uf1fb RBW", inputTheme);
            inputTheme.prefix = $"RBW";
            if (Theme.glowingInput("RBW", ref s_rbw, inputTheme) || Theme.glowingInput("VBW", ref s_vbw, inputTheme))
            {
                double fft_rbw = 0,fft_vbw = 0;
                if (tab_Frequency.TryFormatFreq(s_rbw, out fft_rbw) && tab_Frequency.TryFormatFreq(s_vbw, out fft_vbw))
                    if (fft_rbw > 0 && fft_vbw > 0)
                    {
                        Configuration.config[Configuration.saVar.fftRBW] = fft_rbw;
                        Configuration.config[Configuration.saVar.fftVBW] = fft_vbw;

                        PerformFFT.resetIQFilter();
                    }
                    else
                    {
                        _logger.Debug($"Invalid variable");
                    }
            }
            Theme.newLine();
            Theme.Text($"\uf1fb FFT Refresh Rate (hz)", inputTheme);
            inputTheme.prefix = $"Overlap Between Segments";
            if (Theme.glowingInput("FFT_refresh_rate", ref _displayRefreshRate, inputTheme))
            {
                long refresh_rate = 0;
                if (long.TryParse(_displayRefreshRate, out refresh_rate))
                    if (refresh_rate > 0)
                    {
                        Configuration.config[Configuration.saVar.refreshRate] = (long)1000 / refresh_rate;
                    }
                    else
                    {
                        _logger.Debug($"cannot devide by 0");
                    }
                else
                {
                    _logger.Debug($"Invalid value for refresh rate");
                }
            }
        }
    }
}