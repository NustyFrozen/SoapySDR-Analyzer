using System.Numerics;

namespace SoapyRTSA.Extentions;

/// <summary>
///     Radix 2 Cooley-Tukey over split real/imaginary buffers. Everything it needs - the bit reversal
///     order and the per stage twiddles - is built once in the constructor, so a transform allocates
///     nothing and always costs the same. Split buffers are what make the butterflies vectorisable:
///     both operands of a stage are contiguous runs of floats.
/// </summary>
public sealed class Fft
{
    private static readonly int Width = Vector<float>.Count;

    private readonly int[] _reversed;

    /// <summary>Twiddles per stage, indexed [stage][j], laid out contiguously for vector loads.</summary>
    private readonly float[][] _cos;
    private readonly float[][] _sin;

    public int Size { get; }

    public Fft(int size)
    {
        if (size < 4 || (size & (size - 1)) != 0)
            throw new ArgumentException($"fft size {size} is not a power of two", nameof(size));

        Size = size;

        _reversed = new int[size];
        var bits = BitOperations.TrailingZeroCount(size);
        for (var i = 0; i < size; i++)
            _reversed[i] = (int)(Reverse((uint)i) >> (32 - bits));

        //one stage per doubling: lengths 2, 4, ... size
        _cos = new float[bits][];
        _sin = new float[bits][];

        var stage = 0;
        for (var length = 2; length <= size; length <<= 1, stage++)
        {
            var half = length >> 1;
            _cos[stage] = new float[half];
            _sin[stage] = new float[half];

            for (var j = 0; j < half; j++)
            {
                var angle = -2.0 * Math.PI * j / length;
                _cos[stage][j] = (float)Math.Cos(angle);
                _sin[stage][j] = (float)Math.Sin(angle);
            }
        }
    }

    private static uint Reverse(uint value)
    {
        value = ((value & 0xAAAAAAAA) >> 1) | ((value & 0x55555555) << 1);
        value = ((value & 0xCCCCCCCC) >> 2) | ((value & 0x33333333) << 2);
        value = ((value & 0xF0F0F0F0) >> 4) | ((value & 0x0F0F0F0F) << 4);
        value = ((value & 0xFF00FF00) >> 8) | ((value & 0x00FF00FF) << 8);
        return (value >> 16) | (value << 16);
    }

    /// <summary>Forward transform, in place. Both spans must be <see cref="Size" /> long.</summary>
    public void Forward(Span<float> real, Span<float> imaginary)
    {
        if (real.Length < Size || imaginary.Length < Size)
            throw new ArgumentException($"fft needs {Size} samples");

        //reorder in place: only swap one way round, otherwise every pair swaps back
        for (var i = 0; i < Size; i++)
        {
            var j = _reversed[i];
            if (j <= i)
                continue;

            (real[i], real[j]) = (real[j], real[i]);
            (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
        }

        var stage = 0;
        for (var length = 2; length <= Size; length <<= 1, stage++)
        {
            var half = length >> 1;
            var cos = _cos[stage];
            var sin = _sin[stage];

            for (var block = 0; block < Size; block += length)
            {
                var top = block;
                var bottom = block + half;
                var j = 0;

                if (half >= Width)
                    for (; j <= half - Width; j += Width)
                    {
                        var wr = new Vector<float>(cos.AsSpan(j, Width));
                        var wi = new Vector<float>(sin.AsSpan(j, Width));

                        var br = new Vector<float>(real.Slice(bottom + j, Width));
                        var bi = new Vector<float>(imaginary.Slice(bottom + j, Width));

                        var tr = br * wr - bi * wi;
                        var ti = br * wi + bi * wr;

                        var ar = new Vector<float>(real.Slice(top + j, Width));
                        var ai = new Vector<float>(imaginary.Slice(top + j, Width));

                        (ar - tr).CopyTo(real.Slice(bottom + j, Width));
                        (ai - ti).CopyTo(imaginary.Slice(bottom + j, Width));
                        (ar + tr).CopyTo(real.Slice(top + j, Width));
                        (ai + ti).CopyTo(imaginary.Slice(top + j, Width));
                    }

                //the first stages are narrower than a vector
                for (; j < half; j++)
                {
                    var a = top + j;
                    var b = bottom + j;

                    var tr = real[b] * cos[j] - imaginary[b] * sin[j];
                    var ti = real[b] * sin[j] + imaginary[b] * cos[j];

                    real[b] = real[a] - tr;
                    imaginary[b] = imaginary[a] - ti;
                    real[a] += tr;
                    imaginary[a] += ti;
                }
            }
        }
    }
}
