using ClickableTransparentOverlay;
using Design_imGUINET;
using ImGuiNET;
using SoapySpectrum.soapypower;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        static string display_FreqStart = "930M", display_FreqStop = "960M";

        public void renderFrequency()
        {
            var childSize = Configuration.option_Size;
            var inputTheme = ImGuiTheme.getTextTheme();
            inputTheme.prefix = $" left Frequency";
            ImGui.Text($"{FontAwesome5.ArrowLeft} Left Band:");
            if (ImGuiTheme.glowingInput("InputSelectortext", ref display_FreqStart, inputTheme))
                if (refreshConfiguration())
                    SoapyPower.updateFrequency();
            
            ImGui.NewLine();
            ImGui.NewLine();
            ImGui.Text($"{FontAwesome5.ArrowRight} Right Band:");
            inputTheme.prefix = "End Frequency";
            if (ImGuiTheme.glowingInput("InputSelectortext2", ref display_FreqStop, inputTheme))
                if (refreshConfiguration())
                 SoapyPower.updateFrequency();
            
        }
        public static double formatFreq(string input)
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
                Logger.Error("Invalid Frequency Format, changing to 80000000");
            }
            return results * exponent;
        }
    }
}
