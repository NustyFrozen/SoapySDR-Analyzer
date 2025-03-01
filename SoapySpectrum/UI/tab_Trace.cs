using ClickableTransparentOverlay;
using Design_imGUINET;
using ImGuiNET;
using SoapySpectrum.Extentions;
using System.Diagnostics;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {

        public static int selectedTrace = 0;
        private string[] Combotraces = new string[] { $"{Design_imGUINET.FontAwesome5.Dog} Trace 1", $"{Design_imGUINET.FontAwesome5.Cat} Trace 2", $"{Design_imGUINET.FontAwesome5.Child} Trace 3" };
        Stopwatch markerMoveKeys = new Stopwatch();
        public void renderTrace()
        {
            var inputTheme = ImGuiTheme.getTextTheme();
            inputTheme.prefix = "RBW";
            ImGuiTheme.glowingCombo("InputSelectortext3", ref selectedTrace, Combotraces, inputTheme);
            ImGuiTheme.newLine();
            ImGui.Text($"{Design_imGUINET.FontAwesome5.Eye} View:");
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.PersonRunning} Active", traces[selectedTrace].viewStatus == traceViewStatus.active))
                traces[selectedTrace].viewStatus = traceViewStatus.active;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Eye} View", traces[selectedTrace].viewStatus == traceViewStatus.view))
                traces[selectedTrace].viewStatus = traceViewStatus.view;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Eraser} Clear", traces[selectedTrace].viewStatus == traceViewStatus.clear))
                traces[selectedTrace].viewStatus = traceViewStatus.clear;

            ImGuiTheme.newLine();
            ImGuiTheme.newLine();
            ImGui.Text($"{Design_imGUINET.FontAwesome5.StreetView} Trace Function:");
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Equal} Normal", traces[selectedTrace].dataStatus == traceDataStatus.normal))
                traces[selectedTrace].dataStatus = traceDataStatus.normal;
            if (ImGui.RadioButton($"\ue4c2 Max Hold", traces[selectedTrace].dataStatus == traceDataStatus.maxHold))
                traces[selectedTrace].dataStatus = traceDataStatus.maxHold;
            if (ImGui.RadioButton($"\ue4b8 Min Hold", traces[selectedTrace].dataStatus == traceDataStatus.minHold))
                traces[selectedTrace].dataStatus = traceDataStatus.minHold;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Microscope} Average", traces[selectedTrace].dataStatus == traceDataStatus.Average))
                traces[selectedTrace].dataStatus = traceDataStatus.Average;
            ImGuiTheme.newLine();

            ImGui.Text($"\uf041 Marker:");
            ImGui.Text("use arrow keys <-A D-> to move the Marker position");

            ImGui.Checkbox($"Enable Marker {selectedTrace + 1}", ref traces[selectedTrace].marker.active);
            if (traces[selectedTrace].marker.active)
            {
                if (markerMoveKeys.ElapsedMilliseconds > 25)
                {
                    if (Imports.GetAsyncKeyState(Keys.A))
                        markerMovePrevious(selectedTrace);
                    if (Imports.GetAsyncKeyState(Keys.D))
                        markerMoveNext(selectedTrace);
                    markerMoveKeys.Restart();
                }

                ImGuiTheme.newLine();
                //In Case marker is enabled we show marker features
                var buttonTheme = ImGuiTheme.getButtonTheme();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.ArrowUp} Peak Search (Press Enter)";
                if (ImGuiTheme.button("peakSearch", buttonTheme) || Imports.GetAsyncKeyState(Keys.Enter))
                {
                    traces[selectedTrace].marker.position = peakSearch(selectedTrace);
                }
                ImGuiTheme.newLine();
                ImGuiTheme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.Mountain} Set Delta";
                if (ImGuiTheme.button("markerDelta", buttonTheme) || Imports.GetAsyncKeyState(Keys.Enter))
                {
                    traces[selectedTrace].marker.delta = true;
                    markerSetDelta(selectedTrace);
                }
                ImGuiTheme.newLine();
                ImGuiTheme.newLine();
                buttonTheme.text = $"{Design_imGUINET.FontAwesome5.Eraser} Clear Delta";
                if (ImGuiTheme.button("markerDelta", buttonTheme) || Imports.GetAsyncKeyState(Keys.Enter))
                {
                    traces[selectedTrace].marker.delta = false;
                }
                ImGuiTheme.newLine();
                ImGuiTheme.newLine();
                ImGui.Checkbox($"Enable Band Power", ref traces[selectedTrace].marker.bandPower);
                if (traces[selectedTrace].marker.bandPower)
                {
                    ImGuiTheme.newLine();
                    ImGui.Text($"{FontAwesome5.ArrowLeft} Span {FontAwesome5.ArrowRight}:");
                    if (ImGuiTheme.glowingInput("InputSelectortext11", ref traces[selectedTrace].marker.bandPower_Span_str, inputTheme))
                    {
                        refreshConfiguration();
                    }
                }
            }
        }

        public static void markerMoveNext(int traceID)
        {
            lock (traces[traceID].plot) //could get updateData so we gotta lock it up
            {
                KeyValuePair<float, float>[] plotData = traces[traceID].plot.ToArray();
                for (int i = 0; i < plotData.Length; i++)
                {
                    if (plotData[i].Key == traces[traceID].marker.position)
                    {
                        if (i + 1 == plotData.Length) return; //you are out of the bounderies
                        traces[traceID].marker.position = plotData[i + 1].Key;
                        return;
                    }
                }
            }
        }
        public static void markerMovePrevious(int traceID)
        {
            lock (traces[traceID].plot) //could get updateData so we gotta lock it up
            {
                KeyValuePair<float, float>[] plotData = traces[traceID].plot.ToArray();
                for (int i = 0; i < plotData.Length; i++)
                {
                    if (plotData[i].Key == traces[traceID].marker.position)
                    {
                        if (i == 0) return; //you are out of the bounderies
                        traces[traceID].marker.position = plotData[i - 1].Key;
                        return;
                    }
                }
            }
        }

        public static void markerSetDelta(int traceID)
        {
            lock (traces[traceID].plot) //could get updateData so we gotta lock it up
            {
                var nearest = traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key - traces[traceID].marker.position));
                traces[traceID].marker.DeltaFreq = nearest.Key;
                traces[traceID].marker.DeltadB = nearest.Value;
            }
        }
        public static float peakSearch(int traceID)
        {
            float maxFreq = 0;
            lock (traces[traceID].plot) //could get updateData so we gotta lock it up
            {
                maxFreq = traces[traceID].plot.MaxBy(entry => entry.Value).Key;
            }
            return maxFreq;
        }
    }
}
