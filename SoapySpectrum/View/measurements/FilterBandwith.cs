using ImGuiNET;
using NLog;
using SoapySA.Extentions;
using SoapyVNACommon.Extentions;
using System.Numerics;
using Logger = NLog.Logger;
using Range = Pothosware.SoapySDR.Range;

namespace SoapySA.View.measurements
{
    public class FilterBandwith(MainWindow initiator)
    {
        private MainWindow parent = initiator;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static double dbOffset, refLevel, freqStart, freqStop, graph_startDB, graph_endDB;
        public static float graphLabelIdx, left, right, top, bottom;
        private static float _leftTransitionWidth, _rightTransitionWidth, _leftBW, _rightBW, _filterCenterFreq;
        private static bool _calculatingFilterBW, _calculateSideLobes;

        private static readonly uint c_colorPass = Color.FromArgb(0, 255, 0).ToUint(),
                                     c_ColorDeny = Color.Red.ToUint(),
                                     c_colorTransition = Color.Yellow.ToUint();

        public void updateCanvasData(object? sender, keyOfChangedValueEventArgs e)
        {
            #region Canvas_Data

            try
            {
                dbOffset = (double)parent.Configuration.config[Configuration.saVar.graphOffsetDB];
                refLevel = (double)parent.Configuration.config[Configuration.saVar.graphRefLevel];
                graphLabelIdx = (float)parent.tab_Amplitude.s_scalePerDivision;

                freqStart = (double)parent.Configuration.config[Configuration.saVar.freqStart];
                freqStop = (double)parent.Configuration.config[Configuration.saVar.freqStop];

                graph_startDB = (double)parent.Configuration.config[Configuration.saVar.graphStartDB] + refLevel;
                graph_endDB = (double)parent.Configuration.config[Configuration.saVar.graphStopDB] + refLevel;
            }
            catch (Exception ex)
            {
                _logger.Error($"error on updateCanvasData -> {ex.Message}");
            }

            #endregion Canvas_Data
        }

        public static Task calculateMeasurements(SortedDictionary<float, float> span)
        {
            if (_calculatingFilterBW) //another task is doing it, i dont want to fill threadpool
                return Task.CompletedTask;
            _calculatingFilterBW = true;
            try
            {
                int maxIdx = -1, minIdx = -1;
                float maxDb = -9999, minDb = 9999;
                var range = span.ToList();
                foreach (var sample in range)
                {
                    if (sample.Value > maxDb && sample.Key >= freqStart && sample.Key <= freqStop)
                    {
                        if (sample.Key == 0) continue;//some bug
                        maxDb = sample.Value;
                        maxIdx = range.FindIndex(x => x.Key == sample.Key);
                    }
                    if (sample.Value > maxDb && sample.Key >= span.First().Key && sample.Key <= span.Last().Key)
                    {
                        minDb = sample.Value;
                    }
                }
                int leftBwIdx = 0, leftLobeStopIdx = 0;
                for (int i = maxIdx; i != -1; i--)
                {
                    if (leftBwIdx == 0)
                    {
                        if (Math.Abs(maxDb - range[i].Value) >= 5)
                            leftBwIdx = i;
                    }
                    else if (Math.Abs(range[i].Value - minDb) >= 0.2) //a bit higher of floor level
                    {
                        leftLobeStopIdx = i;
                        break;
                    }
                }
                int rightBwIdx = range.Count, rightLobeStopIdx = range.Count;
                for (int i = maxIdx; i != range.Count; i++)
                {
                    if (rightBwIdx == range.Count)
                    {
                        if (Math.Abs(maxDb - range[i].Value) >= 5)
                            rightBwIdx = i;
                    }
                    else if (Math.Abs(range[i].Value - minDb) >= 0.2)
                    {
                        rightLobeStopIdx = i;
                        break;
                    }
                }
                _leftTransitionWidth = range[leftBwIdx].Key - range[leftLobeStopIdx].Key;
                _leftTransitionWidth = range[rightLobeStopIdx].Key - range[rightBwIdx].Key;
                _leftBW = range[maxIdx].Key - range[leftBwIdx].Key;
                _rightBW = range[rightBwIdx].Key - range[maxIdx].Key;
                _filterCenterFreq = range[maxIdx].Key;
            }
            catch (Exception e)
            {
                _logger.Trace($"FilterBandwith Measurement Error -> {e.Message}");
            }

            _calculatingFilterBW = false;
            return Task.CompletedTask;
        }

