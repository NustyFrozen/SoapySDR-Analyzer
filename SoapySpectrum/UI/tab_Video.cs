using ClickableTransparentOverlay;
using ImGuiNET;
using System.Numerics;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        static int selectedFFTWINDOW = 2;
        static string[] FFTWindow = new string[] { "256", "512", "1024", "2048", "4096", "8192", "16384", "32768 " };
        static string spectralAverage = "1600";
        public void renderVideo()
        {
            var inputTheme = ImGuiTheme.getTextTheme();
            ImGui.Text($"\uf1fb FFT WINDOW Size");
            inputTheme.prefix = "FFT Window";
            inputTheme.size = new Vector2(262, 35);
            if (ImGuiTheme.glowingCombo("fft window", ref selectedFFTWINDOW, FFTWindow, inputTheme))
            {
                Configuration.config["FFTSize"] = int.Parse(FFTWindow[selectedFFTWINDOW]);

            }
            ImGuiTheme.newLine();
            ImGui.Text($"\uf1fb Welch Averaging:");
            inputTheme.prefix = $"Spectral Average";
            if (ImGuiTheme.glowingInput("InputSelectortext", ref spectralAverage, inputTheme))
            {
                int average = 0;
                if (int.TryParse(spectralAverage, out average))
                    if (average > 0)
                        Configuration.config["weleching"] = average;

            }

        }
    }
}
