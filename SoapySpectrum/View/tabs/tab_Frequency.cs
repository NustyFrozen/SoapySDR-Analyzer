using Design_imGUINET;
using Pothosware.SoapySDR;

namespace SoapySpectrum.UI
{
    public static class tab_Frequency
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static string d_FreqStart = "930M", d_FreqStop = "960M";
        static string d_center = "945M", d_span = "30M";
        public static void renderFrequency()
        {
            var childSize = Configuration.optionSize;
            var inputTheme = Theme.getTextTheme();

            Theme.Text($"Center Frequency", inputTheme);
            inputTheme.prefix = $"Center Frequency";
            bool changedFrequencyBySpan = Theme.glowingInput("frequency_center", ref d_center, inputTheme);
            bool changedFrequencyByBand = false;
            Theme.Text($"Span", inputTheme);
            inputTheme.prefix = $"span";
            changedFrequencyBySpan |= Theme.glowingInput("frequency_span", ref d_span, inputTheme);
            if (changedFrequencyBySpan)
            {
                double center_frequency = 0, span = 0;
                if (TryFormatFreq(d_center, out center_frequency) && TryFormatFreq(d_span, out span))
                {
                    changedFrequencyByBand = true;
                    d_FreqStart = (center_frequency - (span / 2.0)).ToString();
                    d_FreqStop = (center_frequency + (span / 2.0)).ToString();
                }
            }

            Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", inputTheme);
            inputTheme.prefix = $" start Frequency";
            changedFrequencyByBand |= Theme.glowingInput("InputSelectortext", ref d_FreqStart, inputTheme);
            Theme.Text($"{FontAwesome5.ArrowRight} Right Band", inputTheme);
            inputTheme.prefix = "End Frequency";
            changedFrequencyByBand |= Theme.glowingInput("InputSelectortext2", ref d_FreqStop, inputTheme);

            if (changedFrequencyByBand)
            {
                double freqStart, freqStop;
                if (TryFormatFreq(d_FreqStart, out freqStart) && TryFormatFreq(d_FreqStop, out freqStop))
                {
                    if (freqStart >= freqStop || !tab_Device.availableChannels[Global.selectedChannel].frequencyRange.ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
                    {
                        Logger.Error("$ Start or End Frequency is not valid");
                    }
                    else
                    {
                        var temp = tab_Device.availableChannels[Global.selectedChannel];
                        temp.freqStart = freqStart;
                        temp.freqStop = freqStop;
                        tab_Device.availableChannels[Global.selectedChannel] = temp;
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
