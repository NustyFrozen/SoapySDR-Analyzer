using ImGuiNET;
using SoapyRL.Extentions;
using System.Diagnostics;
using System.Numerics;

namespace SoapyRL.UI
{
    public static class Graph
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public static void initializeGraphElements()
        {
            for (int i = 0; i < tab_Trace.s_traces.Length; i++)
            {
                tab_Trace.s_traces[i] = new tab_Trace.Trace();
            }
            for (int i = 0; i < tab_Marker.s_markers.Length; i++)
            {
                tab_Marker.s_markers[i] = new tab_Marker.marker();
                tab_Marker.s_markers[i].id = i;
            }
            tab_Marker.s_markerTraceCombo = tab_Trace.s_comboTraces;
            tab_Trace.s_traces[0].viewStatus = tab_Trace.traceViewStatus.active;
        }

        public static void clearPlotData()
        {
            for (int i = 0; i < tab_Trace.s_traces.Length; i++)
            {
                if (tab_Trace.s_traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
                var plot = tab_Trace.s_traces[i].plot;
                plot.Clear();
            }
        }

        public static void updateData(float[][] psd)
        {
            var data = psd.AsSpan();
            for (int i = 0; i < tab_Trace.s_traces.Length; i++)
            {
                if (tab_Trace.s_traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
                var plot = tab_Trace.s_traces[i].plot;
                lock (plot)
                {
                    for (int k = 0; k < data[0].Length; k++)
                    {
                        if (plot.ContainsKey(data[1][k]))
                        {
                            if (plot[data[1][k]] < data[0][k])
                                plot[data[1][k]] = data[0][k];
                        }
                        else
                            plot.Add(data[1][k], data[0][k]);
                    }
                }
            }
        }

        private static Vector2 scaleToGraph(float left, float top, float right, float bottom, float freq, float dB, double freqStart, double freqStop, double graph_startDB, double graph_endDB)
        {
            double scale = freqStop - freqStart;
            double scale2 = freq - freqStart;
            double scaledX = left + Configuration.graphSize.X * (scale2 / scale);
            //endb = 0

            var scaledY = Extentions.Imports.Scale(dB, graph_startDB, graph_endDB, bottom, top);
            return new Vector2((float)scaledX, (float)scaledY);
        }

        private static Thread _calculateBandPowerThread;

        public static Stopwatch s_waitForMouseClick = new Stopwatch();

        public static void drawGraph()
        {
            #region Canvas_Data

            var draw = ImGui.GetForegroundDrawList();

            float left = ImGui.GetWindowPos().X + Configuration.positionOffset.X;
            float right = left + Configuration.graphSize.X;
            float top = ImGui.GetWindowPos().Y + Configuration.positionOffset.Y;
            float bottom = top + Configuration.graphSize.Y;

            float graphLabelIdx = 20.0f;

            double freqStart = (double)Configuration.config[Configuration.saVar.freqStart];
            double freqStop = (double)Configuration.config[Configuration.saVar.freqStop];
            var mousePos = ImGui.GetMousePos();
            double graph_startDB = 100;
            double graph_endDB = 0;
            Vector2 graphStatus = new Vector2();
            draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), ColorExtention.ToUint(Color.FromArgb(16, 16, 16)));
            Vector2 mouseRange = new Vector2();
            float mousePosFreq = 0, mousePosdB;

            #endregion Canvas_Data

            #region backgroundDraw

            if (new RectangleF(left, top, Configuration.graphSize.X, Configuration.graphSize.Y).Contains(mousePos.X, mousePos.Y))
            {
                draw.AddLine(new Vector2(left, mousePos.Y), new Vector2(right, mousePos.Y), Color.FromArgb(100, 100, 100).ToUint());
                draw.AddLine(new Vector2(mousePos.X, top), new Vector2(mousePos.X, bottom), Color.FromArgb(100, 100, 100).ToUint());

                mousePosFreq = (float)((freqStart + ((mousePos.X - left) / (Configuration.graphSize.X)) * (freqStop - freqStart)));
                mousePosdB = (float)((graph_startDB - (bottom - mousePos.Y + top) / bottom * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))));
                mouseRange.X = (float)(mousePosFreq - (freqStop - freqStart) / graphLabelIdx);
                mouseRange.Y = (float)(mousePosFreq + (freqStop - freqStart) / graphLabelIdx);
                draw.AddText(new Vector2(mousePos.X + 5, mousePos.Y + 5), Color.FromArgb(100, 100, 100).ToUint(), $"Freq {(mousePosFreq / 1e6).ToString().TruncateLongString(5)}M\ndBm {mousePosdB}");
            }
            for (float i = 0; i <= graphLabelIdx; i++)
            {
                //draw X axis
                string text = $"{(freqStart + i / graphLabelIdx * (freqStop - freqStart)) / 1e6}".TruncateLongString(5);
                text += "M";
                float posX = left + i / graphLabelIdx * Configuration.graphSize.X - ImGui.CalcTextSize(text).X / 2;
                draw.AddText(new Vector2(posX, bottom), ColorExtention.ToUint(Color.LightGray), text);

                draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, bottom), new Vector2(posX + ImGui.CalcTextSize(text).X / 2, top), ColorExtention.ToUint(Color.FromArgb(100, Color.Gray)));

                //draw Y axis
                text = Extentions.Imports.Scale(i, 0, graphLabelIdx, graph_endDB, graph_startDB).ToString().TruncateLongString(5);
                //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
                float posY = top + i / graphLabelIdx * Configuration.graphSize.Y;
                draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2), ColorExtention.ToUint(Color.LightGray), text);
                draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), ColorExtention.ToUint(Color.FromArgb(100, Color.Gray)));
            }

            #endregion backgroundDraw

            try
            {
                var data = tab_Trace.s_traces[0].plot.ToArray();
                var referenceData = data.AsSpan();
                var minDB = data.MinBy(x=>x.Value).Value;
                var traceColor_uint = ColorExtention.ToUint(Color.Yellow);
                var fadedColorYellow = Color.FromArgb(100, Color.Yellow).ToUint();
                for (int i = 1; i < referenceData.Length; i++)
                {
                    var sampleA = referenceData[i - 1];
                    var sampleADB = 0;
                    var sampleB = referenceData[i];
                    var sampleBDB = 0;
                    Vector2 sampleAPos = scaleToGraph(left, top, right, bottom, sampleA.Key, sampleADB, freqStart, freqStop, graph_startDB, graph_endDB);
                    Vector2 sampleBPos = scaleToGraph(left, top, right, bottom, sampleB.Key, sampleBDB, freqStart, freqStop, graph_startDB, graph_endDB);
                    //bounds check
                    if (sampleBPos.X > right || sampleAPos.X < left) continue;

                    draw.AddLine(sampleAPos, sampleBPos, traceColor_uint, 1.0f);
                    
                    Vector2 sampleAPosRef = scaleToGraph(left, top, right, bottom, sampleA.Key, sampleA.Value - minDB, freqStart, freqStop, graph_startDB, graph_endDB);
                    Vector2 sampleBPosRef = scaleToGraph(left, top, right, bottom, sampleB.Key, sampleB.Value - minDB, freqStart, freqStop, graph_startDB, graph_endDB);

                    draw.AddLine(sampleAPosRef, sampleBPosRef, fadedColorYellow, 1.0f);
                }
                var x = 1;
                var currentActiveMarkers = tab_Marker.s_markers.Where(d => d.reference == x && d.isActive).ToArray();
                traceColor_uint = ColorExtention.ToUint(Color.Cyan);
                var fadedColorCyan = Color.FromArgb(100, Color.Cyan).ToUint();
                var anntennaData = tab_Trace.s_traces[1].plot.ToArray().AsSpan(); //asspan is fastest iteration
                Console.WriteLine($" {referenceData.Length},{anntennaData.Length}");
                for (int i = 1; i < anntennaData.Length; i++)
                {
                    if (referenceData.Length <= i) continue;
                    var sampleA = anntennaData[i - 1];
                    var sampleARL = referenceData[i - 1].Value - sampleA.Value;
                    var sampleB = anntennaData[i];
                    var valueRefB = referenceData[i].Value;
                    var sampleBRL = valueRefB - sampleB.Value;

                    Vector2 sampleAPos = scaleToGraph(left, top, right, bottom, sampleA.Key, sampleARL, freqStart, freqStop, graph_startDB, graph_endDB);
                    Vector2 sampleBPos = scaleToGraph(left, top, right, bottom, sampleB.Key, sampleBRL, freqStart, freqStop, graph_startDB, graph_endDB);
                    //bounds check
                    if (sampleAPos.Y < top || sampleBPos.Y < top
                        || sampleAPos.Y > bottom || sampleBPos.Y > bottom)
                    {
                        sampleAPos.Y = (sampleAPos.Y < top) ? top : (sampleAPos.Y > bottom) ? bottom : sampleAPos.Y;
                        sampleBPos.Y = (sampleBPos.Y < top) ? top : (sampleBPos.Y > bottom) ? bottom : sampleBPos.Y;
                    };

                    draw.AddLine(sampleAPos, sampleBPos, traceColor_uint, 1.0f);
                    Vector2 sampleAPosRef = scaleToGraph(left, top, right, bottom, sampleA.Key, sampleA.Value - minDB, freqStart, freqStop, graph_startDB, graph_endDB);
                    Vector2 sampleBPosRef = scaleToGraph(left, top, right, bottom, sampleB.Key, sampleB.Value - minDB, freqStart, freqStop, graph_startDB, graph_endDB);
                    draw.AddLine(sampleAPosRef, sampleBPosRef, fadedColorCyan, 1.0f);
                    currentActiveMarkers = currentActiveMarkers.Select(marker =>
                    {
                        //apply new db value for marker
                        if (marker.position >= sampleA.Key && marker.position <= sampleB.Key)
                        {
                            marker.value = sampleB.Value;
                            marker.valueRef = valueRefB;
                        }
                        return marker;
                    }).ToArray();
                }
                if (tab_Marker.s_markers[tab_Marker.s_selectedMarker].isActive)
                {
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(new Vector2(left, top), new Vector2(right, bottom))
                                && s_waitForMouseClick.ElapsedMilliseconds > 100)
                    {
                        tab_Marker.s_markers[tab_Marker.s_selectedMarker].position = tab_Trace.getClosestSampeledFrequency(tab_Marker.s_markers[tab_Marker.s_selectedMarker].reference, mousePosFreq).Key;
                    }
                    if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        tab_Marker.s_markers[tab_Marker.s_selectedMarker].position = tab_Trace.findMaxHoldRange(tab_Trace.s_traces[x].plot, mouseRange.X, mouseRange.Y).Key;
                        s_waitForMouseClick.Restart();
                    }
                }

                for (int c = 0; c < currentActiveMarkers.Length; c++)
                {
                    var markerPosOnGraph = scaleToGraph(left, top, right, bottom, (float)currentActiveMarkers[c].position, (float)currentActiveMarkers[c].value, freqStart, freqStop, graph_startDB, graph_endDB);
                    draw.AddCircleFilled(markerPosOnGraph, 4f, traceColor_uint);
                    draw.AddCircle(markerPosOnGraph, 4.1f, ColorExtention.ToUint(Color.Black)); //outline
                    double markerValue = currentActiveMarkers[c].value;
                    double markerPosition = currentActiveMarkers[c].position;
                    double markerRefValue = currentActiveMarkers[c].valueRef;
                    double RL = markerRefValue - markerValue,
                        RC = Math.Pow(10, (markerValue - markerRefValue) / 20.0),
                        VSWR = (1.0 + RC) / (1.0 - RC),
                        mismatchLoss = -10 * Math.Log10(1 - Math.Pow(RC, 2));
                    currentActiveMarkers[c].txtStatus += $"Marker {c + 1} \n" +
                        $"Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}" +
                        $"\nReturn Loss: {RL.ToString().TruncateLongString(5)}dB" +
                        $"\nReflection Coefficient: {RC.ToString().TruncateLongString(5)}" +
                        $"\nVSWR: {VSWR.ToString().TruncateLongString(5)}\n" +
                        $"\nMismatch Loss {mismatchLoss.ToString().TruncateLongString(5)}";

                    string markerstatusText = currentActiveMarkers[c].txtStatus;
                    var textStatusSize = ImGui.CalcTextSize(markerstatusText);
                    draw.AddText(new Vector2(right + graphStatus.X - textStatusSize.X, top + graphStatus.Y), traceColor_uint, markerstatusText);
                    graphStatus.X -= textStatusSize.X + 5;
                    currentActiveMarkers[c].txtStatus = string.Empty; //clear
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Sequence"))
                    _logger.Trace($"Render Error -> {ex.Message} {ex.StackTrace}");
            }
        }
    }
}