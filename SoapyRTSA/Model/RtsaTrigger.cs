namespace SoapyRTSA.Model;

public enum TriggerMode
{
    /// <summary>Show the frame when any bin crosses a level.</summary>
    Magnitude,

    /// <summary>Show the frame when any bin crosses the mask drawn on the graph.</summary>
    Mask,

    /// <summary>Show the frame only inside a repeating time window.</summary>
    TimeQualified
}

/// <summary>
///     One of the three trigger slots. A frame reaches the display when no slot is armed at all, or when
///     any armed slot accepts it.
/// </summary>
public sealed class RtsaTrigger
{
    public bool Enabled { get; set; }
    public TriggerMode Mode { get; set; } = TriggerMode.Magnitude;

    /// <summary>Level for <see cref="TriggerMode.Magnitude" />.</summary>
    public double MagnitudeDb { get; set; } = -60.0;

    /// <summary>Window period for <see cref="TriggerMode.TimeQualified" />.</summary>
    public double PeriodUs { get; set; } = 500.0;

    /// <summary>Where in the period the window opens, as a percentage of it.</summary>
    public double OffsetPercent { get; set; }

    /// <summary>How long the window stays open.</summary>
    public double DurationUs { get; set; } = 50.0;

    /// <summary>Frames this slot accepted, shown in the tab so a mis-set level is obvious.</summary>
    public long Hits;

    #region editors

    //text the tab is editing, kept beside the value so a half typed number never reaches the sweep
    public string MagnitudeStr = "-60";
    public string PeriodStr = "500";
    public string DurationStr = "50";
    public float OffsetKnob;

    #endregion editors

    public void SyncEditors()
    {
        MagnitudeStr = MagnitudeDb.ToString();
        PeriodStr = PeriodUs.ToString();
        DurationStr = DurationUs.ToString();
        OffsetKnob = (float)OffsetPercent;
    }
}

/// <summary>A point of the trigger mask, in absolute frequency.</summary>
public sealed class MaskPoint
{
    public double Frequency { get; set; }
    public double Db { get; set; }
}
