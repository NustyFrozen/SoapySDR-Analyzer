using System.Drawing;
using System.Numerics;
using ImGuiNET;
using SoapyRTSA.Extentions;
using SoapyRTSA.Model;
using SoapyVNACommon.Extentions;
using UserScreenConfiguration = SoapySA.Extentions.UserScreenConfiguration;

namespace SoapyRTSA.View;

public partial class GraphView
{
    /// <summary>Share of the plot given to the density display when the waterfall is showing.</summary>
    private const float DensityShare = 0.62f;

    private const float WaterfallShare = 0.30f;

    private static readonly uint GridLineColor = Color.FromArgb(90, Color.Gray).ToUint();
    private static readonly uint LabelColor = Color.LightGray.ToUint();
    private static readonly uint MaskColor = Color.FromArgb(220, 255, 0, 200).ToUint();

    public void Draw()
    {
        var windowPos = ImGui.GetWindowPos();
        var draw = ImGui.GetForegroundDrawList();
        var size = UserScreenConfiguration.GraphSize;

        #region canvas

        _left = windowPos.X + UserScreenConfiguration.PositionOffset.X;
        _right = _left + size.X;
        _densityTop = windowPos.Y + UserScreenConfiguration.PositionOffset.Y;

        var labelGap = ImGui.GetTextLineHeight() + UserScreenConfiguration.PercentUniform(UserScreenConfiguration.PaddingPct);

        if (_config.ShowWaterfall)
        {
            _densityBottom = _densityTop + size.Y * DensityShare;
            _waterfallTop = _densityBottom + labelGap;
            _waterfallBottom = _waterfallTop + size.Y * WaterfallShare;
        }
        else
        {
            _densityBottom = _densityTop + size.Y - labelGap;
            _waterfallTop = _waterfallBottom = _densityBottom;
        }

        //the fed columns are stretched over the full width, so a partly filled grid still fills the plot
        _columnWidth = (_right - _left) / ActiveColumns;
        _densityRowHeight = (_densityBottom - _densityTop) / PerformRtsa.DensityRows;
        _waterfallRowHeight = (_waterfallBottom - _waterfallTop) / PerformRtsa.WaterfallRows;

        //exactly what the columns cover, which is a whole number of bins and not the requested span
        _spanStart = _sweep.DisplayStartHz;
        _spanStop = _sweep.DisplayStopHz;
        if (_spanStop <= _spanStart)
        {
            _spanStart = _config.CenterFrequency - _sweep.EffectiveSpan / 2.0;
            _spanStop = _config.CenterFrequency + _sweep.EffectiveSpan / 2.0;
        }

        _topDb = _config.DisplayTopDb;
        _bottomDb = _config.DisplayBottomDb;

        #endregion canvas

        draw.AddRectFilled(new Vector2(_left, _densityTop), new Vector2(_right, _densityBottom),
            Color.FromArgb(10, 10, 14).ToUint());

        if (_config.ShowWaterfall)
            draw.AddRectFilled(new Vector2(_left, _waterfallTop), new Vector2(_right, _waterfallBottom),
                Color.FromArgb(6, 6, 10).ToUint());

        MeasureGrid();
        MeasureMarkers();

        DrawDensity(draw);
        if (_config.ShowWaterfall)
            DrawWaterfall(draw);

        DrawAxes(draw);
        DrawMask(draw);
        HandleMouse(draw);
        DrawMarkers(draw);
        DrawStatus(draw);
    }

    /// <summary>
    ///     The persistence display. Each column is walked top down and only the runs where the colour
    ///     actually changes become rectangles, which turns a quarter of a million cells into a few thousand
    ///     draw commands and keeps the whole thing inside one vertex buffer.
    ///     <para>
    ///         Colour comes from how much of the fade window a cell was occupied for, measured against a
    ///         fixed reference rather than against the busiest cell on screen. That is what lets a burst show
    ///         up at all, and what makes a stale hit cool down the ramp and grow transparent instead of
    ///         staying saturated until it disappears.
    ///     </para>
    /// </summary>
    private void DrawDensity(ImDrawListPtr draw)
    {
        var grid = _sweep.Grid;
        var normalise = 1.0f / _sweep.DensityReference;
        var exponent = _config.DensityExponent;
        var columns = ActiveColumns;

        //below a millionth of a frame's worth there is nothing left but decay dust
        const float dust = 1e-6f;

        for (var column = 0; column < columns; column++)
        {
            var x = _left + column * _columnWidth;
            var runStep = 0;
            var runStart = 0;

            for (var row = 0; row <= PerformRtsa.DensityRows; row++)
            {
                var step = 0;
                if (row < PerformRtsa.DensityRows)
                {
                    var occupancy = grid.Cells[row * PerformRtsa.Columns + column] * normalise;
                    if (occupancy > dust)
                        step = Palette.Step(MathF.Pow(occupancy > 1.0f ? 1.0f : occupancy, exponent));
                }

                if (step == runStep)
                    continue;

                if (runStep > 0)
                    draw.AddRectFilled(
                        new Vector2(x, _densityTop + runStart * _densityRowHeight),
                        new Vector2(x + _columnWidth, _densityTop + row * _densityRowHeight),
                        Palette.Fade(runStep)
                    );

                runStep = step;
                runStart = row;
            }
        }
    }

