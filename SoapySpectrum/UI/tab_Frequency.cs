using ClickableTransparentOverlay;
using Design_imGUINET;
using ImGuiNET;
using Pothosware.SoapySDR;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        static string display_FreqStart = "930M", display_FreqStop = "960M";
        static string display_center = "945M", display_span = "30M";
        public void renderFrequency()
        {
            var childSize = Configuration.option_Size;
            var inputTheme = ImGuiTheme.getTextTheme();

            ImGui.Text($"Center Frequency:");
            inputTheme.prefix = $"Center Frequency";
            bool changedFrequencyBySpan = ImGuiTheme.glowingInput("frequency_center", ref display_center, inputTheme);
            bool changedFrequencyByBand = false;
            ImGui.Text($"Span:");
            inputTheme.prefix = $"span";
            changedFrequencyBySpan |= ImGuiTheme.glowingInput("frequency_span", ref display_span, inputTheme);
            if (changedFrequencyBySpan)
            {
                double center_frequency = 0, span = 0;
                if (TryFormatFreq(display_center, out center_frequency) && TryFormatFreq(display_span, out span))
                {
                    changedFrequencyByBand = true;
                    display_FreqStart = (center_frequency - (span / 2.0)).ToString();
                    display_FreqStop = (center_frequency + (span / 2.0)).ToString();
                }
            }

            ImGui.Text($"{FontAwesome5.ArrowLeft} Left Band:");
            inputTheme.prefix = $" start Frequency";
            changedFrequencyByBand |= ImGuiTheme.glowingInput("InputSelectortext", ref display_FreqStart, inputTheme);
            ImGui.Text($"{FontAwesome5.ArrowRight} Right Band:");
            inputTheme.prefix = "End Frequency";
            changedFrequencyByBand |= ImGuiTheme.glowingInput("InputSelectortext2", ref display_FreqStop, inputTheme);

            if (changedFrequencyByBand)
            {
                double freqStart, freqStop;
                if (TryFormatFreq(display_FreqStart, out freqStart) && TryFormatFreq(display_FreqStop, out freqStop))
                {
                    if (freqStart >= freqStop || !frequencyRange[(int)selectedChannel].ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
                    {
                        Logger.Error("$ Start or End Frequency is not valid");
                    }
                    else
                    {
                        Configuration.config["freqStart"] = freqStart;
                        Configuration.config["freqStop"] = freqStop;
                    }
                    PerformFFT.resetIQFilter();
                }
                else
                {
                    Logger.Error("$ Start or End Frequency span is not a valid double");
                }

            }
        }
        public static bool TryFormatFreq(string input, out double value)
        {
            input = input.ToUpper();
            double exponent = 1;
            if (input.Contains("K"))
                exponent = 1e3;
            if (input.Contains("M"))
                exponent = 1e6;
            if (input.Contains("G"))
                exponent = 1e9;
            double results = 80000000;
            if (!double.TryParse(input.Replace("K", "").Replace("M", "").Replace("G", ""), out results))
            {
                value = 0;
                return false;
            }
            value = results * exponent;
            return true;
        }
    }
}
