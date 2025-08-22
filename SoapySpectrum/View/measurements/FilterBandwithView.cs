using System.Numerics;
using ImGuiNET;
using SoapyVNACommon.Extentions;
using Range = Pothosware.SoapySDR.Range;

namespace SoapySA.View.measurements;

public partial class FilterBandwithView(MainWindowView initiator)
{
    public void RenderFilterBandwith()
    {
        #region Canvas_Data

        var windowPos = ImGui.GetWindowPos();
        var draw = ImGui.GetForegroundDrawList();
        var mousePos = ImGui.GetMousePos();
        var graphStatus = new Vector2();
        Left = windowPos.X + _parent.Configuration.PositionOffset.X;
        Right = Left + _parent.Configuration.GraphSize.X;
        Top = windowPos.Y + _parent.Configuration.PositionOffset.Y;
        Bottom = Top + _parent.Configuration.GraphSize.Y;
        var mouseRange = new Vector2();
        float mousePosFreq = 0, mousePosdB;

        #endregion Canvas_Data

        #region backgroundDraw

        draw.AddRectFilled(new Vector2(Left, Top), new Vector2(Right, Bottom), Color.FromArgb(16, 16, 16).ToUint());
        if (new RectangleF(Left, Top, _parent.Configuration.GraphSize.X, _parent.Configuration.GraphSize.Y).Contains(
                mousePos.X,
                mousePos.Y))
        {
            draw.AddLine(new Vector2(Left, mousePos.Y), new Vector2(Right, mousePos.Y),
                Color.FromArgb(100, 100, 100).ToUint());
            draw.AddLine(new Vector2(mousePos.X, Top), new Vector2(mousePos.X, Bottom),
                Color.FromArgb(100, 100, 100).ToUint());

            mousePosFreq =
                (float)(FreqStart + (mousePos.X - Left) / _parent.Configuration.GraphSize.X * (FreqStop - FreqStart));
            mousePosdB = (float)(GraphStartDb -
                (Bottom - mousePos.Y + Top) / Bottom * (Math.Abs(GraphEndDb) - Math.Abs(GraphStartDb)) + DbOffset);
            mouseRange.X = (float)(mousePosFreq - (FreqStop - FreqStart) / GraphLabelIdx);
            mouseRange.Y = (float)(mousePosFreq + (FreqStop - FreqStart) / GraphLabelIdx);
            draw.AddText(new Vector2(mousePos.X + 5, mousePos.Y + 5), Color.FromArgb(100, 100, 100).ToUint(),
                $"Freq {(mousePosFreq / 1e6).ToString().TruncateLongString(5)}M\ndBm {mousePosdB}");
        }

        for (float i = 0; i <= GraphLabelIdx; i++)
        {
            //draw X axis
            var text = $"{(FreqStart + i / GraphLabelIdx * (FreqStop - FreqStart)) / 1e6}".TruncateLongString(5);
            text += "M";
            var posX = Left + i / GraphLabelIdx * _parent.Configuration.GraphSize.X - ImGui.CalcTextSize(text).X / 2;
            draw.AddText(new Vector2(posX, Bottom), Color.LightGray.ToUint(), text);

            draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Bottom),
                new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Top), Color.FromArgb(100, Color.Gray).ToUint());

            //draw Y axis
            text = Imports.Scale(i, 0, GraphLabelIdx, GraphEndDb + DbOffset, GraphStartDb + DbOffset).ToString()
                .TruncateLongString(5);
            //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
            var posY = Top + i / GraphLabelIdx * _parent.Configuration.GraphSize.Y;
            draw.AddText(new Vector2(Left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                Color.LightGray.ToUint(), text);
            draw.AddLine(new Vector2(Left, posY), new Vector2(Right, posY), Color.FromArgb(100, Color.Gray).ToUint());
        }

        #endregion backgroundDraw

        try
        {
            var plot = _parent.TraceView.STraces[0].Plot;
            var plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration
            if (!_calculatingFilterBw)
                CalculateMeasurements(plot);
            var transitionWidths =
                new List<Range>
                {
                    new(_filterCenterFreq - _leftBw - _leftTransitionWidth, _filterCenterFreq - _leftBw),
                    new(_filterCenterFreq + _rightBw, _filterCenterFreq + _rightBw + _rightTransitionWidth)
                };
            var passRange = new Range(_filterCenterFreq - _leftBw, _filterCenterFreq + _rightBw);
            for (var i = 1; i < plotData.Length; i++)
            {
                var sampleA = plotData[i - 1];
                var sampleB = plotData[i];
                var traceColor = CColorDeny;
                if (transitionWidths.Exists(x => x.Minimum <= sampleA.Key && x.Maximum >= sampleB.Key))
                    traceColor = CColorTransition;
                if (passRange.Minimum <= sampleA.Key && passRange.Maximum >= sampleB.Key)
                    traceColor = CColorPass;

                var sampleAPos = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom, sampleA.Key, sampleA.Value,
                    FreqStart,
                    FreqStop, GraphStartDb, GraphEndDb);
                var sampleBPos = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom, sampleB.Key, sampleB.Value,
                    FreqStart,
                    FreqStop, GraphStartDb, GraphEndDb);
                //bounds check
                if (sampleBPos.X > Right || sampleAPos.X < Left) continue;
                if (sampleAPos.Y < Top || sampleBPos.Y < Top || sampleAPos.Y > Bottom || sampleBPos.Y > Bottom)
                {
                    if (!(bool)_parent.Configuration.Config[Configuration.SaVar.AutomaticLevel]) continue;
                    if (sampleAPos.Y < Top || sampleBPos.Y < Top)
                        _parent.Configuration.Config[Configuration.SaVar.GraphStartDb] =
                            (double)Math.Min(sampleA.Value, sampleB.Value);
                    else
                        _parent.Configuration.Config[Configuration.SaVar.GraphStopDb] =
                            (double)Math.Max(sampleA.Value, sampleB.Value);
                }

                draw.AddLine(sampleAPos, sampleBPos, traceColor, 1.0f);
            }

            var text = $"Center BW: {_filterCenterFreq}Hz\n" +
                       $"Start: {_filterCenterFreq - _leftBw}Hz\n" +
                       $"Stop: {_filterCenterFreq + _rightBw}Hz\n" +
                       $"Span: {passRange.Maximum - passRange.Minimum}Hz";
            draw.AddText(new Vector2(Left, Top), 0XFFFFFFFF, text);
        }
        catch (Exception ex)
        {
            _logger.Trace($"FilterBandwith Render Error -> {ex.Message}");
        }
    }
}