namespace SoapySA.Model;

public enum TraceDataStatus
{
    Normal,
    Average,
    MaxHold,
    MinHold
}

public enum TraceViewStatus
{
    Active,
    Clear,
    View
}

public struct Trace
{
    public int Average;
    private TraceDataStatus _datastatus;

    public Trace()
    {
        Plot = new SortedDictionary<float, float>();
        DataStatus = TraceDataStatus.Normal;
        Average = 1;
        ViewStatus = TraceViewStatus.Clear;
    }
    public KeyValuePair<float, float> GetClosestSampledFrequency(float mhz)
    {
        lock (Plot)
        {
            return Plot.MinBy(x => Math.Abs((long)x.Key - mhz));
        }
    }
    public KeyValuePair<float, float> FindMaxHoldRange(float start,
        float stop)
    {
        var results = new KeyValuePair<float, float>(0, -1000);
        var range = Plot;
        foreach (var sample in range)
            if (sample.Value > results.Value && sample.Key >= start && sample.Key <= stop)
                results = sample;

        return results;
    }
    public TraceDataStatus DataStatus // property
    {
        get => _datastatus; // get method
        set
        {
            Average = 1;
            _datastatus = value;
            Plot.Clear();
        } // set method
    }

    public TraceViewStatus ViewStatus;
    public SortedDictionary<float, float> Plot;
}