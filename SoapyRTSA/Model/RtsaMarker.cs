namespace SoapyRTSA.Model;

/// <summary>
///     A marker on the density display. Measurements are taken against the peak envelope of the
///     persistence grid, so holding the display freezes what the markers report.
/// </summary>
public sealed class RtsaMarker
{
    public int Id { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Absolute frequency, Hz.</summary>
    public double Position { get; set; }

    /// <summary>Last measured magnitude, dB. Filled in while rendering.</summary>
    public double Value;

    /// <summary>Marker this one is read relative to, 1 based. 0 means absolute.</summary>
    public int DeltaReference { get; set; }

    public bool BandPower { get; set; }
    public double BandPowerSpan { get; set; } = 1e6;
    public double BandPowerValue;

    public string BandPowerSpanStr = "1M";
}
