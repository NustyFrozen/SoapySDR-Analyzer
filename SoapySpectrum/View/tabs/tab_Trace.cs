using ImGuiNET;

namespace SoapySpectrum.UI
{
    public static class tab_Trace
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static int selectedTrace = 0;
        public static string[] Combotraces = new string[] { $"Trace 1", $"Trace 2", $"Trace 3", $"Trace 4", $"Trace 5", $"Trace 6" };
        public static trace[] traces = new trace[6];
        public enum traceViewStatus
        {
            active, clear, view
        }
        public enum traceDataStatus
        {
            normal, Average, maxHold, minHold
        }
        public struct trace
        {
            public int average;
            private traceDataStatus datastatus;
            public trace()
            {
                plot = new SortedDictionary<float, float>();
                dataStatus = traceDataStatus.normal;
                average = 1;
                viewStatus = traceViewStatus.clear;
            }
            public traceDataStatus dataStatus   // property
            {
                get
                {
                    return datastatus;
                }   // get method
                set
                {
                    average = 1;
                    datastatus = value;
                    plot.Clear();
                }  // set method
            }
            public traceViewStatus viewStatus;
            public SortedDictionary<float, float> plot;
        }
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
            Theme.glowingCombo("InputSelectortext3", ref selectedTrace, Combotraces, inputTheme);
            Theme.newLine();
            Theme.Text($"{Design_imGUINET.FontAwesome5.Eye} View", inputTheme);
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.PersonRunning} Active", traces[selectedTrace].viewStatus == traceViewStatus.active))
                traces[selectedTrace].viewStatus = traceViewStatus.active;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Eye} View", traces[selectedTrace].viewStatus == traceViewStatus.view))
                traces[selectedTrace].viewStatus = traceViewStatus.view;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Eraser} Clear", traces[selectedTrace].viewStatus == traceViewStatus.clear))
                traces[selectedTrace].viewStatus = traceViewStatus.clear;

            Theme.newLine();
            Theme.newLine();
            Theme.Text($"{Design_imGUINET.FontAwesome5.StreetView} Trace Function", inputTheme);
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Equal} Normal", traces[selectedTrace].dataStatus == traceDataStatus.normal))
                traces[selectedTrace].dataStatus = traceDataStatus.normal;
            if (ImGui.RadioButton($"\ue4c2 Max Hold", traces[selectedTrace].dataStatus == traceDataStatus.maxHold))
                traces[selectedTrace].dataStatus = traceDataStatus.maxHold;
            if (ImGui.RadioButton($"\ue4b8 Min Hold", traces[selectedTrace].dataStatus == traceDataStatus.minHold))
                traces[selectedTrace].dataStatus = traceDataStatus.minHold;
            if (ImGui.RadioButton($"{Design_imGUINET.FontAwesome5.Microscope} Average", traces[selectedTrace].dataStatus == traceDataStatus.Average))
                traces[selectedTrace].dataStatus = traceDataStatus.Average;
        }


    }
}
