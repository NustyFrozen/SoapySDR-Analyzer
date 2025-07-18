using NLog;
using SoapyRL.Extentions;

namespace SoapyRL.View.tabs;

public class TabTrace(MainWindow initiator)
{
    public enum TraceViewStatus
    {
        Active,
        Clear,
        View
    }

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public MainWindow Parent = initiator;
    public Trace[] STraces = new Trace[3];

    public KeyValuePair<float, float> GetClosestSampeledFrequency(int traceId, float mhz)
    {
        lock (STraces[traceId].Plot)
        {
            if (STraces[traceId].Plot.Count == 0) return new KeyValuePair<float, float>(0, 0);
            return STraces[traceId].Plot.MinBy(x => Math.Abs((long)x.Key - mhz));
        }
    }

    public KeyValuePair<float, float> FindMaxHoldRange(SortedDictionary<float, float> table, float start,
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
            Plot = new SortedDictionary<float, float>();
            ViewStatus = TraceViewStatus.Clear;
        }

        private uint _mColor;

        public uint Color
        {
            get => _mColor;
            set
            {
                _mColor = value;
                LiteColor = System.Drawing.Color.FromArgb(100, _mColor.ToColor()).ToUint();
            }
        }

        public uint LiteColor { get; private set; }

        public TraceViewStatus ViewStatus;
        public SortedDictionary<float, float> Plot;
    }
}