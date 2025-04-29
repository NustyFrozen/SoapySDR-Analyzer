using NLog;

namespace SoapyRL.View.tabs;

public static class tab_Trace
{
    public enum traceViewStatus
    {
        active,
        clear,
        view
    }

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public static Trace[] s_traces = new Trace[2];

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

    public struct Trace
    {
        public Trace()
        {
            plot = new SortedDictionary<float, float>();
            viewStatus = traceViewStatus.clear;
        }

        public traceViewStatus viewStatus;
        public SortedDictionary<float, float> plot;
    }
}