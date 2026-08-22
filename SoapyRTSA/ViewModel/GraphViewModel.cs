using NLog;
using SoapyRTSA.Model;
using SoapyVNACommon.Extentions;
using Logger = NLog.Logger;

namespace SoapyRTSA.View;

/// <summary>
///     State behind the density display. Every buffer the render path needs is taken once here, so a
///     frame costs no allocations beyond the label strings ImGui itself needs.
/// </summary>
public partial class GraphView
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Configuration _config;
    private readonly PerformRtsa _sweep;

    #region canvas, recomputed every frame

    private float _left,
        _right,
        _densityTop,
        _densityBottom,
        _waterfallTop,
        _waterfallBottom;

    private float _columnWidth,
        _densityRowHeight,
        _waterfallRowHeight;

    private double _spanStart, _spanStop, _topDb, _bottomDb;

    #endregion canvas, recomputed every frame

    /// <summary>
    ///     Highest occupied row per column, in dB. This is the envelope of everything the persistence grid
    ///     still remembers, which is what the markers read: hold the display and it stops moving.
    /// </summary>
    private readonly float[] _peakDb = new float[PerformRtsa.Columns];

    public GraphView(Configuration config, PerformRtsa sweep)
    {
        _config = config;
        _sweep = sweep;
    }

    /// <summary>Columns the sweep is actually feeding. Never zero, so it is safe to divide by.</summary>
    private int ActiveColumns => _sweep.ActiveColumns < 1 ? 1 : _sweep.ActiveColumns;

    /// <summary>Column a frequency falls in, or -1 when it is off screen.</summary>
    private int ColumnOf(double frequency)
    {
        var span = _spanStop - _spanStart;
        if (span <= 0.0)
            return -1;

        var column = (int)((frequency - _spanStart) / span * ActiveColumns);
        return (uint)column < (uint)ActiveColumns ? column : -1;
    }

    private double FrequencyOf(float x)
    {
        var width = _right - _left;
        if (width <= 0.0f)
            return _spanStart;

        return _spanStart + (x - _left) / width * (_spanStop - _spanStart);
    }

    private float YofDb(double db)
    {
        var range = _topDb - _bottomDb;
        if (range <= 0.0)
            return _densityTop;

        var y = _densityTop + (float)((_topDb - db) / range) * (_densityBottom - _densityTop);
        return y < _densityTop ? _densityTop : y > _densityBottom ? _densityBottom : y;
    }

    private double DbOf(float y)
    {
        var height = _densityBottom - _densityTop;
        if (height <= 0.0f)
            return _topDb;

        return _topDb - (y - _densityTop) / height * (_topDb - _bottomDb);
    }

    /// <summary>
    ///     Finds the top occupied cell of each column. This is not drawn - it is not a trace, and showing it
    ///     would read as a max hold sitting over the persistence - but the markers need something to measure,
    ///     and since it comes out of the grid it freezes exactly when the display is held.
    /// </summary>
    private void MeasureGrid()
    {
        var grid = _sweep.Grid;

        //anything this far below an always-occupied cell is decay dust, not signal
        var floor = _sweep.DensityReference * 0.002f;
        var range = (float)(_topDb - _bottomDb);
        var rowDb = range / PerformRtsa.DensityRows;
        var columns = ActiveColumns;

        for (var column = 0; column < columns; column++)
        {
            _peakDb[column] = float.NegativeInfinity;

            for (var row = 0; row < PerformRtsa.DensityRows; row++)
            {
                if (grid.Cells[row * PerformRtsa.Columns + column] <= floor)
                    continue;

                _peakDb[column] = (float)(_topDb - (row + 0.5f) * rowDb);
                break;
            }
        }
    }

    /// <summary>
    ///     Fills in what the markers report. Band power sums the envelope across the marker's span in mW,
    ///     the same way the spectrum widget does it.
    /// </summary>
    private void MeasureMarkers()
    {
        foreach (var marker in _config.Markers)
        {
            if (!marker.IsActive)
                continue;

            var column = ColumnOf(marker.Position);
            marker.Value = column < 0 ? double.NegativeInfinity : _peakDb[column] + _config.OffsetDb;

            if (!marker.BandPower)
                continue;

            var from = ColumnOf(marker.Position - marker.BandPowerSpan / 2.0);
            var to = ColumnOf(marker.Position + marker.BandPowerSpan / 2.0);
            if (from < 0)
                from = 0;
            if (to < 0)
                to = ActiveColumns - 1;

            var milliwatts = 0.0;
            for (var i = from; i <= to; i++)
            {
                var db = _peakDb[i];
                if (!float.IsNegativeInfinity(db))
                    milliwatts += ((double)db + _config.OffsetDb).ToMw();
            }

            marker.BandPowerValue = milliwatts <= 0.0 ? double.NegativeInfinity : milliwatts.ToDBm();
        }
    }

    private RtsaMarker Selected => _config.Markers[
        _config.SelectedMarker < 0 || _config.SelectedMarker >= _config.Markers.Length
            ? 0
            : _config.SelectedMarker
    ];
}
