using ImGuiNET;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class AmplitudeView : TabViewModel
{
    public override void Render()
    {
        Theme.Text("Min Range (dBm)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Min Range";
        if (Theme.GlowingInput("min dB", ref SDisplayStartDb, Theme.InputTheme))
        {
            if (double.TryParse(SDisplayStartDb, out var results))
            {
                if (results < _config.GraphStopDb)
                    _config.GraphStartDb = results;
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
            if (double.TryParse(SDisplayStopDb, out var results))
            {
                if (results > _config.GraphStartDb)
                    _config.GraphStopDb = results;
            }
            else
            {
                _logger.Error("couldn't change Graph range");
            }
        }

        Theme.Text($"{FontAwesome5.Plus} Offset (dB)", Theme.InputTheme);
        if (Theme.GlowingInput("Amplitude Offset", ref SDisplayOffset, Theme.InputTheme))
        {
            if (double.TryParse(SDisplayOffset, out var results))
                _config.GraphOffsetDb = results;
            else
                _logger.Error("couldn't change Graph Offset Invalid integer Value");
        }

        Theme.NewLine();
        if (Theme.Button("Auto Tune"))
        {
            // TODO: implement (kept as-is)
        }

        Theme.NewLine();
        Theme.Text($"{FontAwesome5.Plus} Ref Level (dB)", Theme.InputTheme);
        if (Theme.GlowingInput("Ref level", ref SDisplayRefLevel, Theme.InputTheme))
        {
            if (double.TryParse(SDisplayRefLevel, out var results))
                _config.GraphRefLevel = results;
            else
                _logger.Error("couldn't change Graph level Invalid integer Value");
        }

        Theme.NewLine();
        if (ImGui.Checkbox("Auto Adjust", ref SAutomaticLevelingEnabled))
            _config.AutomaticLevel = SAutomaticLevelingEnabled;

        Theme.NewLine();
        Theme.Text("Scale/Div (5-60)", Theme.InputTheme);
        if (ImGui.InputInt("Scale/Div", ref SScalePerDivision))
        {
            if (SScalePerDivision > 60 || SScalePerDivision < 5)
            {
                SScalePerDivision = 20;
                _logger.Error("Invalid Scale per division, out of range (5-60)");
            }

            _config.ScalePerDivision = SScalePerDivision;
        }
    }
}