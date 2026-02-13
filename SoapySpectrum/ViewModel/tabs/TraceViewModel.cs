using NLog;
using SoapySA.Model;

namespace SoapySA.View.tabs;

public partial class TraceView 
{
    public override string tabName => "\uf3c5 Trace";
    public static string[] SComboTraces = new[] { "Trace 1", "Trace 2", "Trace 3", "Trace 4", "Trace 5", "Trace 6" };
    
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private MainWindowView _parent = initiator;
    public int SSelectedTrace;
    public Trace[] STraces = new Trace[6];

    public KeyValuePair<float, float> GetClosestSampledFrequency(int traceId, float mhz)
    {
        lock (STraces[traceId].Plot)
        {
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

    public void DisableAllTraces()
    {
        for (int i = 0; i < STraces.Length; i++) 
            STraces[i].ViewStatus = TraceViewStatus.Clear;
        
    }
}