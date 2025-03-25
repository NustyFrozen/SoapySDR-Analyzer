using Design_imGUINET;
using ImGuiNET;

namespace SoapySpectrum.UI
{
    public static class tab_Amplitude
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static string displayOffset = "0", displayRefLevel = "-40", displayStartDB = "-138", displayStopDB = "0";
        public static int scalePerDivision = 20;
        public static bool automaticLeveling = false;
        public static void renderAmplitude()
        {
            var inputTheme = Theme.getTextTheme();
            Theme.Text($"Min Range (dBm)", inputTheme);
            inputTheme.prefix = $"Min Range";
            if (Theme.glowingInput("min dB", ref displayStartDB, inputTheme))
            {
                double results;

                if (double.TryParse(displayStartDB, out results))
                {
                    if (results < (double)Configuration.config[Configuration.saVar.graphStopDB])
                    {
                        Configuration.config[Configuration.saVar.graphStartDB] = results;
                    }
                }
                else
                {
                    Logger.Error("couldn't change Graph range");
                }
            }

            Theme.Text($"Max Range (dBm)", inputTheme);
            inputTheme.prefix = $"Max Range";
            if (Theme.glowingInput("max dB", ref displayStopDB, inputTheme))
            {
                double results;

                if (double.TryParse(displayStopDB, out results))
                {
                    if (results > (double)Configuration.config[Configuration.saVar.graphStartDB])
                    {
                        Configuration.config[Configuration.saVar.graphStopDB] = results;
                    }
                }
                else
                {
                    Logger.Error("couldn't change Graph range");
                }
            }

            Theme.Text($"{FontAwesome5.Plus} Offset (dB)", inputTheme);
            if (Theme.glowingInput("Amplitude Offset", ref displayOffset, inputTheme))
            {
                double results;
                if (double.TryParse(displayOffset, out results))
                {
                    Configuration.config[Configuration.saVar.graphOffsetDB] = results;
                }
                else
                {
                    Logger.Error("couldn't change Graph Offset Invalid integer Value");
                }
            }
            Theme.newLine();
            if(Theme.button("Auto Tune"))
            {
                var temp = Configuration.config;
            }
            Theme.newLine();
            Theme.Text($"{FontAwesome5.Plus} Ref Level (dB)", inputTheme);
            if (Theme.glowingInput("Ref level", ref displayRefLevel, inputTheme))
            {
                double results;
                if (double.TryParse(displayRefLevel, out results))
                {
                    Configuration.config[Configuration.saVar.graphRefLevel] = results;
                }
                else
                {
                    Logger.Error("couldn't change Graph level Invalid integer Value");
                }
            }
            Theme.newLine();
            if (ImGui.Checkbox("Auto Adjust", ref automaticLeveling))
            {
                Configuration.config[Configuration.saVar.automaticLevel] = automaticLeveling;
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
                Configuration.config[Configuration.saVar.scalePerDivision] = scalePerDivision;
            }
            
        }
    }
}
