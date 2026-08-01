using System.Numerics;

namespace SoapyRTSA.Extentions;

/// <summary>
///     The vectorised inner loops of the analyser. Every method works in place over caller owned spans:
///     nothing here allocates, so the sweep threads never hand the collector any work.
/// </summary>
public static class SimdOps
{
    /// <summary>10 / ln(2) * ln(2) folded down: dB = 10*log10(p) = 3.0103 * log2(p).</summary>
    private const float DbPerLog2 = 3.01029995664f;

    /// <summary>Power below this is treated as this, so an empty bin gives about -300 dB instead of -inf.</summary>
    private const float PowerFloor = 1e-30f;

    private static readonly int Width = Vector<float>.Count;

    /// <summary>destination[i] *= source[i], used to apply the fft window.</summary>
    public static void Multiply(Span<float> destination, ReadOnlySpan<float> source)
    {
        var i = 0;
        for (; i <= destination.Length - Width; i += Width)
        {
            var value = new Vector<float>(destination.Slice(i, Width)) * new Vector<float>(source.Slice(i, Width));
            value.CopyTo(destination.Slice(i, Width));
        }

        for (; i < destination.Length; i++)
            destination[i] *= source[i];
    }

    /// <summary>float -> double, for the double precision FFTW path.</summary>
    public static void Widen(ReadOnlySpan<float> source, Span<double> destination)
    {
        var i = 0;
        for (; i <= source.Length - Width; i += Width)
        {
            Vector.Widen(new Vector<float>(source.Slice(i, Width)), out var low, out var high);
            low.CopyTo(destination.Slice(i, Vector<double>.Count));
            high.CopyTo(destination.Slice(i + Vector<double>.Count, Vector<double>.Count));
        }

        for (; i < source.Length; i++)
            destination[i] = source[i];
    }

    /// <summary>double -> float, for the double precision FFTW path.</summary>
    public static void Narrow(ReadOnlySpan<double> source, Span<float> destination)
    {
        var lanes = Vector<double>.Count;
        var i = 0;
        for (; i <= source.Length - Width; i += Width)
        {
            var narrowed = Vector.Narrow(
                new Vector<double>(source.Slice(i, lanes)),
                new Vector<double>(source.Slice(i + lanes, lanes))
            );
            narrowed.CopyTo(destination.Slice(i, Width));
        }

        for (; i < source.Length; i++)
            destination[i] = (float)source[i];
    }

    /// <summary>destination[i] *= factor, which is how the persistence grid fades.</summary>
    public static void Scale(Span<float> destination, float factor)
    {
        var scale = new Vector<float>(factor);
        var i = 0;
        for (; i <= destination.Length - Width; i += Width)
        {
            var value = new Vector<float>(destination.Slice(i, Width)) * scale;
            value.CopyTo(destination.Slice(i, Width));
        }

        for (; i < destination.Length; i++)
            destination[i] *= factor;
    }

    /// <summary>Largest value in the span, used to normalise the grid before it is coloured.</summary>
    public static float Max(ReadOnlySpan<float> source)
    {
        var i = 0;
        var peak = float.NegativeInfinity;

        if (source.Length >= Width)
        {
            var peaks = new Vector<float>(source.Slice(0, Width));
            for (i = Width; i <= source.Length - Width; i += Width)
                peaks = Vector.Max(peaks, new Vector<float>(source.Slice(i, Width)));

            for (var lane = 0; lane < Width; lane++)
                if (peaks[lane] > peak)
                    peak = peaks[lane];
        }

        for (; i < source.Length; i++)
            if (source[i] > peak)
                peak = source[i];

        return peak;
    }

    /// <summary>
    ///     Turns a split complex spectrum into dB, in place over <paramref name="destination" />:
    ///     10*log10((re^2 + im^2) * scale).
    /// </summary>
    public static void PowerToDb(
        ReadOnlySpan<float> real,
        ReadOnlySpan<float> imaginary,
        Span<float> destination,
        float scale
    )
    {
        var gain = new Vector<float>(scale);
        var floor = new Vector<float>(PowerFloor);
        var perLog2 = new Vector<float>(DbPerLog2);

        var i = 0;
        for (; i <= destination.Length - Width; i += Width)
        {
            var re = new Vector<float>(real.Slice(i, Width));
            var im = new Vector<float>(imaginary.Slice(i, Width));
            var power = Vector.Max(re * re + im * im, floor) * gain;

            (Log2(power) * perLog2).CopyTo(destination.Slice(i, Width));
        }

        for (; i < destination.Length; i++)
        {
            var power = MathF.Max(real[i] * real[i] + imaginary[i] * imaginary[i], PowerFloor) * scale;
            destination[i] = 10.0f * MathF.Log10(power);
        }
    }

    /// <summary>
    ///     log2 for positive lanes. The exponent comes straight out of the float bits and the mantissa is
    ///     folded into [1/sqrt2, sqrt2) where the atanh series converges in three terms, which lands well
    ///     inside a thousandth of a dB.
    /// </summary>
    private static Vector<float> Log2(Vector<float> x)
    {
        var bits = Vector.AsVectorInt32(x);

        //IEEE754: exponent in bits 23..30 biased by 127, mantissa forced into [1,2)
        var exponent = Vector.ConvertToSingle(Vector.ShiftRightArithmetic(bits, 23) - new Vector<int>(127));
        var mantissa = Vector.AsVectorSingle(
            (bits & new Vector<int>(0x007FFFFF)) | new Vector<int>(0x3F800000)
        );

        //halve the upper half of the octave so the series sees |u| <= 0.172 instead of 0.333
        var high = Vector.GreaterThan(mantissa, new Vector<float>(1.41421356f));
        mantissa = Vector.ConditionalSelect(high, mantissa * new Vector<float>(0.5f), mantissa);
        exponent += Vector.ConditionalSelect(high, Vector<float>.One, Vector<float>.Zero);

        //ln(m) = 2*(u + u^3/3 + u^5/5), u = (m-1)/(m+1)
        var u = (mantissa - Vector<float>.One) / (mantissa + Vector<float>.One);
        var u2 = u * u;
        var ln = u * (new Vector<float>(2.0f) + u2 * (new Vector<float>(0.666666667f) + u2 * new Vector<float>(0.4f)));

        return exponent + ln * new Vector<float>(1.44269504089f);
    }
}
