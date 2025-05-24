using NLog;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public class tab_Frequency(MainWindow initiator)
{
    private MainWindow parent = initiator;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    //input start and stop OR center and span
    public string s_displayFreqStart = "930M", s_displayFreqStop = "960M";

    public string s_displayFreqCenter = "945M", s_displaySpan = "30M";

    public void renderFrequency()
    {
        var childSize = parent.Configuration.optionSize;

        Theme.Text("Center Frequency", Theme.inputTheme);
        Theme.inputTheme.prefix = "Center Frequency";
        var hasFrequencyChanged = Theme.glowingInput("frequency_center", ref s_displayFreqCenter, Theme.inputTheme);

        Theme.Text("Span", Theme.inputTheme);
        Theme.inputTheme.prefix = "span";
        hasFrequencyChanged |= Theme.glowingInput("frequency_span", ref s_displaySpan, Theme.inputTheme);
        if (hasFrequencyChanged) //frequencyChangedByCenterSpan
        {
            double center_frequency = 0, span = 0;
            if (Global.TryFormatFreq(s_displayFreqCenter, out center_frequency) && Global.TryFormatFreq(s_displaySpan, out span))
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
            if (Global.TryFormatFreq(s_displayFreqStart, out freqStart) && Global.TryFormatFreq(s_displayFreqStop, out freqStop))
            {
                if (freqStart >= freqStop || !parent.tab_Device.deviceCOM.deviceRxFrequencyRange[(int)parent.tab_Device.deviceCOM.rxAntenna.Item1]
                        .ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
                {
                    _logger.Error("$ Start or End Frequency is not valid");
                }
                else
                {
                    s_displaySpan = (freqStop - freqStart).ToString();
                    s_displayFreqCenter = ((freqStop - freqStart) / 2.0 + freqStart).ToString();
                    parent.Configuration.config[Configuration.saVar.freqStart] = freqStart;
                    parent.Configuration.config[Configuration.saVar.freqStop] = freqStop;
                }

                parent.fftManager.resetIQFilter();
            }
            else
            {
                _logger.Error("$ Start or End Frequency span is not a valid double");
            }
        }
    }
}