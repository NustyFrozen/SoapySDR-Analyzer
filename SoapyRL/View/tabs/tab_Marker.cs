using ImGuiNET;
using SoapyRL.Extentions;
using System.Diagnostics;

namespace SoapyRL.UI
{
    public static class tab_Marker
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public static Stopwatch markerMoveKeys = new Stopwatch();
        private static string[] _markerCombo = new string[] { "Marker 1", "Marker 2", "Marker 3" };
        public static string[] s_markerTraceCombo;
        public static string[] s_markerRefPoint = new string[] { "trace" }.Concat(_markerCombo).ToArray();
        public static int s_selectedMarker = 0;
        public static marker[] s_markers = new marker[9];

        public struct marker
        {
            public marker()
            {
            }

            public int id, reference = 1;
            public string txtStatus;
            public bool isActive;
            public double position, value;
            public double valueRef;
        }

        public static void markerMoveNext(marker marker)
        {
            lock (tab_Trace.s_traces[marker.reference].plot) //could get updateData so we gotta lock it up
            {
                KeyValuePair<float, float>[] plotData = tab_Trace.s_traces[marker.reference].plot.ToArray();
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
            lock (tab_Trace.s_traces[marker.reference].plot) //could get updateData so we gotta lock it up
            {
                KeyValuePair<float, float>[] plotData = tab_Trace.s_traces[marker.reference].plot.ToArray();
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

        public static float peakSearch(marker marker, float minimumFreq, float maxFreq)
        {
            float peak = 0;
            lock (tab_Trace.s_traces[marker.reference].plot) //could get updateData so we gotta lock it up
            {
                peak = tab_Trace.s_traces[marker.reference].plot.Where(x => x.Key >= minimumFreq && x.Key <= maxFreq).MaxBy(entry => entry.Value).Key;
            }
            return peak;
        }

        public static void renderMarker()
        {
            var inputTheme = Theme.getTextTheme();
            inputTheme.prefix = "Marker";
            Theme.glowingCombo("marker_combo", ref s_selectedMarker, _markerCombo, inputTheme);
            ImGui.Checkbox($"Enable Marker {s_selectedMarker + 1}", ref tab_Marker.s_markers[s_selectedMarker].isActive);
            if (tab_Marker.s_markers[s_selectedMarker].isActive)
            {
                Theme.Text("Trace:", inputTheme);
                Theme.glowingCombo("marker_reference", ref tab_Marker.s_markers[s_selectedMarker].reference, s_markerTraceCombo, inputTheme);
                if (markerMoveKeys.ElapsedMilliseconds > 25)
                {
                    if (Imports.GetAsyncKeyState(Keys.A))
                        markerMovePrevious(s_markers[s_selectedMarker]);
                    if (Imports.GetAsyncKeyState(Keys.D))
                        markerMoveNext(s_markers[s_selectedMarker]);
                    markerMoveKeys.Restart();
                }
                Theme.newLine();
                Theme.Text("Source:", inputTheme);
                Theme.newLine();
                //In Case markers[selectedMarker] is enabled we show markers[selectedMarker] features
                var buttonTheme = Theme.getButtonTheme();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.ArrowUp} Peak Search";
                if (Theme.button("peakSearch", buttonTheme) || Imports.GetAsyncKeyState(Keys.Enter))
                {
                    s_markers[s_selectedMarker].position = peakSearch(s_markers[s_selectedMarker], (float)(double)Configuration.config[Configuration.saVar.freqStart], (float)(double)(Configuration.config[Configuration.saVar.freqStop]));
                }
                Theme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.ArrowUp} Next Pk Right";
                if (Theme.button("Next ", buttonTheme))
                {
                    s_markers[s_selectedMarker].position = peakSearch(s_markers[s_selectedMarker], (float)(double)s_markers[s_selectedMarker].position, (float)(double)(Configuration.config[Configuration.saVar.freqStop]));
                }
                Theme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.ArrowUp} Next Pk Left";
                if (Theme.button("peakSearch", buttonTheme))
                {
                    s_markers[s_selectedMarker].position = peakSearch(s_markers[s_selectedMarker], (float)(double)Configuration.config[Configuration.saVar.freqStart], (float)(double)s_markers[s_selectedMarker].position);
                }
                Theme.newLine();
                Theme.newLine();
            }
        }
    }
}