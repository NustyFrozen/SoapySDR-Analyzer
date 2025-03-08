using ClickableTransparentOverlay;
using ImGuiNET;
using System.Numerics;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        static int selectedFFTLength = 2, selectedFFTWindow = 0;
        static double additionalWindowArgument = 0.5;
        static string additional_text;
        bool requiredAdditionalWindowArgument = false;
        static string[] FFTLength = new string[] { "256", "512", "1024", "2048", "4096", "8192", "16384", "32768", "65535", "131072", "262144", "524288", "1048576" },
                        FFT_Window = new string[] {"Hamming","Triangular","Tukey","Lanczos","Nuttall","Hann","Bartlett","BartlettHann"
                            ,"Blackman","BlackmanHarris","BlackmanNuttall",
                            "Cosine","Dirichlet","FlatTop","Gauss","None"};
        static string FFT_segments = "1600", FFT_overlap = "50%", FFT_WINDOW_ADDITIONAL = "0.5", refreshrate_text = "1000";
        double[] noWindowFunction(int length)
        {
            double[] result = new double[length];
            for (int i = 0; i < length; i++)
                result[i] = 1;
            return result;
        }

        public void selectWindow()
        {
            if (selectedFFTWindow == 15)
            {
                Func<int, double[]> noFunc = length => noWindowFunction(length);
                Configuration.config["FFT_WINDOW"] = noFunc;
                Configuration.config["FFT_WINDOW_PERIODIC"] = noFunc;
                PerformFFT.resetIQFilter();
                return;
            }
            var window_class = Type.GetType("MathNet.Numerics.Window,MathNet.Numerics");
            var method = window_class.GetMethod(FFT_Window[selectedFFTWindow]);
            var method_periodic = method;
            Func<int, double[]> windowFunction, windowFunctionPeriodic;

            //check if has a periodic function for non bounded segment
            if (window_class.GetMethod($"{FFT_Window[selectedFFTWindow]}Periodic") is not null)
                method_periodic = window_class.GetMethod($"{FFT_Window[selectedFFTWindow]}Periodic");

            //check if required additional argument
            if (selectedFFTWindow == 2 || selectedFFTWindow == 14)
            {
                if (selectedFFTWindow == 2)
                    additional_text = "Fraction of Cosine:";
                else
                    additional_text = "Sigma:";
                requiredAdditionalWindowArgument = true;
                windowFunction = length => (double[])method.Invoke(null, new object[] { length, additionalWindowArgument });
                windowFunctionPeriodic = length => (double[])method_periodic.Invoke(null, new object[] { length, additionalWindowArgument });
            }
            else
            {
                requiredAdditionalWindowArgument = false;
                windowFunction = length => (double[])method.Invoke(null, new object[] { length });
                windowFunctionPeriodic = length => (double[])method_periodic.Invoke(null, new object[] { length });
            }
            PerformFFT.resetIQFilter();
            Configuration.config["FFT_WINDOW"] = windowFunction;
            Configuration.config["FFT_WINDOW_PERIODIC"] = windowFunctionPeriodic;
        }
        public void renderVideo()
        {
            var inputTheme = ImGuiTheme.getTextTheme();
            ImGui.Text($"\uf1fb FFT Length");
            inputTheme.prefix = "FFT Length";
            inputTheme.size = new Vector2(262, 35);
            if (ImGuiTheme.glowingCombo("fft Length", ref selectedFFTLength, FFTLength, inputTheme))
            {
                Configuration.config["FFT_Size"] = int.Parse(FFTLength[selectedFFTLength]);
                PerformFFT.resetIQFilter();
            }


            ImGuiTheme.newLine();
            ImGui.Text($"\uf1fb FFT WINDOW Size");
            inputTheme.prefix = "Window Function";
            inputTheme.size = new Vector2(262, 35);
            if (ImGuiTheme.glowingCombo("fft Window Function", ref selectedFFTWindow, FFT_Window, inputTheme))
                selectWindow();

            if (requiredAdditionalWindowArgument)
            {
                ImGuiTheme.newLine();
                ImGui.Text($"\uf1fb {additional_text}:");
                inputTheme.prefix = $"0.5";
                if (ImGuiTheme.glowingInput("FFT_WINDOW_additional_text", ref FFT_WINDOW_ADDITIONAL, inputTheme))
                    if (double.TryParse(FFT_WINDOW_ADDITIONAL, out additionalWindowArgument))
                        selectWindow();
            }

            ImGuiTheme.newLine();
            ImGui.Text($"\uf1fb Welch FFT segments:");
            inputTheme.prefix = $"How many segments should be in the FFT";
            if (ImGuiTheme.glowingInput("FFT_segments", ref FFT_segments, inputTheme))
            {
                int fft_segements = 0;
                if (int.TryParse(FFT_segments, out fft_segements))
                    if (fft_segements > 0)
                    {
                        Configuration.config["FFT_segments"] = fft_segements;
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

            ImGuiTheme.newLine();
            ImGui.Text($"\uf1fb Welch FFT overlap (precentage):");
            inputTheme.prefix = $"Overlap Between Segments";
            if (ImGuiTheme.glowingInput("FFT_overlap", ref FFT_overlap, inputTheme))
            {
                double fft_overlap = 0;
                if (double.TryParse(FFT_overlap.Replace($"%", ""), out fft_overlap))
                    if (fft_overlap <= 80 && fft_overlap >= 0)
                    {
                        Configuration.config["FFT_overlap"] = fft_overlap / 100.0;
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
            ImGuiTheme.newLine();
            ImGui.Text($"\uf1fb FFT Refresh Rate (hz):");
            inputTheme.prefix = $"Overlap Between Segments";
            if (ImGuiTheme.glowingInput("FFT_refresh_rate", ref refreshrate_text, inputTheme))
            {
                long refresh_rate = 0;
                if (long.TryParse(refreshrate_text, out refresh_rate))
                    if (refresh_rate > 0)
                    {
                        Configuration.config["refreshRate"] = (long)1000 / refresh_rate;
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
            ImGui.Text($"RBW: {PerformFFT.RBW}Hz");
            ImGui.Text($"VBW: {PerformFFT.VBW}Hz");
        }
    }
}