        public void renderFilterBandwith()
        {
            #region Canvas_Data

            var windowPos = ImGui.GetWindowPos();
            var draw = ImGui.GetForegroundDrawList();
            var mousePos = ImGui.GetMousePos();
            var graphStatus = new Vector2();
            left = windowPos.X + parent.Configuration.positionOffset.X;
            right = left + parent.Configuration.graphSize.X;
            top = windowPos.Y + parent.Configuration.positionOffset.Y;
            bottom = top + parent.Configuration.graphSize.Y;
            var mouseRange = new Vector2();
            float mousePosFreq = 0, mousePosdB;

            #endregion Canvas_Data

            #region backgroundDraw

            draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(16, 16, 16).ToUint());
            if (new RectangleF(left, top, parent.Configuration.graphSize.X, parent.Configuration.graphSize.Y).Contains(mousePos.X,
                    mousePos.Y))
            {
                draw.AddLine(new Vector2(left, mousePos.Y), new Vector2(right, mousePos.Y),
                    Color.FromArgb(100, 100, 100).ToUint());
                draw.AddLine(new Vector2(mousePos.X, top), new Vector2(mousePos.X, bottom),
                    Color.FromArgb(100, 100, 100).ToUint());

                mousePosFreq =
                    (float)(freqStart + (mousePos.X - left) / parent.Configuration.graphSize.X * (freqStop - freqStart));
                mousePosdB = (float)(graph_startDB -
                    (bottom - mousePos.Y + top) / bottom * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB)) + dbOffset);
                mouseRange.X = (float)(mousePosFreq - (freqStop - freqStart) / graphLabelIdx);
                mouseRange.Y = (float)(mousePosFreq + (freqStop - freqStart) / graphLabelIdx);
                draw.AddText(new Vector2(mousePos.X + 5, mousePos.Y + 5), Color.FromArgb(100, 100, 100).ToUint(),
                    $"Freq {(mousePosFreq / 1e6).ToString().TruncateLongString(5)}M\ndBm {mousePosdB}");
            }

            for (float i = 0; i <= graphLabelIdx; i++)
            {
                //draw X axis
                var text = $"{(freqStart + i / graphLabelIdx * (freqStop - freqStart)) / 1e6}".TruncateLongString(5);
                text += "M";
                var posX = left + i / graphLabelIdx * parent.Configuration.graphSize.X - ImGui.CalcTextSize(text).X / 2;
                draw.AddText(new Vector2(posX, bottom), Color.LightGray.ToUint(), text);

                draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, bottom),
                    new Vector2(posX + ImGui.CalcTextSize(text).X / 2, top), Color.FromArgb(100, Color.Gray).ToUint());

                //draw Y axis
                text = Imports.Scale(i, 0, graphLabelIdx, graph_endDB + dbOffset, graph_startDB + dbOffset).ToString()
                    .TruncateLongString(5);
                //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
                var posY = top + i / graphLabelIdx * parent.Configuration.graphSize.Y;
                draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                    Color.LightGray.ToUint(), text);
                draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), Color.FromArgb(100, Color.Gray).ToUint());
            }

            #endregion backgroundDraw

            try
            {
                var plot = parent.tab_Trace.s_traces[0].plot;
                var plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration
                if (!_calculatingFilterBW)
                    calculateMeasurements(plot);
                List<Range> transitionWidths =
                new List<Range>(){
                    new Range(_filterCenterFreq - _leftBW - _leftTransitionWidth, _filterCenterFreq - _leftBW),
                    new Range(_filterCenterFreq + _rightBW, _filterCenterFreq + _rightBW + _rightTransitionWidth)
                };
                Range passRange = new Range(_filterCenterFreq - _leftBW, _filterCenterFreq + _rightBW);
                for (var i = 1; i < plotData.Length; i++)
                {
                    var sampleA = plotData[i - 1];
                    var sampleB = plotData[i];
                    var traceColor = c_ColorDeny;
                    if (transitionWidths.Exists(x => (x.Minimum <= sampleA.Key && x.Maximum >= sampleB.Key)))
                        traceColor = c_colorTransition;
                    if (passRange.Minimum <= sampleA.Key && passRange.Maximum >= sampleB.Key)
                        traceColor = c_colorPass;

                    var sampleAPos = parent.Graph.scaleToGraph(left, top, right, bottom, sampleA.Key, sampleA.Value, freqStart,
                        freqStop, graph_startDB, graph_endDB);
                    var sampleBPos = parent.Graph.scaleToGraph(left, top, right, bottom, sampleB.Key, sampleB.Value, freqStart,
                        freqStop, graph_startDB, graph_endDB);
                    //bounds check
                    if (sampleBPos.X > right || sampleAPos.X < left) continue;
                    if (sampleAPos.Y < top || sampleBPos.Y < top || sampleAPos.Y > bottom || sampleBPos.Y > bottom)
                    {
                        if (!(bool)parent.Configuration.config[Configuration.saVar.automaticLevel]) continue;
                        if (sampleAPos.Y < top || sampleBPos.Y < top)
                            parent.Configuration.config[Configuration.saVar.graphStartDB] =
                                (double)Math.Min(sampleA.Value, sampleB.Value);
                        else
                            parent.Configuration.config[Configuration.saVar.graphStopDB] =
                                (double)Math.Max(sampleA.Value, sampleB.Value);
                    }
                    draw.AddLine(sampleAPos, sampleBPos, traceColor, 1.0f);
                }

                var text = $"Center BW: {_filterCenterFreq}Hz\n" +
                           $"Start: {_filterCenterFreq - _leftBW}Hz\n" +
                           $"Stop: {_filterCenterFreq + _rightBW}Hz\n" +
                           $"Span: {passRange.Maximum - passRange.Minimum}Hz";
                draw.AddText(new Vector2(left, top), 0XFFFFFFFF, text);
            }
            catch (Exception ex)
            {
                _logger.Trace($"FilterBandwith Render Error -> {ex.Message}");
            }
        }
    }
}