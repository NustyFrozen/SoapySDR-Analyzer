using SoapyRTSA.Extentions;

namespace SoapyRTSA.Model;

/// <summary>
///     The persistence bitmap behind the display: one counter per (column, amplitude row) that the fft
///     thread bumps and time decays. Row 0 is the top of the graph. Allocated once and never resized, so
///     the sweep never waits on the collector; the ui reads it while it is being written and the odd torn
///     value is invisible at 60 frames a second.
/// </summary>
public sealed class DensityGrid
{
    public int Columns { get; }
    public int Rows { get; }

    /// <summary>
    ///     Hit counters, row major. Written by the fft thread, read by the ui. A counter is turned into an
    ///     occupancy fraction against <see cref="PerformRtsa.DensityReference" />, never against whatever the
    ///     busiest cell happens to be.
    /// </summary>
    public readonly float[] Cells;

    public DensityGrid(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        Cells = new float[columns * rows];
    }

    public void Clear() => Array.Clear(Cells);

    /// <summary>Multiplies every counter, which is how a trace ages out of the display.</summary>
    public void Fade(float factor) => SimdOps.Scale(Cells, factor);

    /// <summary>
    ///     Decay that leaves a counter at 1% of its value after <paramref name="fadeSeconds" />, so the
    ///     fade time the operator types is what they actually see.
    /// </summary>
    public static float DecayFactor(double elapsedSeconds, double fadeSeconds)
    {
        if (fadeSeconds <= 0.0)
            return 0.0f;

        return MathF.Pow(0.01f, (float)(elapsedSeconds / fadeSeconds));
    }
}
