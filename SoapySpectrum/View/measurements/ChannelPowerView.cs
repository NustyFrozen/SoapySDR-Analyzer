using ImGuiNET;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapySA.View.tabs;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using System.Drawing;
using System.Numerics;
using static SoapySA.Configuration;

namespace SoapySA.View.measurements;

public partial class ChannelPowerView : MeasurementFeature
{
    public override string Name => $"{FontAwesome5.Bolt} Channel Power";
    private readonly Configuration _config;
    private readonly GraphPlotManager graphHandle;

    public ChannelPowerView(Configuration config, GraphPlotManager GraphHandle)
    {
        _config = config;
        this.graphHandle = GraphHandle;
        HookConfig();
        UpdateFromConfig(); // initial
    }

    public override bool renderSettings()
    {
        Theme.Text("Channel BW");
        if (Theme.GlowingInput("channelPowerBandwith", ref _sDisplayBw, Theme.InputTheme))
        {
            if (Global.TryFormatFreq(_sDisplayBw, out var bw))
                _config.ChannelBw = bw;
        }

        Theme.NewLine();
        Theme.Text("Channel Occupied Bandwidth (0-99%)");
        if (Theme.GlowingInput("channelPowerOCP", ref _sDisplayObw, Theme.InputTheme))
        {
            if (double.TryParse(_sDisplayObw.Replace("%", ""), out var ocp))
            {
                if (ocp < 100 && ocp > 0)
                    _config.ChannelOcp = ocp / 100.0;
            }
        }

        if (Theme.Button("Rest Channel Peak and Flatness values"))
        {
            peakValue = -99999;
            minValue = 99999;
        }
        return true;
    }

    float peakValue = -99999, minValue = 99999;

