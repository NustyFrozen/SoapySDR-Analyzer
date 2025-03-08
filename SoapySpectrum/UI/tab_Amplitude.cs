using ClickableTransparentOverlay;
using Design_imGUINET;
using ImGuiNET;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        static string display_Offset = "0", display_refLevel = "0";
        public void renderAmplitude()
        {
            var inputTheme = ImGuiTheme.getTextTheme();
            ImGui.Text($"{FontAwesome5.Plus} Offset (dB):");
            inputTheme.prefix = $"{FontAwesome5.Plus} Amplitude Offset";
            if (ImGuiTheme.glowingInput("Amplitude Offset", ref display_Offset, inputTheme))
            {
                double results;
                if (double.TryParse(display_Offset, out results))
                {
                    Configuration.config["graph_OffsetDB"] = results;
                }
                else
                {
                    Logger.Error("couldn't change Graph Offset Invalid integer Value");
                }
            }
            ImGuiTheme.newLine();
            ImGui.Text($"{FontAwesome5.Plus} Ref Level (dB):");
            if (ImGuiTheme.glowingInput("Ref level", ref display_refLevel, inputTheme))
            {
                double results;
                if (double.TryParse(display_Offset, out results))
                {
                    Configuration.config["graph_RefLevel"] = results;
                }
                else
                {
                    Logger.Error("couldn't change Graph level Invalid integer Value");
                }
            }
        }
    }
}
