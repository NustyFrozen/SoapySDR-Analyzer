using NLog;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public static class tab_Frequency
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    //input start and stop OR center and span
    public static string s_displayFreqStart = "930M", s_displayFreqStop = "960M";

    private static string _displayFreqCenter = "945M", _displaySpan = "30M";

    public static void renderFrequency()
    {
        var childSize = Configuration.optionSize;

        Theme.Text("Center Frequency", Theme.inputTheme);
        Theme.inputTheme.prefix = "Center Frequency";
        var hasFrequencyChanged = Theme.glowingInput("frequency_center", ref _displayFreqCenter, Theme.inputTheme);

        Theme.Text("Span", Theme.inputTheme);
        Theme.inputTheme.prefix = "span";
        hasFrequencyChanged |= Theme.glowingInput("frequency_span", ref _displaySpan, Theme.inputTheme);
        if (hasFrequencyChanged) //frequencyChangedByCenterSpan
        {
            double center_frequency = 0, span = 0;
            if (TryFormatFreq(_displayFreqCenter, out center_frequency) && TryFormatFreq(_displaySpan, out span))
            {
                s_displayFreqStart = (center_frequency - span / 2.0).ToString();
                s_displayFreqStop = (center_frequency + span / 2.0).ToString();
            }
        }

        Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", Theme.inputTheme);
        Theme.inputTheme.prefix = " start Frequency";
        hasFrequencyChanged |= Theme.glowingInput("InputSelectortext", ref s_displayFreqStart, Theme.inputTheme);
        Theme.Text($"{FontAwesome5.ArrowRight} Right Band", Theme.inputTheme);
        Theme.inputTheme.prefix = "End Frequency";
        hasFrequencyChanged |= Theme.glowingInput("InputSelectortext2", ref s_displayFreqStop, Theme.inputTheme);

        if (hasFrequencyChanged) //apply frequency change in settings
        {
            double freqStart, freqStop;
            if (TryFormatFreq(s_displayFreqStart, out freqStart) && TryFormatFreq(s_displayFreqStop, out freqStop))
            {
                if (freqStart >= freqStop || !tab_Device.s_deviceFrequencyRange[(int)tab_Device.s_selectedChannel]
                        .ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
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