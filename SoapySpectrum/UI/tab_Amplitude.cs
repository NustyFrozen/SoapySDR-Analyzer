using ClickableTransparentOverlay;
using Design_imGUINET;
using ImGuiNET;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        static string display_Offset = "0";
        public void renderAmplitude()
        {
            var inputTheme = ImGuiTheme.getTextTheme();
            ImGui.Text($"{FontAwesome5.Plus} Amplitude Offset (dB):");
            inputTheme.prefix = $"{FontAwesome5.Plus} Amplitude Offset";
            if (ImGuiTheme.glowingInput("Amplitude Offset", ref display_Offset, inputTheme))
            {

                refreshConfiguration();

            }
        }
    }
}
