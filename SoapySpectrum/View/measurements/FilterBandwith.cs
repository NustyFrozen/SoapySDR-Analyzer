using ImGuiNET;
using NLog;
using SoapySA.Extentions;
using SoapySA.View.tabs;
using System.Numerics;

namespace SoapySA.View.measurements
{
    internal class FilterBandwith
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static double dbOffset, refLevel, freqStart, freqStop, graph_startDB, graph_endDB;
        public static float graphLabelIdx, left, right, top, bottom;

        public void renderFilterBandwith()
        {
            #region Canvas_Data

            var windowPos = ImGui.GetWindowPos();
            var draw = ImGui.GetForegroundDrawList();
            var mousePos = ImGui.GetMousePos();
            var graphStatus = new Vector2();
            left = windowPos.X + Configuration.positionOffset.X;
            right = left + Configuration.graphSize.X;
            top = windowPos.Y + Configuration.positionOffset.Y;
            bottom = top + Configuration.graphSize.Y;
            var mouseRange = new Vector2();
            float mousePosFreq = 0, mousePosdB;

            #endregion Canvas_Data

            #region backgroundDraw

            draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(16, 16, 16).ToUint());
            if (new RectangleF(left, top, Configuration.graphSize.X, Configuration.graphSize.Y).Contains(mousePos.X,
                    mousePos.Y))
            {
                draw.AddLine(new Vector2(left, mousePos.Y), new Vector2(right, mousePos.Y),
                    Color.FromArgb(100, 100, 100).ToUint());
                draw.AddLine(new Vector2(mousePos.X, top), new Vector2(mousePos.X, bottom),
                    Color.FromArgb(100, 100, 100).ToUint());

                mousePosFreq =
                    (float)(freqStart + (mousePos.X - left) / Configuration.graphSize.X * (freqStop - freqStart));
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
                var posX = left + i / graphLabelIdx * Configuration.graphSize.X - ImGui.CalcTextSize(text).X / 2;
                draw.AddText(new Vector2(posX, bottom), Color.LightGray.ToUint(), text);

                draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, bottom),
                    new Vector2(posX + ImGui.CalcTextSize(text).X / 2, top), Color.FromArgb(100, Color.Gray).ToUint());

                //draw Y axis
                text = Imports.Scale(i, 0, graphLabelIdx, graph_endDB + dbOffset, graph_startDB + dbOffset).ToString()
                    .TruncateLongString(5);
                //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
                var posY = top + i / graphLabelIdx * Configuration.graphSize.Y;
                draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                    Color.LightGray.ToUint(), text);
                draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), Color.FromArgb(100, Color.Gray).ToUint());
            }

            #endregion backgroundDraw

            try
            {
                for (var x = 0; x < tab_Trace.s_traces.Length; x++)
                {
                    if (tab_Trace.s_traces[x].viewStatus == tab_Trace.traceViewStatus.clear) continue;
                    var currentActiveMarkers = tab_Marker.s_markers.Where(d => d.reference == x && d.isActive).ToArray();
                    var bandPowerDBList = new List<float>();
                    var traceColor = Color.Yellow;
                    switch (x)
                    {
                        case 1:
                            traceColor = Color.FromArgb(0, 255, 255);
                            break;

                        case 2:
                            traceColor = Color.FromArgb(255, 0, 255);
                            break;

                        case 3:
                            traceColor = Color.FromArgb(0, 255, 0);
                            break;

                        case 4:
                            traceColor = Color.FromArgb(0, 0, 255);
                            break;

                        case 5:
                            traceColor = Color.FromArgb(255, 0, 0);
                            break;
                    }

                    if (tab_Trace.s_traces[x].viewStatus == tab_Trace.traceViewStatus.view)
                        traceColor = Color.FromArgb(100, traceColor);
                    var plot = tab_Trace.s_traces[x].plot;
                    var traceColor_uint = traceColor.ToUint();
                    var plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration
                    for (var i = 1; i < plotData.Length; i++)
                    {
                        var sampleA = plotData[i - 1];
                        var sampleB = plotData[i];

                        var sampleAPos = Graph.scaleToGraph(left, top, right, bottom, sampleA.Key, sampleA.Value, freqStart,
                            freqStop, graph_startDB, graph_endDB);
                        var sampleBPos = Graph.scaleToGraph(left, top, right, bottom, sampleB.Key, sampleB.Value, freqStart,
                            freqStop, graph_startDB, graph_endDB);
                        //bounds check
                        if (sampleBPos.X > right || sampleAPos.X < left) continue;
                        if (sampleAPos.Y < top || sampleBPos.Y < top || sampleAPos.Y > bottom || sampleBPos.Y > bottom)
                        {
                            if (!(bool)Configuration.config[Configuration.saVar.automaticLevel]) continue;
                            if (sampleAPos.Y < top || sampleBPos.Y < top)
                                Configuration.config[Configuration.saVar.graphStartDB] =
                                    (double)Math.Min(sampleA.Value, sampleB.Value);
                            else
                                Configuration.config[Configuration.saVar.graphStopDB] =
                                    (double)Math.Max(sampleA.Value, sampleB.Value);
                        }

                        draw.AddLine(sampleAPos, sampleBPos, traceColor_uint, 1.0f);
                        currentActiveMarkers = currentActiveMarkers.Select(marker =>
                        {
                            //apply new db value for marker
                            if (marker.position >= sampleA.Key && marker.position <= sampleB.Key)
                            {
                                marker.value = Math.Abs(marker.position - sampleA.Key) >=
                                               Math.Abs(marker.position - sampleB.Key)
                                    ? sampleA.Value
                                    : sampleB.Value; //to which point is he closer
                                tab_Marker.s_markers[marker.id].value = marker.value;
                            }

                            //apply bandPower List
                            if (marker.bandPower)
                                if (sampleA.Key >= (float)(marker.position - marker.bandPowerSpan / 2) && sampleA.Key <=
                                    (float)(tab_Marker.s_markers[marker.id].position + marker.bandPowerSpan / 2))
                                {
                                    draw.AddLine(sampleAPos, sampleBPos, Color.White.ToUint(), 1.0f);
                                    bandPowerDBList.Add(sampleA.Value);
                                }

                            return marker;
                        }).ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Sequence"))
                    _logger.Trace($"NormalMeasurement Render Error -> {ex.Message}");
            }
        }
    }
}