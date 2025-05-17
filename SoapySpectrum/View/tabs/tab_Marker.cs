using ImGuiNET;
using NLog;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using System.Diagnostics;

namespace SoapySA.View.tabs;

public class tab_Marker(MainWindow initiator)
{
    private MainWindow parent = initiator;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public static Stopwatch markerMoveKeys = new();

    private static readonly string[] _markerCombo = new[]
        { "Marker 1", "Marker 2", "Marker 3", "Marker 4", "Marker 5", "Marker 6", "Marker 7", "Marker 8", "Marker 9" };

    public static string[] s_markerTraceCombo;
    public static string[] s_markerRefPoint = new[] { "trace" }.Concat(_markerCombo).ToArray();
    public int s_selectedMarker;
    public marker[] s_markers = new marker[9];

    public void markerMoveNext(marker marker)
    {
        lock (parent.tab_Trace.s_traces[marker.reference].plot) //could get updateData so we gotta lock it up
        {
            var plotData = parent.tab_Trace.s_traces[marker.reference].plot.ToArray();
            for (var i = 0; i < plotData.Length; i++)
                if (plotData[i].Key == marker.position)
                {
                    if (i + 1 == plotData.Length) return; //you are out of the bounderies
                    marker.position = plotData[i + 1].Key;
                    return;
                }
        }
    }

    public void markerMovePrevious(marker marker)
    {
        lock (parent.tab_Trace.s_traces[marker.reference].plot) //could get updateData so we gotta lock it up
        {
            var plotData = parent.tab_Trace.s_traces[marker.reference].plot.ToArray();
            for (var i = 0; i < plotData.Length; i++)
                if (plotData[i].Key == marker.position)
                {
                    if (i == 0) return; //you are out of the bounderies
                    marker.position = plotData[i - 1].Key;
                    return;
                }
        }
    }

    public void markerSetDelta(int markerid)
    {
        s_markers[markerid].DeltaFreq = s_markers[markerid].position;
        s_markers[markerid].DeltadB = s_markers[markerid].value;
    }

    public void peakSearch(marker marker, float minimumFreq, float maxFreq)
    {
        lock (parent.tab_Trace.s_traces[marker.reference].plot) //could get updateData so we gotta lock it up
        {
            var peakPointArry = parent.tab_Trace.s_traces[marker.reference].plot
                .Where(x => x.Key >= minimumFreq && x.Key <= maxFreq).OrderByDescending(entry => entry.Value).ToList();
            s_markers[marker.id].position = peakPointArry[0].Key;
            s_markers[marker.id].value = peakPointArry[0].Value;
        }
    }

    public void renderMarker()
    {
        Theme.inputTheme.prefix = "Marker";
        Theme.glowingCombo("marker_combo", ref s_selectedMarker, _markerCombo, Theme.inputTheme);
        ImGui.Checkbox($"Enable Marker {s_selectedMarker + 1}", ref s_markers[s_selectedMarker].isActive);
        if (s_markers[s_selectedMarker].isActive)
        {
            Theme.Text("Trace:", Theme.inputTheme);
            Theme.glowingCombo("marker_reference", ref s_markers[s_selectedMarker].reference, s_markerTraceCombo,
                Theme.inputTheme);
            if (markerMoveKeys.ElapsedMilliseconds > 25)
            {
                if (Imports.GetAsyncKeyState(Imports.Keys.A))
                    markerMovePrevious(s_markers[s_selectedMarker]);
                if (Imports.GetAsyncKeyState(Imports.Keys.D))
                    markerMoveNext(s_markers[s_selectedMarker]);
                markerMoveKeys.Restart();
            }

            Theme.newLine();
            Theme.Text("Source:", Theme.inputTheme);
            Theme.glowingCombo("marker_delta_reference", ref s_markers[s_selectedMarker].deltaReference,
                s_markerRefPoint, Theme.inputTheme);
            Theme.newLine();
            //In Case markers[selectedMarker] is enabled we show markers[selectedMarker] features

            Theme.buttonTheme.text = $"{FontAwesome5.ArrowUp} Peak Search";
            if (Theme.button("peakSearch", Theme.buttonTheme) || Imports.GetAsyncKeyState(Imports.Keys.Enter))
                peakSearch(s_markers[s_selectedMarker],
                    (float)(double)parent.Configuration.config[Configuration.saVar.freqStart],
                    (float)(double)parent.Configuration.config[Configuration.saVar.freqStop]);
            Theme.newLine();
            Theme.buttonTheme.text = $"{FontAwesome5.ArrowUp} Next Pk Right";
            if (Theme.button("Next ", Theme.buttonTheme))
                peakSearch(s_markers[s_selectedMarker], (float)s_markers[s_selectedMarker].position,
                    (float)(double)parent.Configuration.config[Configuration.saVar.freqStop]);
            Theme.newLine();
            Theme.buttonTheme.text = $"{FontAwesome5.ArrowUp} Next Pk Left";
            if (Theme.button("peakSearch", Theme.buttonTheme))
                peakSearch(s_markers[s_selectedMarker],
                    (float)(double)parent.Configuration.config[Configuration.saVar.freqStart],
                    (float)s_markers[s_selectedMarker].position);
            Theme.newLine();
            Theme.buttonTheme.text = $"{FontAwesome5.Mountain} Set Delta";
            if (Theme.button("markerDelta", Theme.buttonTheme) || Imports.GetAsyncKeyState(Imports.Keys.Enter))
            {
                s_markers[s_selectedMarker].delta = true;
                markerSetDelta(s_selectedMarker);
            }

            Theme.newLine();
            Theme.buttonTheme.text = $"{FontAwesome5.Eraser} Clear Delta";
            if (Theme.button("markerDelta", Theme.buttonTheme) || Imports.GetAsyncKeyState(Imports.Keys.Enter))
                s_markers[s_selectedMarker].delta = false;
            Theme.newLine();
            Theme.newLine();
            ImGui.Checkbox("Enable Band Power", ref s_markers[s_selectedMarker].bandPower);
            if (s_markers[s_selectedMarker].bandPower)
            {
                Theme.newLine();
                Theme.Text($"{FontAwesome5.ArrowLeft} Span {FontAwesome5.ArrowRight}:", Theme.inputTheme);
                if (Theme.glowingInput("InputSelectortext11", ref s_markers[s_selectedMarker].bandPowerSpan_str,
                        Theme.inputTheme))
                {
                    double results = 0;
                    if (tab_Frequency.TryFormatFreq(s_markers[s_selectedMarker].bandPowerSpan_str, out results))
                        s_markers[s_selectedMarker].bandPowerSpan = results;
                    else
                        _logger.Error("couldn't change bandPowerSpan Invalid Double exponent Value");
                }
            }
        }
    }

    public struct marker
    {
        public marker()
        {
        }

        public int id, reference;
        public string txtStatus;
        public bool isActive;
        public double position, value;

        public int deltaReference;
        public bool delta;
        public double DeltaFreq, DeltadB;

        public bool bandPower;
        public double bandPowerSpan = 5e6, bandPowerValue;
        public string bandPowerSpan_str = "5M";
    }
}