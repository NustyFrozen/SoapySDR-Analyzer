using ImGuiNET;

namespace SoapySpectrum.UI
{
    public static class tab_Trace
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();


        public static string[] Combotraces = new string[] { $"Trace 1", $"Trace 2", $"Trace 3", $"Trace 4", $"Trace 5", $"Trace 6" };
        public static trace[] traces = new trace[6];

        public static KeyValuePair<float, float> getClosestSampeledFrequency(int traceID, float Mhz)
        {
            lock (traces[traceID].plot)
                return traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key - Mhz));
        }
        public static float getFloordB(int traceID)
        {
            lock (traces[traceID].plot)
                return traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key)).Value;
        }
        public static KeyValuePair<float, float> findMaxHoldRange(SortedDictionary<float, float> table, float start, float stop)
        {
            KeyValuePair<float, float> results = new KeyValuePair<float, float>(0, -1000);
            var range = table.ToList();
            foreach (KeyValuePair<float, float> sample in range)
                if (sample.Value > results.Value && sample.Key >= start && sample.Key <= stop)
                    results = sample;

            return results;
        }
        public static void renderTrace()
        {
            var inputTheme = Theme.getTextTheme();
            inputTheme.prefix = "RBW";
            Theme.glowingCombo("InputSelectortext3", ref Global.selectedTrace, Combotraces, inputTheme);
            Theme.newLine();
            Theme.Text($"{Design_imGUINET.FontAwesome5.Eye} View", inputTheme);
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.PersonRunning} Active", traces[Global.selectedTrace].viewStatus == traceViewStatus.active))
                traces[Global.selectedTrace].viewStatus = traceViewStatus.active;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Eye} View", traces[Global.selectedTrace].viewStatus == traceViewStatus.view))
                traces[Global.selectedTrace].viewStatus = traceViewStatus.view;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Eraser} Clear", traces[Global.selectedTrace].viewStatus == traceViewStatus.clear))
                traces[Global.selectedTrace].viewStatus = traceViewStatus.clear;

            Theme.newLine();
            Theme.newLine();
            Theme.Text($"{Design_imGUINET.FontAwesome5.StreetView} Trace Function", inputTheme);
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Equal} Normal", traces[Global.selectedTrace].dataStatus == traceDataStatus.normal))
                traces[Global.selectedTrace].dataStatus = traceDataStatus.normal;
            if (ImGui.RadioButton($"\ue4c2 Max Hold", traces[Global.selectedTrace].dataStatus == traceDataStatus.maxHold))
                traces[Global.selectedTrace].dataStatus = traceDataStatus.maxHold;
            if (ImGui.RadioButton($"\ue4b8 Min Hold", traces[Global.selectedTrace].dataStatus == traceDataStatus.minHold))
                traces[Global.selectedTrace].dataStatus = traceDataStatus.minHold;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Microscope} Average", traces[Global.selectedTrace].dataStatus == traceDataStatus.Average))
                traces[Global.selectedTrace].dataStatus = traceDataStatus.Average;
        }


    }
}
