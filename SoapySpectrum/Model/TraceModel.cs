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