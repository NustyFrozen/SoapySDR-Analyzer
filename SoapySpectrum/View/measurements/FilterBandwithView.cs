using ImGuiNET;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using System.Drawing;
using System.Numerics;
using static SoapySA.Configuration;
using Range = Pothosware.SoapySDR.Range;
namespace SoapySA.View.measurements;

public partial class FilterBandwithView : MeasurementFeature
{
    public FilterBandwithView(Configuration config, GraphPlotManager graphData)
    {
        _config = config;
        _graphData = graphData;

        // initial cache
        UpdateCanvasDataFromConfig();

        // keep cached values synced
        _config.PropertyChanged -= ConfigOnPropertyChanged;
        _config.PropertyChanged += ConfigOnPropertyChanged;
    }
    public override bool renderSettings() => false;
    public override bool renderGraph()
    {
        #region Canvas_Data

        var windowPos = ImGui.GetWindowPos();
        var draw = ImGui.GetForegroundDrawList();
        var mousePos = ImGui.GetMousePos();

        Left = windowPos.X + UserScreenConfiguration.PositionOffset.X;
        Right = Left + UserScreenConfiguration.GraphSize.X;
        Top = windowPos.Y + UserScreenConfiguration.PositionOffset.Y;
        Bottom = Top + UserScreenConfiguration.GraphSize.Y;

        float mousePosFreq = 0, mousePosdB;

        #endregion Canvas_Data

        #region Background Draw

        draw.AddRectFilled(new Vector2(Left, Top), new Vector2(Right, Bottom), Color.FromArgb(16, 16, 16).ToUint());

        if (new RectangleF(Left, Top, UserScreenConfiguration.GraphSize.X, UserScreenConfiguration.GraphSize.Y).Contains(mousePos.X, mousePos.Y))
        {
            draw.AddLine(new Vector2(Left, mousePos.Y), new Vector2(Right, mousePos.Y), Color.FromArgb(100, 100, 100).ToUint());
            draw.AddLine(new Vector2(mousePos.X, Top), new Vector2(mousePos.X, Bottom), Color.FromArgb(100, 100, 100).ToUint());

            mousePosFreq = (float)(FreqStart + (mousePos.X - Left) / UserScreenConfiguration.GraphSize.X * (FreqStop - FreqStart));
            mousePosdB = (float)(GraphStartDb - (Bottom - mousePos.Y + Top) / Bottom * (Math.Abs(GraphEndDb) - Math.Abs(GraphStartDb)) + DbOffset);

            draw.AddText(new Vector2(mousePos.X + 5, mousePos.Y + 5), Color.FromArgb(100, 100, 100).ToUint(),
                $"Freq {(mousePosFreq / 1e6).ToString().TruncateLongString(5)}M\ndBm {mousePosdB}");
        }

        for (float i = 0; i <= GraphLabelIdx; i++)
        {
            // draw X axis
            var text = $"{(FreqStart + i / GraphLabelIdx * (FreqStop - FreqStart)) / 1e6}".TruncateLongString(5) + "M";
            var posX = Left + i / GraphLabelIdx * UserScreenConfiguration.GraphSize.X - ImGui.CalcTextSize(text).X / 2;
            draw.AddText(new Vector2(posX, Bottom), Color.LightGray.ToUint(), text);
            draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Bottom), new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Top), Color.FromArgb(100, Color.Gray).ToUint());

            // draw Y axis
            var yLabel = Imports.Scale(i, 0, GraphLabelIdx, GraphEndDb + DbOffset, GraphStartDb + DbOffset).ToString().TruncateLongString(5);
            var posY = Top + i / GraphLabelIdx * UserScreenConfiguration.GraphSize.Y;
            draw.AddText(new Vector2(Left - ImGui.CalcTextSize(yLabel).X, posY - ImGui.CalcTextSize(yLabel).Y / 2), Color.LightGray.ToUint(), yLabel);
            draw.AddLine(new Vector2(Left, posY), new Vector2(Right, posY), Color.FromArgb(100, Color.Gray).ToUint());
        }

        #endregion Background Draw

        try
        {
            // Access traces via the injected _graphData manager
            var plot = _graphData.STraces[0].Plot;
            var plotData = plot.ToArray().AsSpan();

            if (!_calculatingFilterBw)
                CalculateMeasurements(plot);

            var transitionWidths = new List<Range>
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

                var sampleAPos = GraphPlotManager.ScaleToGraph(Left, Top, Right, Bottom, sampleA.Key, sampleA.Value, FreqStart, FreqStop, GraphStartDb, GraphEndDb);
                var sampleBPos = GraphPlotManager.ScaleToGraph(Left, Top, Right, Bottom, sampleB.Key, sampleB.Value, FreqStart, FreqStop, GraphStartDb, GraphEndDb);

                if (sampleBPos.X > Right || sampleAPos.X < Left) continue;

                if (sampleAPos.Y < Top || sampleBPos.Y < Top || sampleAPos.Y > Bottom || sampleBPos.Y > Bottom)
                {
                    if (_config.AutomaticLevel)
                    {
                        if (sampleAPos.Y < Top || sampleBPos.Y < Top)
                            _config.GraphStartDb = Math.Min(sampleA.Value, sampleB.Value);
                        else
                            _config.GraphStopDb = Math.Max(sampleA.Value, sampleB.Value);
                    }
                    continue;
                }

                draw.AddLine(sampleAPos, sampleBPos, traceColor, 1.0f);
            }

            var infoText = $"Center BW: {(_filterCenterFreq / 1e6):F3} MHz\n" +
                           $"Start: {((_filterCenterFreq - _leftBw) / 1e6):F3} MHz\n" +
                           $"Stop: {((_filterCenterFreq + _rightBw) / 1e6):F3} MHz\n" +
                           $"Span: {((passRange.Maximum - passRange.Minimum) / 1e3):F1} kHz";
            draw.AddText(new Vector2(Left + 10, Top + 10), 0xFFFFFFFF, infoText);
        }
        catch (Exception ex)
        {
            _logger.Trace($"FilterBandwith Render Error -> {ex.Message}");
        }
        return true;
    }
}