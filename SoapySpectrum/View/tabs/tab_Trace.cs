using ImGuiNET;
using NLog;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public static class tab_Trace
{
    public enum traceDataStatus
    {
        normal,
        Average,
        maxHold,
        minHold
    }

    public enum traceViewStatus
    {
        active,
        clear,
        view
    }

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static int s_selectedTrace;
    public static string[] s_comboTraces = new[] { "Trace 1", "Trace 2", "Trace 3", "Trace 4", "Trace 5", "Trace 6" };
    public static Trace[] s_traces = new Trace[6];

    public static KeyValuePair<float, float> getClosestSampeledFrequency(int traceID, float Mhz)
    {
        lock (s_traces[traceID].plot)
        {
            return s_traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key - Mhz));
        }
    }

    public static KeyValuePair<float, float> findMaxHoldRange(SortedDictionary<float, float> table, float start,
        float stop)
    {
        var results = new KeyValuePair<float, float>(0, -1000);
        var range = table.ToList();
        foreach (var sample in range)
            if (sample.Value > results.Value && sample.Key >= start && sample.Key <= stop)
                results = sample;

        return results;
    }

    public static void renderTrace()
    {
        Theme.inputTheme.prefix = "RBW";
        Theme.glowingCombo("InputSelectortext3", ref s_selectedTrace, s_comboTraces, Theme.inputTheme);
        Theme.newLine();
        Theme.Text($"{FontAwesome5.Eye} View", Theme.inputTheme);
        if (ImGui.RadioButton($"{FontAwesome5.PersonRunning} Active",
                s_traces[s_selectedTrace].viewStatus == traceViewStatus.active))
            s_traces[s_selectedTrace].viewStatus = traceViewStatus.active;
        if (ImGui.RadioButton($"{FontAwesome5.Eye} View", s_traces[s_selectedTrace].viewStatus == traceViewStatus.view))
            s_traces[s_selectedTrace].viewStatus = traceViewStatus.view;
        if (ImGui.RadioButton($"{FontAwesome5.Eraser} Clear",
                s_traces[s_selectedTrace].viewStatus == traceViewStatus.clear))
            s_traces[s_selectedTrace].viewStatus = traceViewStatus.clear;

        Theme.newLine();
        Theme.newLine();
        Theme.Text($"{FontAwesome5.StreetView} Trace Function", Theme.inputTheme);
        if (ImGui.RadioButton($"{FontAwesome5.Equal} Normal",
                s_traces[s_selectedTrace].dataStatus == traceDataStatus.normal))
            s_traces[s_selectedTrace].dataStatus = traceDataStatus.normal;
        if (ImGui.RadioButton("\ue4c2 Max Hold", s_traces[s_selectedTrace].dataStatus == traceDataStatus.maxHold))
            s_traces[s_selectedTrace].dataStatus = traceDataStatus.maxHold;
        if (ImGui.RadioButton("\ue4b8 Min Hold", s_traces[s_selectedTrace].dataStatus == traceDataStatus.minHold))
            s_traces[s_selectedTrace].dataStatus = traceDataStatus.minHold;
        if (ImGui.RadioButton($"{FontAwesome5.Microscope} Average",
                s_traces[s_selectedTrace].dataStatus == traceDataStatus.Average))
            s_traces[s_selectedTrace].dataStatus = traceDataStatus.Average;
    }

    public struct Trace
    {
        public int average;
        private traceDataStatus datastatus;

        public Trace()
        {
            plot = new SortedDictionary<float, float>();
            dataStatus = traceDataStatus.normal;
            average = 1;
            viewStatus = traceViewStatus.clear;
        }

        public traceDataStatus dataStatus // property
        {
            get => datastatus; // get method
            set
            {
                average = 1;
                datastatus = value;
                plot.Clear();
            } // set method
        }

        public traceViewStatus viewStatus;
        public SortedDictionary<float, float> plot;
    }
}