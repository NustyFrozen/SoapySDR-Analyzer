using ImGuiNET;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class MarkerView(MainWindowView initiator)
{
    public void RenderMarker()
    {
        Theme.InputTheme.Prefix = "Marker";
        Theme.GlowingCombo("marker_combo", ref SSelectedMarker, MarkerCombo, Theme.InputTheme);
        ImGui.Checkbox($"Enable Marker {SSelectedMarker + 1}", ref SMarkers[SSelectedMarker].IsActive);
        if (SMarkers[SSelectedMarker].IsActive)
        {
            Theme.Text("Trace:", Theme.InputTheme);
            Theme.GlowingCombo("marker_reference", ref SMarkers[SSelectedMarker].Reference, SMarkerTraceCombo,
                Theme.InputTheme);
            if (MarkerMoveKeys.ElapsedMilliseconds > 25)
            {
                if (Imports.GetAsyncKeyState(Imports.Keys.A))
                    MarkerMovePrevious(SMarkers[SSelectedMarker]);
                if (Imports.GetAsyncKeyState(Imports.Keys.D))
                    MarkerMoveNext(SMarkers[SSelectedMarker]);
                MarkerMoveKeys.Restart();
            }

            Theme.NewLine();
            Theme.Text("Source:", Theme.InputTheme);
            Theme.GlowingCombo("marker_delta_reference", ref SMarkers[SSelectedMarker].DeltaReference,
                SMarkerRefPoint, Theme.InputTheme);
            Theme.NewLine();
            //In Case markers[selectedMarker] is enabled we show markers[selectedMarker] features

            Theme.ButtonTheme.Text = $"{FontAwesome5.ArrowUp} Peak Search";
            if (Theme.Button("peakSearch", Theme.ButtonTheme) || Imports.GetAsyncKeyState(Imports.Keys.Enter))
                PeakSearch(SMarkers[SSelectedMarker],
                    (float)(double)_parent.Configuration.Config[Configuration.SaVar.FreqStart],
                    (float)(double)_parent.Configuration.Config[Configuration.SaVar.FreqStop]);
            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.ArrowUp} Next Pk Right";
            if (Theme.Button("Next ", Theme.ButtonTheme))
                PeakSearch(SMarkers[SSelectedMarker], (float)SMarkers[SSelectedMarker].Position,
                    (float)(double)_parent.Configuration.Config[Configuration.SaVar.FreqStop]);
            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.ArrowUp} Next Pk Left";
            if (Theme.Button("peakSearch", Theme.ButtonTheme))
                PeakSearch(SMarkers[SSelectedMarker],
                    (float)(double)_parent.Configuration.Config[Configuration.SaVar.FreqStart],
                    (float)SMarkers[SSelectedMarker].Position);
            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.Mountain} Set Delta";
            if (Theme.Button("markerDelta", Theme.ButtonTheme) || Imports.GetAsyncKeyState(Imports.Keys.Enter))
            {
                SMarkers[SSelectedMarker].Delta = true;
                MarkerSetDelta(SSelectedMarker);
            }

            Theme.NewLine();
            Theme.ButtonTheme.Text = $"{FontAwesome5.Eraser} Clear Delta";
            if (Theme.Button("markerDelta", Theme.ButtonTheme) || Imports.GetAsyncKeyState(Imports.Keys.Enter))
                SMarkers[SSelectedMarker].Delta = false;
            Theme.NewLine();
            Theme.NewLine();
            ImGui.Checkbox("Enable Band Power", ref SMarkers[SSelectedMarker].BandPower);
            if (SMarkers[SSelectedMarker].BandPower)
            {
                Theme.NewLine();
                Theme.Text($"{FontAwesome5.ArrowLeft} Span {FontAwesome5.ArrowRight}:", Theme.InputTheme);
                if (Theme.GlowingInput("InputSelectortext11", ref SMarkers[SSelectedMarker].BandPowerSpanStr,
                        Theme.InputTheme))
                {
                    double results = 0;
                    if (Global.TryFormatFreq(SMarkers[SSelectedMarker].BandPowerSpanStr, out results))
                        SMarkers[SSelectedMarker].BandPowerSpan = results;
                    else
                        _logger.Error("couldn't change bandPowerSpan Invalid Double exponent Value");
                }
            }
        }
    }
}