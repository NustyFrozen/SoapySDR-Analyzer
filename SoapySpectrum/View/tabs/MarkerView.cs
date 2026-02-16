using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class MarkerView : TabViewModel
{
    private readonly Configuration _config;
    public MarkerView(Configuration config,GraphPlotManager GraphData)
    {
        _config = config;
        _GraphData = GraphData;
    }

    public override void Render()
    {
        Theme.InputTheme.Prefix = "Marker";
        Theme.GlowingCombo("marker_combo", ref _GraphData.SSelectedMarker, MarkerCombo, Theme.InputTheme);

        ImGui.Checkbox($"Enable Marker {_GraphData.SSelectedMarker + 1}", ref _GraphData.Markers[_GraphData.SSelectedMarker].IsActive);
        if (_GraphData.Markers[_GraphData.SSelectedMarker].IsActive)
        {
            Theme.Text("Trace:", Theme.InputTheme);
            Theme.GlowingCombo("marker_reference", ref _GraphData.Markers[_GraphData.SSelectedMarker].Reference, SMarkerTraceCombo, Theme.InputTheme);

            Theme.NewLine();
            Theme.Text("Source:", Theme.InputTheme);
            Theme.GlowingCombo("marker_delta_reference", ref _GraphData.Markers[_GraphData.SSelectedMarker].DeltaReference, SMarkerRefPoint, Theme.InputTheme);

            Theme.NewLine();

            Theme.ButtonTheme.Text = $"{FontAwesome5.ArrowUp} Peak Search";
            if (Theme.Button("peakSearch", Theme.ButtonTheme))
                PeakSearch(_GraphData.Markers[_GraphData.SSelectedMarker], (float)_config.FreqStart, (float)_config.FreqStop);

            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.ArrowUp} Next Pk Right";
            if (Theme.Button("Next ", Theme.ButtonTheme))
                PeakSearch(_GraphData.Markers[_GraphData.SSelectedMarker], (float)_GraphData.Markers[_GraphData.SSelectedMarker].Position, (float)_config.FreqStop);

            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.ArrowUp} Next Pk Left";
            if (Theme.Button("peakSearch", Theme.ButtonTheme))
                PeakSearch(_GraphData.Markers[_GraphData.SSelectedMarker], (float)_config.FreqStart, (float)_GraphData.Markers[_GraphData.SSelectedMarker].Position);

            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.Mountain} Set Delta";
            if (Theme.Button("markerDelta", Theme.ButtonTheme))
            {
                _GraphData.Markers[_GraphData.SSelectedMarker].Delta = true;
                MarkerSetDelta(_GraphData.SSelectedMarker);
            }

            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.Eraser} Clear Delta";
            if (Theme.Button("markerDelta", Theme.ButtonTheme))
                _GraphData.Markers[_GraphData.SSelectedMarker].Delta = false;

            Theme.NewLine();
            Theme.NewLine();

            ImGui.Checkbox("Enable Band Power", ref _GraphData.Markers[_GraphData.SSelectedMarker].BandPower);
            if (_GraphData.Markers[_GraphData.SSelectedMarker].BandPower)
            {
                Theme.NewLine();
                Theme.Text($"{FontAwesome5.ArrowLeft} Span {FontAwesome5.ArrowRight}:", Theme.InputTheme);
                if (Theme.GlowingInput("InputSelectortext11", ref _GraphData.Markers[_GraphData.SSelectedMarker].BandPowerSpanStr, Theme.InputTheme))
                {
                    if (Global.TryFormatFreq(_GraphData.Markers[_GraphData.SSelectedMarker].BandPowerSpanStr, out var results))
                        _GraphData.Markers[_GraphData.SSelectedMarker].BandPowerSpan = results;
                    else
                        _logger.Error("couldn't change bandPowerSpan Invalid Double exponent Value");
                }
            }
        }
    }
}