    /// <summary>History under the plot, newest row on top and aligned to the same columns.</summary>
    private void DrawWaterfall(ImDrawListPtr draw)
    {
        var waterfall = _sweep.Waterfall;
        var columns = ActiveColumns;

        for (var age = 0; age < PerformRtsa.WaterfallRows; age++)
        {
            var row = waterfall.Row(age);
            var y = _waterfallTop + age * _waterfallRowHeight;
            var runStep = 0;
            var runStart = 0;

            for (var column = 0; column <= columns; column++)
            {
                var step = column < columns ? row[column] : 0;

                if (step == runStep)
                    continue;

                if (runStep > 0)
                    draw.AddRectFilled(
                        new Vector2(_left + runStart * _columnWidth, y),
                        new Vector2(_left + column * _columnWidth, y + _waterfallRowHeight),
                        Palette.Opaque(runStep)
                    );

                runStep = step;
                runStart = column;
            }
        }
    }

    private void DrawAxes(ImDrawListPtr draw)
    {
        var divisions = Math.Clamp(_config.ScalePerDivision, 4, 20);

        for (var i = 0; i <= divisions; i++)
        {
            var t = i / (float)divisions;

            //amplitude, labelled down the left of the density plot
            var y = _densityTop + t * (_densityBottom - _densityTop);
            var db = (_topDb - t * (_topDb - _bottomDb) + _config.OffsetDb).ToString("0.#");
            var dbSize = ImGui.CalcTextSize(db);
            draw.AddText(new Vector2(_left - dbSize.X, y - dbSize.Y / 2.0f), LabelColor, db);
            draw.AddLine(new Vector2(_left, y), new Vector2(_right, y), GridLineColor);

            //frequency, shared by both plots
            var x = _left + t * (_right - _left);
            var frequency = $"{(_spanStart + t * (_spanStop - _spanStart)) / 1e6:0.###}M";
            var frequencySize = ImGui.CalcTextSize(frequency);
            draw.AddText(new Vector2(x - frequencySize.X / 2.0f, _densityBottom), LabelColor, frequency);
            draw.AddLine(new Vector2(x, _densityTop), new Vector2(x, _densityBottom), GridLineColor);

            if (_config.ShowWaterfall)
                draw.AddLine(new Vector2(x, _waterfallTop), new Vector2(x, _waterfallBottom),
                    Color.FromArgb(40, Color.Gray).ToUint());
        }
    }

    private void DrawMask(ImDrawListPtr draw)
    {
        var mask = _config.Mask;
        if (mask.Count == 0)
            return;

        for (var i = 0; i < mask.Count; i++)
        {
            var point = new Vector2((float)ColumnX(mask[i].Frequency), YofDb(mask[i].Db));
            draw.AddCircleFilled(point, UserScreenConfiguration.ScaleUniform(3), MaskColor);

            if (i > 0)
                draw.AddLine(
                    new Vector2((float)ColumnX(mask[i - 1].Frequency), YofDb(mask[i - 1].Db)),
                    point,
                    MaskColor,
                    UserScreenConfiguration.ScaleUniform(2)
                );
        }
    }

    private double ColumnX(double frequency)
    {
        var span = _spanStop - _spanStart;
        if (span <= 0.0)
            return _left;

        return _left + (frequency - _spanStart) / span * (_right - _left);
    }

