using System.Drawing;
using System.Numerics;
using ImGuiNET;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.measurements;

public partial class ChannelPowerView(MainWindowView initiator)
{
    public void RenderChannelPowerSettings()
    {
        Theme.Text("Channel BW");
        if (Theme.GlowingInput("channelPowerBandwith", ref _sDisplayBw,
                Theme.InputTheme)) //frequencyChangedByCenterSpan
        {
            double bw = 0;
            if (Global.TryFormatFreq(_sDisplayBw, out bw))
                _parent.Configuration.Config[Configuration.SaVar.ChannelBw] = bw;
        }

        Theme.NewLine();
        Theme.Text("Channel Occupied Bandwidth (0-99%)");
        if (Theme.GlowingInput("channelPowerOCP", ref _sDisplayObw, Theme.InputTheme)) //frequencyChangedByCenterSpan
        {
            double ocp = 0;
            if (double.TryParse(_sDisplayObw.Replace("%", ""), out ocp))
                if (ocp < 100 && ocp > 0)
                    _parent.Configuration.Config[Configuration.SaVar.ChannelOcp] = ocp / 100.0;
        }
        if(Theme.Button("Rest Channel Peak and Flatness values"))
        {
            peakValue = -99999;
            minValue = 99999;
        }
    }
    float peakValue = -99999, minValue = 99999;
    public void RenderChannelPower()
    {
        #region Canvas_Data

        var windowPos = ImGui.GetWindowPos();
        var draw = ImGui.GetForegroundDrawList();
        var graphStatus = new Vector2();
        _left = windowPos.X + _parent.Configuration.PositionOffset.X;
        _right = _left + _parent.Configuration.GraphSize.X;
        _top = windowPos.Y + _parent.Configuration.PositionOffset.Y;
        _bottom = _top + _parent.Configuration.GraphSize.Y * .75f - _parent.Configuration.PositionOffset.Y;
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
                var posY = _top + i / _graphLabelIdx * _parent.Configuration.GraphSize.Y * .75f;
                draw.AddText(new Vector2(_left - textSize.X, posY - textSize.Y / 2),
                    Color.LightGray.ToUint(), text);
                draw.AddLine(new Vector2(_left, posY), new Vector2(_right, posY),
                    Color.FromArgb(100, Color.Gray).ToUint());
            }

            var plot = _parent.TraceView.STraces[0].Plot;

            plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration
            //x = left, y = right
            var channelBandwithFreq = new Vector2((float)(_center - _channelBandwith / 2.0f),
                (float)(_center + _channelBandwith / 2.0f));

            for (var i = 1; i < plotData.Length; i++)
            {
                var sampleA = plotData[i - 1];
                var sampleB = plotData[i];

                var sampleAPos = _parent.GraphView.ScaleToGraph(_left, _top, _right, _bottom, sampleA.Key, sampleA.Value,
                    _center - _span / 2,
                    _center + _span / 2, _graphStartDb, _graphEndDb);
                var sampleBPos = _parent.GraphView.ScaleToGraph(_left, _top, _right, _bottom, sampleB.Key, sampleB.Value,
                    _center - _span / 2,
                    _center + _span / 2, _graphStartDb, _graphEndDb);
                //draw two lines of bandwith on the graph
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
                    if (!(bool)_parent.Configuration.Config[Configuration.SaVar.AutomaticLevel]) continue;
                    if (sampleAPos.Y < _top || sampleBPos.Y < _top)
                        _parent.Configuration.Config[Configuration.SaVar.GraphStartDb] =
                            (double)Math.Min(sampleA.Value, sampleB.Value);
                    else
                        _parent.Configuration.Config[Configuration.SaVar.GraphStopDb] =
                            (double)Math.Max(sampleA.Value, sampleB.Value);
                }

                draw.AddLine(sampleAPos, sampleBPos, traceColorUint, 1.0f);
            }

            if (!_calculatingBandPower)
                CalculateMeasurements(plotData.Slice(startBandPower, endBandPower - startBandPower).ToArray()
                    .Select(x => x.Value).ToArray());
        }
        catch (Exception ex)
        {
            _calculatingBandPower = false;
            Logger.Error($"Channe Power trace Render error -> {ex.Message}");
        }

        //draw OccupiedBW
        var occupiedStart = _parent.GraphView.ScaleToGraph(_left, _top, _right, _bottom,
            (float)(_center - _calculatedoccupiedBw / 2.0f), peakValue, _center - _span / 2,
            _center + _span / 2, _graphStartDb, _graphEndDb);
        var occupiedStop = _parent.GraphView.ScaleToGraph(_left, _top, _right, _bottom,
            (float)(_center + _calculatedoccupiedBw / 2.0f), peakValue, _center - _span / 2,
            _center + _span / 2, _graphStartDb, _graphEndDb);
        draw.AddLine(occupiedStart, occupiedStop, 0XFF00FF00);
        draw.AddLine(new Vector2(occupiedStart.X, _top), new Vector2(occupiedStart.X, _bottom), 0XFF00FF00);
        draw.AddLine(new Vector2(occupiedStop.X, _top), new Vector2(occupiedStop.X, _bottom), 0XFF00FF00);
        text = $"OBW {_calculatedoccupiedBw}hz";
        textSize = ImGui.CalcTextSize(text);
        draw.AddText(occupiedStart + new Vector2(0, -2 - textSize.Y),
            0XFF00FF00,
            text);

        #endregion graphDraw

        text = $"Center {_center} Span {_span} Channel BW {_channelBandwith} RBW {_fftRbw}";
        textSize = ImGui.CalcTextSize(text);
        draw.AddText(new Vector2(_left, _bottom - textSize.Y), 0xFFFFFFFF, text);
        _top = _bottom;
        _bottom += _parent.Configuration.GraphSize.Y * .25f;
        draw.AddRectFilled(new Vector2(_left, _top), new Vector2(_right, _bottom), Color.FromArgb(16, 16, 16).ToUint());
        draw.AddRect(new Vector2(_left, _top), new Vector2(_right, _bottom), Color.FromArgb(91, 36, 221).ToUint());
        var shrink = (_right - _left) * .25f;
        _left += shrink;
        _right -= shrink;

        text = $"Channel Power: {_calculatedbandPower.ToString().TruncateLongString(5)} dB\n" +
               $"Channel Flatness {(peakValue - minValue).ToString().TruncateLongString(5)} Pk->Pk\n" +
               $"Spectral Density: {(_calculatedbandPower - _channelBandwith.ToDBm()).ToString().TruncateLongString(5)} dBm/hz\n" +
               $"PAPR: {(peakValue - _calculatedbandAveragePower).ToString().TruncateLongString(5)} dBm\n" +
               $"{(_occupiedBwPrecentile * 100).ToString().TruncateLongString(5)}% Occupied Bandwith {(_calculatedoccupiedBw / 1e6).ToString().TruncateLongString(5)}Mhz";
        textPos = new Vector2(_left, _top);
        var measurements = text.Split('\n');
        foreach (var measurement in measurements)
        {
            textSize = ImGui.CalcTextSize(measurement);
            draw.AddText(textPos, 0xFFFFFFFF, measurement);
            textPos.Y += textSize.Y + 5 * _parent.Configuration.ScaleSize.Y;
        }
    }
}