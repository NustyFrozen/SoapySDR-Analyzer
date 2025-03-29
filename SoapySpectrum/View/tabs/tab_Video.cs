using ImGuiNET;

namespace SoapySpectrum.UI
{
    public static class tab_Video
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static int selectedFFTLength = 2, selectedFFTWindow = 0;
        static double additionalWindowArgument = 0.5;
        static string additional_text;
        static bool requiredAdditionalWindowArgument = false;
       public static string[] FFTLength = new string[] { "Auto","256", "512", "1024", "2048", "4096", "8192", "16384", "32768", "65535", "131072", "262144", "524288", "1048576" },
                        FFT_Window = new string[] { "Gauss","FlatTop","None"};
        public static string FFT_segments = "1600", FFT_overlap = "50%", FFT_WINDOW_ADDITIONAL = "0.5", refreshrate_text = "1000";
        public static double[] noWindowFunction(int length)
        {
            double[] result = new double[length];
            for (int i = 0; i < length; i++)
                result[i] = 1;
            return result;
        }

        public static void selectWindow()
        {
            if (selectedFFTWindow == 2)
            {
                Func<int, double[]> noFunc = length => noWindowFunction(length);
                Configuration.config[saVar.fftWindow] = noFunc;
                PerformFFT.resetIQFilter();
                return;
            }
            var window_class = Type.GetType("MathNet.Numerics.Window,MathNet.Numerics");
            var method = window_class.GetMethod(FFT_Window[selectedFFTWindow]);
            var method_periodic = method;
            Func<int, double[]> windowFunction;

            //check if has a periodic function for non bounded segment
            if (window_class.GetMethod($"{FFT_Window[selectedFFTWindow]}Periodic") is not null)
                method_periodic = window_class.GetMethod($"{FFT_Window[selectedFFTWindow]}Periodic");

            //check if required additional argument
            if (selectedFFTWindow == 0)
            {
                additional_text = "Sigma:";
                requiredAdditionalWindowArgument = true;
                windowFunction = length => (double[])method.Invoke(null, new object[] { length, additionalWindowArgument });
            }
            else
            {
                requiredAdditionalWindowArgument = false;
                windowFunction = length => (double[])method.Invoke(null, new object[] { length });
            }
            PerformFFT.resetIQFilter();
            Configuration.config[saVar.fftWindow] = windowFunction;
        }
        public static void renderVideo()
        {
            var inputTheme = Theme.getTextTheme();
            Theme.Text($"\uf1fb FFT Length", inputTheme);
            inputTheme.prefix = "FFT Length";
            if (Theme.glowingCombo("fft Length", ref selectedFFTLength, FFTLength, inputTheme))
            {
                if(selectedFFTLength == 0)
                {
                    //auto
                    Configuration.config[saVar.fftSize] = 0;
                } else
                Configuration.config[saVar.fftSize] = int.Parse(FFTLength[selectedFFTLength]);
                PerformFFT.resetIQFilter();
            }


            Theme.newLine();
            Theme.Text($"\uf1fb FFT WINDOW Size", inputTheme);
            inputTheme.prefix = "Window Function";
            if (Theme.glowingCombo("fft Window Function", ref selectedFFTWindow, FFT_Window, inputTheme))
                selectWindow();

            if (requiredAdditionalWindowArgument)
            {
                Theme.newLine();
                Theme.Text($"\uf1fb {additional_text}", inputTheme);
                inputTheme.prefix = $"0.5";
                if (Theme.glowingInput("FFT_WINDOW_additional_text", ref FFT_WINDOW_ADDITIONAL, inputTheme))
                    if (double.TryParse(FFT_WINDOW_ADDITIONAL, out additionalWindowArgument))
                        selectWindow();
            }

            Theme.newLine();
            Theme.Text($"\uf1fb Welch FFT segments", inputTheme);
            inputTheme.prefix = $"How many segments should be in the FFT";
            if (Theme.glowingInput("fftsegments", ref FFT_segments, inputTheme))
            {
                int fft_segements = 0;
                if (int.TryParse(FFT_segments, out fft_segements))
                    if (fft_segements > 0)
                    {
                        Configuration.config[saVar.fftSegment] = fft_segements;
                        PerformFFT.resetIQFilter();
                    }
                    else
                    {
                        Logger.Debug($"Invalid Amount Of FFT_segments");
                    }
                else
                {
                    Logger.Debug($"Invalid Integer for value segments");
                }
            }

            Theme.newLine();
            Theme.Text($"\uf1fb Welch FFT overlap (precentage)", inputTheme);
            inputTheme.prefix = $"Overlap Between Segments";
            if (Theme.glowingInput("fftoverlap", ref FFT_overlap, inputTheme))
            {
                double fft_overlap = 0;
                if (double.TryParse(FFT_overlap.Replace($"%", ""), out fft_overlap))
                    if (fft_overlap <= 80 && fft_overlap >= 0)
                    {
                        Configuration.config[saVar.fftOverlap] = fft_overlap / 100.0;
                        PerformFFT.resetIQFilter();
                    }
                    else
                    {
                        Logger.Debug($"overlay is not between 0-80%");
                    }
                else
                {
                    Logger.Debug($"Invalid Integer for value overlap");
                }
            }
            Theme.newLine();
            Theme.Text($"\uf1fb FFT Refresh Rate (hz)", inputTheme);
            inputTheme.prefix = $"Overlap Between Segments";
            if (Theme.glowingInput("FFT_refresh_rate", ref refreshrate_text, inputTheme))
            {
                long refresh_rate = 0;
                if (long.TryParse(refreshrate_text, out refresh_rate))
                    if (refresh_rate > 0)
                    {
                        Configuration.config[saVar.refreshRate] = (long)1000 / refresh_rate;
                    }
                    else
                    {
                        Logger.Debug($"cannot devide by 0");
                    }
                else
                {
                    Logger.Debug($"Invalid value for refresh rate");
                }
            }
            ImGui.NewLine();
            Theme.Text($"RBW: {PerformFFT.RBW}Hz", inputTheme);
            Theme.Text($"VBW: {PerformFFT.VBW}Hz", inputTheme);
        }
    }
}