    /// <summary>
    ///     Clicks land either on the mask, while it is being edited, or on the selected marker. A readout
    ///     follows the cursor either way.
    /// </summary>
    private void HandleMouse(ImDrawListPtr draw)
    {
        var mouse = ImGui.GetMousePos();
        var overDensity = mouse.X >= _left && mouse.X <= _right && mouse.Y >= _densityTop && mouse.Y <= _densityBottom;
        if (!overDensity)
            return;

        draw.AddLine(new Vector2(_left, mouse.Y), new Vector2(_right, mouse.Y), Color.FromArgb(70, 120, 120, 120).ToUint());
        draw.AddLine(new Vector2(mouse.X, _densityTop), new Vector2(mouse.X, _densityBottom),
            Color.FromArgb(70, 120, 120, 120).ToUint());

        var frequency = FrequencyOf(mouse.X);
        var db = DbOf(mouse.Y) + _config.OffsetDb;
        var gap = UserScreenConfiguration.PercentUniform(UserScreenConfiguration.PaddingPct);
        draw.AddText(new Vector2(mouse.X + gap, mouse.Y + gap), Color.FromArgb(160, 160, 160).ToUint(),
            $"{frequency / 1e6:0.###}M\n{db:0.#} dB");

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            return;

        if (_config.MaskEditing)
        {
            _config.AddMaskPoint(frequency, DbOf(mouse.Y));
            return;
        }

        var marker = Selected;
        marker.IsActive = true;
        marker.Position = frequency;
    }

    private void DrawMarkers(ImDrawListPtr draw)
    {
        var radius = UserScreenConfiguration.ScaleUniform(5);
        var offsetY = 0.0f;

        foreach (var marker in _config.Markers)
        {
            if (!marker.IsActive)
                continue;

            var isSelected = marker.Id == _config.SelectedMarker;
            var color = isSelected ? Color.Yellow.ToUint() : Color.FromArgb(180, Color.White).ToUint();
            var x = (float)ColumnX(marker.Position);

            if (!double.IsNegativeInfinity(marker.Value))
            {
                var point = new Vector2(x, YofDb(marker.Value - _config.OffsetDb));
                draw.AddCircleFilled(point, radius, color);
                draw.AddCircle(point, radius * 1.4f, Color.Black.ToUint());
            }

            draw.AddLine(new Vector2(x, _densityTop), new Vector2(x, _densityBottom),
                Color.FromArgb(60, 255, 255, 0).ToUint());

            var text = MarkerText(marker);
            var textSize = ImGui.CalcTextSize(text);
            draw.AddText(new Vector2(_right - textSize.X, _densityTop + offsetY), color, text);
            offsetY += textSize.Y + UserScreenConfiguration.PercentUniform(UserScreenConfiguration.PaddingPct);
        }
    }

    private string MarkerText(RtsaMarker marker)
    {
        var value = double.IsNegativeInfinity(marker.Value) ? "--" : marker.Value.ToString("0.##");
        var text = $"M{marker.Id + 1} {marker.Position / 1e6:0.###}M  {value} dB";

        var reference = marker.DeltaReference - 1;
        if (reference >= 0 && reference < _config.Markers.Length && reference != marker.Id)
        {
            var other = _config.Markers[reference];
            text += $"\n  d{reference + 1} {(marker.Position - other.Position) / 1e6:0.###}M "
                + $"{marker.Value - other.Value:0.##} dB";
        }

        if (marker.BandPower)
            text += $"\n  band {(double.IsNegativeInfinity(marker.BandPowerValue) ? "--" : marker.BandPowerValue.ToString("0.##"))} dBm";

        return text;
    }

    private void DrawStatus(ImDrawListPtr draw)
    {
        var gap = UserScreenConfiguration.PercentUniform(UserScreenConfiguration.PaddingPct);
        var text = $"{_sweep.FramesPerSecond / 1000.0:0.0}k FFT/s  {_sweep.SweepMegasamplesPerSecond:0.##} MS/s"
            + $"  drops {_sweep.Dropped}  overruns {_sweep.Overruns}";

        draw.AddText(new Vector2(_left + gap, _densityTop + gap), Color.FromArgb(150, 150, 150).ToUint(), text);

        if (!_config.Paused)
            return;

        const string hold = "HOLD";
        var holdSize = ImGui.CalcTextSize(hold);
        draw.AddText(new Vector2(_right - holdSize.X - gap, _densityBottom - holdSize.Y - gap),
            Color.OrangeRed.ToUint(), hold);
    }
}
