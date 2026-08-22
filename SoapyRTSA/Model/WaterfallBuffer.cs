using SoapyRTSA.Extentions;

namespace SoapyRTSA.Model;

/// <summary>
///     Rolling history under the density display, stored as one palette index per cell so a row costs a
///     byte per column and a push is a single fill. Circular: nothing moves, only the head does.
/// </summary>
public sealed class WaterfallBuffer
{
    public int Columns { get; }
    public int Rows { get; }

    private readonly byte[] _cells;
    private int _head; //row that will be written next

    public WaterfallBuffer(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        _cells = new byte[columns * rows];
    }

    public void Clear()
    {
        Array.Clear(_cells);
        _head = 0;
    }

    /// <summary>Row by age, 0 being the one pushed most recently.</summary>
    public ReadOnlySpan<byte> Row(int age)
    {
        var row = _head - 1 - age;
        while (row < 0)
            row += Rows;

        return _cells.AsSpan(row * Columns, Columns);
    }

    /// <summary>
    ///     Adds a row of dB, quantised onto the colour ramp between the display's floor and ceiling.
    ///     Columns that saw nothing since the last push land on step 0 and draw as background.
    /// </summary>
    public void Push(ReadOnlySpan<float> columnDb, float bottomDb, float topDb)
    {
        var target = _cells.AsSpan(_head * Columns, Columns);
        var range = topDb - bottomDb;
        var scale = range <= 0.0f ? 0.0f : 1.0f / range;

        var fed = Math.Min(Columns, columnDb.Length);
        for (var column = 0; column < fed; column++)
        {
            var db = columnDb[column];
            target[column] = float.IsNegativeInfinity(db)
                ? (byte)0
                : (byte)Palette.Step((db - bottomDb) * scale);
        }

        //columns the transform is not feeding must not keep whatever was in this row last time round
        if (fed < Columns)
            target.Slice(fed).Clear();

        _head++;
        if (_head >= Rows)
            _head = 0;
    }
}
