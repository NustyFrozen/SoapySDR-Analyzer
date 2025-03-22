using Design_imGUINET;
using ImGuiNET;

namespace SoapySpectrum.UI
{
    public static class tab_Amplitude
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        static string display_Offset = "0", display_refLevel = "-40", display_startdB = "-138", display_stopdB = "0";
        public static int scalePerDivision = 20;
        public static bool automaticLeveling = false;
        public static void renderAmplitude()
        {
            var inputTheme = Theme.getTextTheme();
            Theme.Text($"Min Range (dBm)", inputTheme);
            inputTheme.prefix = $"Min Range";
            if (Theme.glowingInput("min dB", ref display_startdB, inputTheme))
            {
                double results;

                if (double.TryParse(display_startdB, out results))
                {
                    if (results < (double)Configuration.config["graph_endDB"])
                    {
                        Configuration.config["graph_startDB"] = results;
                    }
                }
                else
                {
                    Logger.Error("couldn't change Graph range");
                }
            }

            Theme.Text($"Max Range (dBm)", inputTheme);
            inputTheme.prefix = $"Max Range";
            if (Theme.glowingInput("max dB", ref display_stopdB, inputTheme))
            {
                double results;

                if (double.TryParse(display_stopdB, out results))
                {
                    if (results > (double)Configuration.config["graph_startDB"])
                    {
                        Configuration.config["graph_endDB"] = results;
                    }
                }
                else
                {
                    Logger.Error("couldn't change Graph range");
                }
            }

            Theme.Text($"{FontAwesome5.Plus} Offset (dB)", inputTheme);
            if (Theme.glowingInput("Amplitude Offset", ref display_Offset, inputTheme))
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
            Theme.newLine();
            Theme.Text($"{FontAwesome5.Plus} Ref Level (dB)", inputTheme);
            if (Theme.glowingInput("Ref level", ref display_refLevel, inputTheme))
            {
                double results;
                if (double.TryParse(display_refLevel, out results))
                {
                    Configuration.config["graph_RefLevel"] = results;
                }
                else
                {
                    Logger.Error("couldn't change Graph level Invalid integer Value");
                }
            }
            Theme.newLine();
            if (ImGui.Checkbox("Auto Adjust", ref automaticLeveling))
            {
                Configuration.config["automaticLeveling"] = automaticLeveling;
            }

            Theme.newLine();
            Theme.Text($"Scale/Div (5-60)", inputTheme);
            if (ImGui.InputInt("Scale/Div", ref scalePerDivision))
            {
                if (scalePerDivision > 60 || scalePerDivision < 5)
                {
                    scalePerDivision = 20;
                    Logger.Error("Invalid Scale per division, out of range (5-60)");
                }
                Configuration.config["scalePerDivision"] = scalePerDivision;
            }
        }
    }
}
