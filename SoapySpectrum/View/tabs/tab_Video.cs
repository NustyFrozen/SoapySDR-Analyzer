using ImGuiNET;

namespace SoapyRL.UI
{
    public static class tab_Video
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private static int _selectedFFTLength = 2, _selectedFFTWindow = 0;
        private static double _additionalWindowArgument = 0.5;
        private static string _additionalText, _displayRefreshRate = "1000";
        private static bool _hasWindowArgument = false;

        public static string[] s_fftLengthCombo = new string[] { "Auto", "256", "512", "1024", "2048", "4096", "8192", "16384", "32768", "65535", "131072", "262144", "524288", "1048576" },
                               s_fftWindowCombo = new string[] { "Gauss", "FlatTop", "None" };

        public static string s_fftSegments = "1600", s_fftOverlap = "50%", s_fftWindowAdditionalArgument = "0.5";

        public static double[] noWindowFunction(int length)
        {
            double[] result = new double[length];
            for (int i = 0; i < length; i++)
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
                windowFunction = length => (double[])method.Invoke(null, new object[] { length, _additionalWindowArgument });
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
            var inputTheme = Theme.getTextTheme();
            Theme.Text($"\uf1fb FFT Length", inputTheme);
            inputTheme.prefix = "FFT Length";
            if (Theme.glowingCombo("fft Length", ref _selectedFFTLength, s_fftLengthCombo, inputTheme))
            {
                if (_selectedFFTLength == 0)
                {
                    //auto
                    Configuration.config[Configuration.saVar.fftSize] = 0;
                }
                else
                    Configuration.config[Configuration.saVar.fftSize] = int.Parse(s_fftLengthCombo[_selectedFFTLength]);
                PerformFFT.resetIQFilter();
            }

            Theme.newLine();
            Theme.Text($"\uf1fb FFT WINDOW Size", inputTheme);
            inputTheme.prefix = "Window Function";
            if (Theme.glowingCombo("fft Window Function", ref _selectedFFTWindow, s_fftWindowCombo, inputTheme))
                selectWindow();

            if (_hasWindowArgument)
            {
                Theme.newLine();
                Theme.Text($"\uf1fb {_additionalText}", inputTheme);
                inputTheme.prefix = $"0.5";
                if (Theme.glowingInput("FFT_WINDOW_additional_text", ref s_fftWindowAdditionalArgument, inputTheme))
                    if (double.TryParse(s_fftWindowAdditionalArgument, out _additionalWindowArgument))
                        selectWindow();
            }

            Theme.newLine();
            Theme.Text($"\uf1fb Welch FFT segments", inputTheme);
            inputTheme.prefix = $"How many segments should be in the FFT";
            if (Theme.glowingInput("fftsegments", ref s_fftSegments, inputTheme))
            {
                int fft_segements = 0;
                if (int.TryParse(s_fftSegments, out fft_segements))
                    if (fft_segements > 0)
                    {
                        Configuration.config[Configuration.saVar.fftSegment] = fft_segements;
                        PerformFFT.resetIQFilter();
                    }
                    else
                    {
                        _logger.Debug($"Invalid Amount Of FFT_segments");
                    }
                else
                {
                    _logger.Debug($"Invalid Integer for value segments");
                }
            }

            Theme.newLine();
            Theme.Text($"\uf1fb Welch FFT overlap (precentage)", inputTheme);
            inputTheme.prefix = $"Overlap Between Segments";
            if (Theme.glowingInput("fftoverlap", ref s_fftOverlap, inputTheme))
            {
                double fft_overlap = 0;
                if (double.TryParse(s_fftOverlap.Replace($"%", ""), out fft_overlap))
                    if (fft_overlap <= 80 && fft_overlap >= 0)
                    {
                        Configuration.config[Configuration.saVar.fftOverlap] = fft_overlap / 100.0;
                        PerformFFT.resetIQFilter();
                    }
                    else
                    {
                        _logger.Debug($"overlay is not between 0-80%");
                    }
                else
                {
                    _logger.Debug($"Invalid Integer for value overlap");
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
            ImGui.NewLine();
            Theme.Text($"RBW: {PerformFFT.RBW}Hz", inputTheme);
            Theme.Text($"VBW: {PerformFFT.VBW}Hz", inputTheme);
        }
    }
}