    public override bool renderGraph()
    {
        #region Canvas_Data

        var windowPos = ImGui.GetWindowPos();
        var draw = ImGui.GetForegroundDrawList();
        var graphStatus = new Vector2();
        _left = windowPos.X + UserScreenConfiguration.PositionOffset.X;
        _right = _left + UserScreenConfiguration.GraphSize.X;
        _top = windowPos.Y + UserScreenConfiguration.PositionOffset.Y;
        _bottom = _top + UserScreenConfiguration.GraphSize.Y * .75f - UserScreenConfiguration.PositionOffset.Y;
        var text = string.Empty;
        var textSize = new Vector2();
        var textPos = new Vector2();
        uint traceColorUint = 0x7FFFFF00;
        int startBandPower = 0, endBandPower = 0;
        var plotData = new Span<KeyValuePair<float, float>>();

        #endregion Canvas_Data

        #region graphDraw

        try
        {
            draw.AddRectFilled(new Vector2(_left, _top), new Vector2(_right, _bottom), Color.FromArgb(16, 16, 16).ToUint());
            for (float i = 0; i < _graphLabelIdx; i++)
            {
                //draw Y axis
                text = Imports.Scale(i, 0, _graphLabelIdx, _graphEndDb + _dbOffset, _graphStartDb + _dbOffset).ToString()
                    .TruncateLongString(5);
                textSize = ImGui.CalcTextSize(text);
                var posY = _top + i / _graphLabelIdx * UserScreenConfiguration.GraphSize.Y * .75f;
                draw.AddText(new Vector2(_left - textSize.X, posY - textSize.Y / 2),
                    Color.LightGray.ToUint(), text);
                draw.AddLine(new Vector2(_left, posY), new Vector2(_right, posY),
                    Color.FromArgb(100, Color.Gray).ToUint());
            }

            var plot = graphHandle.STraces[0].Plot;

            plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration
            //x = left, y = right
            var channelBandwithFreq = new Vector2((float)(_center - _channelBandwith / 2.0f),
                (float)(_center + _channelBandwith / 2.0f));

            for (var i = 1; i < plotData.Length; i++)
            {
                var sampleA = plotData[i - 1];
                var sampleB = plotData[i];

                var sampleAPos = GraphPlotManager.ScaleToGraph(_left, _top, _right, _bottom, sampleA.Key, sampleA.Value,
                    _center - _span / 2,
                    _center + _span / 2, _graphStartDb, _graphEndDb);
                var sampleBPos = GraphPlotManager.ScaleToGraph(_left, _top, _right, _bottom, sampleB.Key, sampleB.Value,
                    _center - _span / 2,
                    _center + _span / 2, _graphStartDb, _graphEndDb);

                //draw two lines of bandwidth on the graph
                if (sampleA.Key <= channelBandwithFreq.X && sampleB.Key >= channelBandwithFreq.X)
                {
                    startBandPower = i;
                    draw.AddLine(new Vector2(sampleAPos.X, _top), new Vector2(sampleAPos.X, _bottom), 0xFF726E6D);
                    traceColorUint = 0xFFFFFF00;
                }
                else if (sampleA.Key <= channelBandwithFreq.Y && sampleB.Key >= channelBandwithFreq.Y)
                {
                    endBandPower = i;
                    draw.AddLine(new Vector2(sampleAPos.X, _top), new Vector2(sampleAPos.X, _bottom), 0xFF726E6D);
                    traceColorUint = 0x7FFFFF00;
                }

                if (startBandPower == i || endBandPower == i)
                {
                    if (sampleA.Value > peakValue)
                        peakValue = sampleA.Value;
                    if (sampleA.Value < minValue)
                        minValue = sampleA.Value;
                }

                //bounds check
                if (sampleBPos.X > _right || sampleAPos.X < _left) continue;

                if (sampleAPos.Y < _top || sampleBPos.Y < _top || sampleAPos.Y > _bottom || sampleBPos.Y > _bottom)
                {
                    if (!_config.AutomaticLevel) continue;

                    if (sampleAPos.Y < _top || sampleBPos.Y < _top)
                        _config.GraphStartDb = Math.Min(sampleA.Value, sampleB.Value);
                    else
                        _config.GraphStopDb = Math.Max(sampleA.Value, sampleB.Value);
                }

                draw.AddLine(sampleAPos, sampleBPos, traceColorUint, 1.0f);
            }

            if (!_calculatingBandPower)
            {
                _ = CalculateMeasurements(
                    plotData.Slice(startBandPower, endBandPower - startBandPower)
                        .ToArray()
                        .Select(x => x.Value)
                        .ToArray());
            }
        }
        catch (Exception ex)
        {
            _calculatingBandPower = false;
            Logger.Error($"Channe Power trace Render error -> {ex.Message}");
        }

        //draw OccupiedBW
        var occupiedStart = GraphPlotManager.ScaleToGraph(_left, _top, _right, _bottom,
            (float)(_center - _calculatedoccupiedBw / 2.0f), peakValue, _center - _span / 2,
            _center + _span / 2, _graphStartDb, _graphEndDb);
        var occupiedStop = GraphPlotManager.ScaleToGraph(_left, _top, _right, _bottom,
            (float)(_center + _calculatedoccupiedBw / 2.0f), peakValue, _center - _span / 2,
            _center + _span / 2, _graphStartDb, _graphEndDb);

        draw.AddLine(occupiedStart, occupiedStop, 0XFF00FF00);
        draw.AddLine(new Vector2(occupiedStart.X, _top), new Vector2(occupiedStart.X, _bottom), 0XFF00FF00);
        draw.AddLine(new Vector2(occupiedStop.X, _top), new Vector2(occupiedStop.X, _bottom), 0XFF00FF00);

        text = $"OBW {_calculatedoccupiedBw}hz";
        textSize = ImGui.CalcTextSize(text);
        draw.AddText(occupiedStart + new Vector2(0, -2 - textSize.Y), 0XFF00FF00, text);

        #endregion graphDraw

        text = $"Center {_center} Span {_span} Channel BW {_channelBandwith} RBW {_fftRbw}";
        textSize = ImGui.CalcTextSize(text);
        draw.AddText(new Vector2(_left, _bottom - textSize.Y), 0xFFFFFFFF, text);

        _top = _bottom;
        _bottom += UserScreenConfiguration.GraphSize.Y * .25f;

        draw.AddRectFilled(new Vector2(_left, _top), new Vector2(_right, _bottom), Color.FromArgb(16, 16, 16).ToUint());
        draw.AddRect(new Vector2(_left, _top), new Vector2(_right, _bottom), Color.FromArgb(91, 36, 221).ToUint());

        var shrink = (_right - _left) * .25f;
        _left += shrink;
        _right -= shrink;

        text =
            $"Channel Power: {_calculatedbandPower.ToString().TruncateLongString(5)} dB\n" +
            $"Channel Flatness {(peakValue - minValue).ToString().TruncateLongString(5)} Pk->Pk\n" +
            $"Spectral Density: {(_calculatedbandPower - _channelBandwith.ToDBm()).ToString().TruncateLongString(5)} dBm/hz\n" +
            $"PAPR: {(peakValue - _calculatedbandAveragePower).ToString().TruncateLongString(5)} dBm\n" +
            $"{(_occupiedBwPrecentile * 100).ToString().TruncateLongString(5)}% Occupied Bandwith {(_calculatedoccupiedBw / 1e6).ToString().TruncateLongString(5)}Mhz";

        textPos = new Vector2(_left, _top);
        foreach (var measurement in text.Split('\n'))
        {
            textSize = ImGui.CalcTextSize(measurement);
            draw.AddText(textPos, 0xFFFFFFFF, measurement);
            textPos.Y += textSize.Y + 5 * UserScreenConfiguration.ScaleSize.Y;
        }
        return true;
    }
}