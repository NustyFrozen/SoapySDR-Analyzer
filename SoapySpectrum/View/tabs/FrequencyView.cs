using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class FrequencyView(MainWindowView initiator) : TabViewModel
{
    //input start and stop OR center and span

    public override void Render()
    {
        var childSize = _parent.Configuration.OptionSize;

        Theme.Text("Center Frequency", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Center Frequency";
        var hasFrequencyChanged = Theme.GlowingInput("frequency_center", ref SDisplayFreqCenter, Theme.InputTheme);
        Theme.Text("Span", Theme.InputTheme);
        Theme.InputTheme.Prefix = "span";
        hasFrequencyChanged |= Theme.GlowingInput("frequency_span", ref SDisplaySpan, Theme.InputTheme);
        if (hasFrequencyChanged) //frequencyChangedByCenterSpan
        {
            double centerFrequency = 0, span = 0;
            if (Global.TryFormatFreq(SDisplayFreqCenter, out centerFrequency) &&
                Global.TryFormatFreq(SDisplaySpan, out span))
            {
                SDisplayFreqStart = (centerFrequency - span / 2.0).ToString();
                SDisplayFreqStop = (centerFrequency + span / 2.0).ToString();
            }
        }

        Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", Theme.InputTheme);
        Theme.InputTheme.Prefix = " start Frequency";
        hasFrequencyChanged |= Theme.GlowingInput("InputSelectortext", ref SDisplayFreqStart, Theme.InputTheme);
        Theme.Text($"{FontAwesome5.ArrowRight} Right Band", Theme.InputTheme);
        Theme.InputTheme.Prefix = "End Frequency";
        hasFrequencyChanged |= Theme.GlowingInput("InputSelectortext2", ref SDisplayFreqStop, Theme.InputTheme);

        if (hasFrequencyChanged) //apply frequency change in settings
        {
            if (Global.TryFormatFreq(SDisplayFreqStart, out double freqStart) &&
                Global.TryFormatFreq(SDisplayFreqStop, out double freqStop))
                ChangeFrequencyByRange(freqStart,freqStop);
            else _logger.Error("$ Start or End Frequency span is not a valid double");
        }
        
    }
}