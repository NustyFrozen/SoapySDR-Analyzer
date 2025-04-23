using Design_imGUINET;

namespace SoapyRL.UI
{
    public static class tab_Frequency
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        //input start and stop OR center and span
        public static string s_displayFreqStart = "930M", s_displayFreqStop = "960M";

        private static string _displayFreqCenter = "945M", _displaySpan = "30M";

        public static void renderFrequency()
        {
            var childSize = Configuration.optionSize;
            var inputTheme = Theme.getTextTheme();

            Theme.Text($"Center Frequency", inputTheme);
            inputTheme.prefix = $"Center Frequency";
            bool hasFrequencyChanged = Theme.glowingInput("frequency_center", ref _displayFreqCenter, inputTheme);

            Theme.Text($"Span", inputTheme);
            inputTheme.prefix = $"span";
            hasFrequencyChanged |= Theme.glowingInput("frequency_span", ref _displaySpan, inputTheme);
            if (hasFrequencyChanged) //frequencyChangedByCenterSpan
            {
                double center_frequency = 0, span = 0;
                if (TryFormatFreq(_displayFreqCenter, out center_frequency) && TryFormatFreq(_displaySpan, out span))
                {
                    s_displayFreqStart = (center_frequency - (span / 2.0)).ToString();
                    s_displayFreqStop = (center_frequency + (span / 2.0)).ToString();
                }
            }

            Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", inputTheme);
            inputTheme.prefix = $" start Frequency";
            hasFrequencyChanged |= Theme.glowingInput("InputSelectortext", ref s_displayFreqStart, inputTheme);
            Theme.Text($"{FontAwesome5.ArrowRight} Right Band", inputTheme);
            inputTheme.prefix = "End Frequency";
            hasFrequencyChanged |= Theme.glowingInput("InputSelectortext2", ref s_displayFreqStop, inputTheme);

            if (hasFrequencyChanged) //apply frequency change in settings
            {
                double freqStart, freqStop;
                if (TryFormatFreq(s_displayFreqStart, out freqStart) && TryFormatFreq(s_displayFreqStop, out freqStop))
                {
                    if (freqStart >= freqStop || !tab_Device.s_deviceFrequencyRange[(int)tab_Device.s_selectedChannel].ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
                    {
                        _logger.Error("$ Start or End Frequency is not valid");
                    }
                    else
                    {
                        Configuration.config[Configuration.saVar.freqStart] = freqStart;
                        Configuration.config[Configuration.saVar.freqStop] = freqStop;
                    }
                    PerformFFT.resetIQFilter();
                }
                else
                {
                    _logger.Error("$ Start or End Frequency span is not a valid double");
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