using Design_imGUINET;
using ImGuiNET;
using SoapySpectrum.Extentions;
using System.Diagnostics;

namespace SoapySpectrum.UI
{
    public static class tab_Marker
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static Stopwatch markerMoveKeys = new Stopwatch();
        public static string[] markersText = new string[] { "Marker 1", "Marker 2", "Marker 3", "Marker 4", "Marker 5", "Marker 6", "Marker 7", "Marker 8", "Marker 9" };
        public static string[] markerReferences;
        public static string[] markerSource = new string[] { "trace" }.Concat(markersText).ToArray();

        public static marker[] markers = new marker[9];



        public struct marker
        {

            public marker()
            {

            }
            public int id, reference;
            public string txtStatus;
            public bool active;
            public double position, value;

            public int deltaReference;
            public bool delta;
            public double DeltaFreq, DeltadB;

            public bool bandPower;
            public double bandPowerSpan = 5e6, bandPowerValue;
            public string bandPower_Span_str = "5M";

        }

        public static void markerMoveNext(marker marker)
        {
            lock (tab_Trace.traces[marker.reference].plot) //could get updateData so we gotta lock it up
            {
                KeyValuePair<float, float>[] plotData = tab_Trace.traces[marker.reference].plot.ToArray();
                for (int i = 0; i < plotData.Length; i++)
                {
                    if (plotData[i].Key == marker.position)
                    {
                        if (i + 1 == plotData.Length) return; //you are out of the bounderies
                        marker.position = plotData[i + 1].Key;
                        return;
                    }
                }
            }
        }
        public static void markerMovePrevious(marker marker)
        {
            lock (tab_Trace.traces[marker.reference].plot) //could get updateData so we gotta lock it up
            {
                KeyValuePair<float, float>[] plotData = tab_Trace.traces[marker.reference].plot.ToArray();
                for (int i = 0; i < plotData.Length; i++)
                {
                    if (plotData[i].Key == marker.position)
                    {
                        if (i == 0) return; //you are out of the bounderies
                        marker.position = plotData[i - 1].Key;
                        return;
                    }
                }
            }
        }

        public static void markerSetDelta(int markerid)
        {

            markers[markerid].DeltaFreq = markers[markerid].position;
            markers[markerid].DeltadB = markers[markerid].value;

        }
        public static float peakSearch(marker marker, float minimumFreq, float maxFreq)
        {
            float peak = 0;
            lock (tab_Trace.traces[marker.reference].plot) //could get updateData so we gotta lock it up
            {
                peak = tab_Trace.traces[marker.reference].plot.Where(x => x.Key >= minimumFreq && x.Key <= maxFreq).MaxBy(entry => entry.Value).Key;
            }
            return peak;
        }
        public static void renderMarker()
        {
            var inputTheme = Theme.getTextTheme();
            inputTheme.prefix = "Marker";
            Theme.glowingCombo("marker_combo", ref Global.selectedMarker, markersText, inputTheme);
            ImGui.Checkbox($"Enable Marker {Global.selectedMarker + 1}", ref tab_Marker.markers[Global.selectedMarker].active);
            if (tab_Marker.markers[Global.selectedMarker].active)
            {
                Theme.Text("Trace:", inputTheme);
                Theme.glowingCombo("marker_reference", ref tab_Marker.markers[Global.selectedMarker].reference, markerReferences, inputTheme);
                if (markerMoveKeys.ElapsedMilliseconds > 25)
                {
                    if (Imports.GetAsyncKeyState(Keys.A))
                        markerMovePrevious(markers[Global.selectedMarker]);
                    if (Imports.GetAsyncKeyState(Keys.D))
                        markerMoveNext(markers[Global.selectedMarker]);
                    markerMoveKeys.Restart();
                }
                Theme.newLine();
                Theme.Text("Source:", inputTheme);
                Theme.glowingCombo("marker_delta_reference", ref markers[Global.selectedMarker].deltaReference, markerSource, inputTheme);
                Theme.newLine();
                //In Case markers[Global.selectedMarker] is enabled we show markers[Global.selectedMarker] features
                var buttonTheme = Theme.getButtonTheme();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.ArrowUp} Peak Search";
                if (Theme.button("peakSearch", buttonTheme) || Imports.GetAsyncKeyState(Keys.Enter))
                {
                    markers[Global.selectedMarker].position = peakSearch(markers[Global.selectedMarker], (float)(double)Configuration.config[saVar.freqStart], (float)(double)(Configuration.config[saVar.freqStop]));
                }
                Theme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.ArrowUp} Next Pk Right";
                if (Theme.button("Next ", buttonTheme))
                {
                    markers[Global.selectedMarker].position = peakSearch(markers[Global.selectedMarker], (float)(double)markers[Global.selectedMarker].position, (float)(double)(Configuration.config[saVar.freqStop]));
                }
                Theme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.ArrowUp} Next Pk Left";
                if (Theme.button("peakSearch", buttonTheme))
                {
                    markers[Global.selectedMarker].position = peakSearch(markers[Global.selectedMarker], (float)(double)Configuration.config[saVar.freqStart], (float)(double)markers[Global.selectedMarker].position);
                }
                Theme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.Mountain} Set Delta";
                if (Theme.button("markerDelta", buttonTheme) || Imports.GetAsyncKeyState(Keys.Enter))
                {
                    markers[Global.selectedMarker].delta = true;
                    markerSetDelta(Global.selectedMarker);
                }
                Theme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.Eraser} Clear Delta";
                if (Theme.button("markerDelta", buttonTheme) || Imports.GetAsyncKeyState(Keys.Enter))
                {
                    markers[Global.selectedMarker].delta = false;
                }
                Theme.newLine();
                Theme.newLine();
                ImGui.Checkbox($"Enable Band Power", ref markers[Global.selectedMarker].bandPower);
                if (markers[Global.selectedMarker].bandPower)
                {
                    Theme.newLine();
                    Theme.Text($"{FontAwesome5.ArrowLeft} Span {FontAwesome5.ArrowRight}:", inputTheme);
                    if (Theme.glowingInput("InputSelectortext11", ref markers[Global.selectedMarker].bandPower_Span_str, inputTheme))
                    {
                        double results = 0;
                        if (tab_Frequency.TryFormatFreq(markers[Global.selectedMarker].bandPower_Span_str, out results))
                        {
                            markers[Global.selectedMarker].bandPowerSpan = results;
                        }
                        else
                        {
                            Logger.Error("couldn't change bandPowerSpan Invalid Double exponent Value");
                        }
                    }
                }
            }
        }
    }
}
