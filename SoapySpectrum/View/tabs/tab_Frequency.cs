using Design_imGUINET;
using Pothosware.SoapySDR;

namespace SoapySpectrum.UI
{
    public static class tab_Frequency
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static string display_FreqStart = "930M", display_FreqStop = "960M";
        static string display_center = "945M", display_span = "30M";
        public static void renderFrequency()
        {
            var childSize = Configuration.option_Size;
            var inputTheme = Theme.getTextTheme();

            Theme.Text($"Center Frequency", inputTheme);
            inputTheme.prefix = $"Center Frequency";
            bool changedFrequencyBySpan = Theme.glowingInput("frequency_center", ref display_center, inputTheme);
            bool changedFrequencyByBand = false;
            Theme.Text($"Span", inputTheme);
            inputTheme.prefix = $"span";
            changedFrequencyBySpan |= Theme.glowingInput("frequency_span", ref display_span, inputTheme);
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

            Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", inputTheme);
            inputTheme.prefix = $" start Frequency";
            changedFrequencyByBand |= Theme.glowingInput("InputSelectortext", ref display_FreqStart, inputTheme);
            Theme.Text($"{FontAwesome5.ArrowRight} Right Band", inputTheme);
            inputTheme.prefix = "End Frequency";
            changedFrequencyByBand |= Theme.glowingInput("InputSelectortext2", ref display_FreqStop, inputTheme);

            if (changedFrequencyByBand)
            {
                double freqStart, freqStop;
                if (TryFormatFreq(display_FreqStart, out freqStart) && TryFormatFreq(display_FreqStop, out freqStop))
                {
                    if (freqStart >= freqStop || !tab_Device.frequencyRange[(int)tab_Device.selectedChannel].ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
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
