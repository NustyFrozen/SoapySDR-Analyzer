using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class AmplitudeView(MainWindowView initiator): TabViewModel
{
    public override void Render()
    {
        Theme.Text("Min Range (dBm)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Min Range";
        if (Theme.GlowingInput("min dB", ref SDisplayStartDb, Theme.InputTheme))
        {
            double results;

            if (double.TryParse(SDisplayStartDb, out results))
            {
                if (results < (double)_parent.Configuration.Config[Configuration.SaVar.GraphStopDb])
                    _parent.Configuration.Config[Configuration.SaVar.GraphStartDb] = results;
            }
            else
            {
                _logger.Error("couldn't change Graph range");
            }
        }

        Theme.Text("Max Range (dBm)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Max Range";
        if (Theme.GlowingInput("max dB", ref SDisplayStopDb, Theme.InputTheme))
        {
            double results;

            if (double.TryParse(SDisplayStopDb, out results))
            {
                if (results > (double)_parent.Configuration.Config[Configuration.SaVar.GraphStartDb])
                    _parent.Configuration.Config[Configuration.SaVar.GraphStopDb] = results;
            }
            else
            {
                _logger.Error("couldn't change Graph range");
            }
        }

        Theme.Text($"{FontAwesome5.Plus} Offset (dB)", Theme.InputTheme);
        if (Theme.GlowingInput("Amplitude Offset", ref SDisplayOffset, Theme.InputTheme))
        {
            double results;
            if (double.TryParse(SDisplayOffset, out results))
                _parent.Configuration.Config[Configuration.SaVar.GraphOffsetDb] = results;
            else
                _logger.Error("couldn't change Graph Offset Invalid integer Value");
        }

        Theme.NewLine();
        if (Theme.Button("Auto Tune"))
        {
            var temp = _parent.Configuration.Config;
        }

        Theme.NewLine();
        Theme.Text($"{FontAwesome5.Plus} Ref Level (dB)", Theme.InputTheme);
        if (Theme.GlowingInput("Ref level", ref SDisplayRefLevel, Theme.InputTheme))
        {
            double results;
            if (double.TryParse(SDisplayRefLevel, out results))
                _parent.Configuration.Config[Configuration.SaVar.GraphRefLevel] = results;
            else
                _logger.Error("couldn't change Graph level Invalid integer Value");
        }

        Theme.NewLine();
        if (ImGui.Checkbox("Auto Adjust", ref SAutomaticLevelingEnabled))
            _parent.Configuration.Config[Configuration.SaVar.AutomaticLevel] = SAutomaticLevelingEnabled;

        Theme.NewLine();
        Theme.Text("Scale/Div (5-60)", Theme.InputTheme);
        if (ImGui.InputInt("Scale/Div", ref SScalePerDivision))
        {
            if (SScalePerDivision > 60 || SScalePerDivision < 5)
            {
                SScalePerDivision = 20;
                _logger.Error("Invalid Scale per division, out of range (5-60)");
            }

            _parent.Configuration.Config[Configuration.SaVar.ScalePerDivision] = SScalePerDivision;
        }
    }
}