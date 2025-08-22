namespace SoapySA.Model;

public struct Marker
{
    public Marker()
    {
    }

    public int Id, Reference;
    public string TxtStatus;
    public bool IsActive;
    public double Position, Value;

    public int DeltaReference;
    public bool Delta;
    public double DeltaFreq, DeltadB;

    public bool BandPower;
    public double BandPowerSpan = 5e6, BandPowerValue;
    public string BandPowerSpanStr = "5M";
}