using NLog;
using SoapyRL.Extentions;

namespace SoapyRL.View.tabs;

public class tab_Trace(MainWindow initiator)
{
    public MainWindow parent = initiator;

    public enum traceViewStatus
    {
        active,
        clear,
        view
    }

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public Trace[] s_traces = new Trace[3];

    public KeyValuePair<float, float> getClosestSampeledFrequency(int traceID, float Mhz)
    {
        lock (s_traces[traceID].plot)
        {
            if (s_traces[traceID].plot.Count == 0) return new KeyValuePair<float, float>(0, 0);
            return s_traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key - Mhz));
        }
    }

    public KeyValuePair<float, float> findMaxHoldRange(SortedDictionary<float, float> table, float start,
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

        private uint m_color, m_liteColor;

        public uint color
        {
            get
            {
                return m_color;
            }
            set
            {
                m_color = value;
                m_liteColor = Color.FromArgb(100, m_color.toColor()).ToUint();
            }
        }

        public readonly uint liteColor
        {
            get
            {
                return m_liteColor;
            }
        }

        public traceViewStatus viewStatus;
        public SortedDictionary<float, float> plot;
    }
}