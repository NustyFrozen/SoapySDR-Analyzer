using ImGuiNET;
using NLog;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs
{
    public class tab_Amplitude(MainWindow initiator)
    {
        private MainWindow parent = initiator;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public string s_displayOffset = "0",
            s_displayRefLevel = "-40",
            s_displayStartDB = "-138",
            s_displayStopDB = "0";

        public int s_scalePerDivision = 20;
        public bool s_automaticLevelingEnabled;

        public void renderAmplitude()
        {
            Theme.Text("Min Range (dBm)", Theme.inputTheme);
            Theme.inputTheme.prefix = "Min Range";
            if (Theme.glowingInput("min dB", ref s_displayStartDB, Theme.inputTheme))
            {
                double results;

                if (double.TryParse(s_displayStartDB, out results))
                {
                    if (results < (double)parent.Configuration.config[Configuration.saVar.graphStopDB])
                        parent.Configuration.config[Configuration.saVar.graphStartDB] = results;
                }
                else
                {
                    _logger.Error("couldn't change Graph range");
                }
            }

            Theme.Text("Max Range (dBm)", Theme.inputTheme);
            Theme.inputTheme.prefix = "Max Range";
            if (Theme.glowingInput("max dB", ref s_displayStopDB, Theme.inputTheme))
            {
                double results;

                if (double.TryParse(s_displayStopDB, out results))
                {
                    if (results > (double)parent.Configuration.config[Configuration.saVar.graphStartDB])
                        parent.Configuration.config[Configuration.saVar.graphStopDB] = results;
                }
                else
                {
                    _logger.Error("couldn't change Graph range");
                }
            }

            Theme.Text($"{FontAwesome5.Plus} Offset (dB)", Theme.inputTheme);
            if (Theme.glowingInput("Amplitude Offset", ref s_displayOffset, Theme.inputTheme))
            {
                double results;
                if (double.TryParse(s_displayOffset, out results))
                    parent.Configuration.config[Configuration.saVar.graphOffsetDB] = results;
                else
                    _logger.Error("couldn't change Graph Offset Invalid integer Value");
            }

            Theme.newLine();
            if (Theme.button("Auto Tune"))
            {
                var temp = parent.Configuration.config;
            }

            Theme.newLine();
            Theme.Text($"{FontAwesome5.Plus} Ref Level (dB)", Theme.inputTheme);
            if (Theme.glowingInput("Ref level", ref s_displayRefLevel, Theme.inputTheme))
            {
                double results;
                if (double.TryParse(s_displayRefLevel, out results))
                    parent.Configuration.config[Configuration.saVar.graphRefLevel] = results;
                else
                    _logger.Error("couldn't change Graph level Invalid integer Value");
            }

            Theme.newLine();
            if (ImGui.Checkbox("Auto Adjust", ref s_automaticLevelingEnabled))
                parent.Configuration.config[Configuration.saVar.automaticLevel] = s_automaticLevelingEnabled;

            Theme.newLine();
            Theme.Text("Scale/Div (5-60)", Theme.inputTheme);
            if (ImGui.InputInt("Scale/Div", ref s_scalePerDivision))
            {
                if (s_scalePerDivision > 60 || s_scalePerDivision < 5)
                {
                    s_scalePerDivision = 20;
                    _logger.Error("Invalid Scale per division, out of range (5-60)");
                }

                parent.Configuration.config[Configuration.saVar.scalePerDivision] = s_scalePerDivision;
            }
        }
    }
}