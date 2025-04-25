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
                tab_Marker.s_markers[i].deltaReference = 0;
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
                    switch (tab_Trace.s_traces[i].dataStatus)
                    {
                        case tab_Trace.traceDataStatus.normal:
                            for (int k = 0; k < data[0].Length; k++)
                            {
                                if (plot.ContainsKey(data[1][k]))
                                    plot[data[1][k]] = data[0][k];
                                else
                                    plot.Add(data[1][k], data[0][k]);
                            }
                            break;

                        case tab_Trace.traceDataStatus.minHold:
                            for (int k = 0; k < data[0].Length; k++)
                            {
                                if (plot.ContainsKey(data[1][k]))
                                {
                                    if (plot[data[1][k]] > data[0][k])
                                        plot[data[1][k]] = data[0][k];
                                }
                                else
                                    plot.Add(data[1][k], data[0][k]);
                            }
                            break;

                        case tab_Trace.traceDataStatus.maxHold:
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
                            break;

                        case tab_Trace.traceDataStatus.Average:
                            for (int k = 0; k < data[0].Length; k++)
                            {
                                if (plot.ContainsKey(data[1][k]))
                                {
                                    plot[data[1][k]] = (plot[data[1][k]] * tab_Trace.s_traces[i].average + data[0][k]) / (tab_Trace.s_traces[i].average + 1);
                                }
                                else
                                    plot.Add(data[1][k], data[0][k]);
                            }
                            tab_Trace.s_traces[i].average++;
                            break;
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

        public static void calculateBandPower(tab_Marker.marker marker, List<float> dBArray)
        {
            if (_calculateBandPowerThread is not null)
                if (_calculateBandPowerThread.IsAlive) return; //Already in calculations return
            _calculateBandPowerThread = new Thread(() =>
            {
                double tempMarkerBandPowerDecimal = 0;
                foreach (float b in dBArray)
                {
                    tempMarkerBandPowerDecimal += ((double)b).toMW();
                }
                if (tempMarkerBandPowerDecimal != 0) //not enough values in dbArray --> log(0) --> overflow -inf
                    tab_Marker.s_markers[marker.id].bandPowerValue = tempMarkerBandPowerDecimal.toDBm();
            })
            { Priority = ThreadPriority.Lowest };
            _calculateBandPowerThread.Start();
        }

        public static Stopwatch s_waitForMouseClick = new Stopwatch();

        public static void drawGraph()
        {
            #region Canvas_Data

            var draw = ImGui.GetForegroundDrawList();
            var dbOffset = (double)Configuration.config[Configuration.saVar.graphOffsetDB];
            var refLevel = (double)Configuration.config[Configuration.saVar.graphRefLevel];

            float left = ImGui.GetWindowPos().X + Configuration.positionOffset.X;
            float right = left + Configuration.graphSize.X;
            float top = ImGui.GetWindowPos().Y + Configuration.positionOffset.Y;
            float bottom = top + Configuration.graphSize.Y;

            float graphLabelIdx = (float)tab_Amplitude.s_scalePerDivision;

            double freqStart = (double)Configuration.config[Configuration.saVar.freqStart];
            double freqStop = (double)Configuration.config[Configuration.saVar.freqStop];
            var mousePos = ImGui.GetMousePos();

            double graph_startDB = (double)Configuration.config[Configuration.saVar.graphStartDB] + refLevel;
            double graph_endDB = (double)Configuration.config[Configuration.saVar.graphStopDB] + refLevel;

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
                mousePosdB = (float)((graph_startDB - (bottom - mousePos.Y + top) / bottom * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset);
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
                text = Extentions.Imports.Scale(i, 0, graphLabelIdx, graph_endDB + dbOffset, graph_startDB + dbOffset).ToString().TruncateLongString(5);
                //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
                float posY = top + i / graphLabelIdx * Configuration.graphSize.Y;
                draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2), ColorExtention.ToUint(Color.LightGray), text);
                draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), ColorExtention.ToUint(Color.FromArgb(100, Color.Gray)));
            }

            #endregion backgroundDraw

            try
            {
                for (int x = 0; x < tab_Trace.s_traces.Length; x++)
                {
                    
                    if (tab_Trace.s_traces[x].viewStatus == tab_Trace.traceViewStatus.clear) continue;
                    var currentActiveMarkers = tab_Marker.s_markers.Where(d => d.reference == x && d.isActive).ToArray();
                    List<float> bandPowerDBList = new List<float>();
                    Color traceColor = Color.Yellow;
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

                        default:
                            break;
                    }

                    if (tab_Trace.s_traces[x].viewStatus == tab_Trace.traceViewStatus.view)
                        traceColor = Color.FromArgb(100, traceColor);
                    var plot = tab_Trace.s_traces[x].plot;
                    var traceColor_uint = ColorExtention.ToUint(traceColor);
                    var plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration

                    for (int i = 1; i < plotData.Length; i++)
                    {
                        var sampleA = plotData[i - 1];
                        var sampleB = plotData[i];

                        Vector2 sampleAPos = scaleToGraph(left, top, right, bottom, sampleA.Key, sampleA.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                        Vector2 sampleBPos = scaleToGraph(left, top, right, bottom, sampleB.Key, sampleB.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                        //bounds check
                        if (sampleBPos.X > right || sampleAPos.X < left) continue;
                        if (sampleAPos.Y < top || sampleBPos.Y < top || sampleAPos.Y > bottom || sampleBPos.Y > bottom)
                        {
                            if (!(bool)Configuration.config[Configuration.saVar.automaticLevel]) continue;
                            if (sampleAPos.Y < top || sampleBPos.Y < top)
                            {
                                Configuration.config[Configuration.saVar.graphStartDB] = (double)Math.Min(sampleA.Value, sampleB.Value);
                            }
                            else
                            {
                                Configuration.config[Configuration.saVar.graphStopDB] = (double)Math.Max(sampleA.Value, sampleB.Value);
                            }
                        }

                        draw.AddLine(sampleAPos, sampleBPos, traceColor_uint, 1.0f);
                        currentActiveMarkers = currentActiveMarkers.Select(marker =>
                        {
                            //apply new db value for marker
                            if (marker.position >= sampleA.Key && marker.position <= sampleB.Key)
                            {
                                tab_Marker.s_markers[marker.id].value = marker.value;
                                marker.value = sampleB.Value;
                            }

                            //apply bandPower List
                            if (marker.bandPower)
                                if (sampleA.Key >= (float)(marker.position - (marker.bandPowerSpan / 2)) && (sampleA.Key <= (float)(tab_Marker.s_markers[marker.id].position + (marker.bandPowerSpan / 2))))
                                {
                                    draw.AddLine(sampleAPos, sampleBPos, Color.White.ToUint(), 1.0f);
                                    bandPowerDBList.Add(sampleA.Value);
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
                        if (currentActiveMarkers[c].bandPower) calculateBandPower(currentActiveMarkers[c], bandPowerDBList);

                        var markerPosOnGraph = scaleToGraph(left, top, right, bottom, (float)currentActiveMarkers[c].position, (float)currentActiveMarkers[c].value, freqStart, freqStop, graph_startDB, graph_endDB);
                        draw.AddCircleFilled(markerPosOnGraph, 4f, traceColor_uint);
                        draw.AddCircle(markerPosOnGraph, 4.1f, ColorExtention.ToUint(Color.Black)); //outline
                        double markerValue = currentActiveMarkers[c].value;
                        double markerPosition = currentActiveMarkers[c].position;
                        if (currentActiveMarkers[c].deltaReference != 0)
                        {
                            var findReference = currentActiveMarkers.Where(x => x.id == currentActiveMarkers[c].deltaReference - 1);
                            if (findReference.Any())
                            {
                                markerValue = currentActiveMarkers[c].value - findReference.First().value;
                            }
                            markerPosition = currentActiveMarkers[c].position - tab_Marker.s_markers[currentActiveMarkers[c].deltaReference - 1].position;
                        }
                        currentActiveMarkers[c].txtStatus += $"Marker {c + 1} \n Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}M \n {(markerValue + dbOffset).ToString().TruncateLongString(5)} dB\n";
                        if (currentActiveMarkers[c].delta)
                        {
                            var deltaPosition = scaleToGraph(left, top, right, bottom, (float)currentActiveMarkers[c].DeltaFreq, (float)currentActiveMarkers[c].DeltadB, freqStart, freqStop, graph_startDB, graph_endDB);
                            var textSize = ImGui.CalcTextSize($"Delta Marker {c + 1}");

                            draw.AddLine(new Vector2(deltaPosition.X + 5, deltaPosition.Y), new Vector2(deltaPosition.X - 5, deltaPosition.Y), traceColor_uint);
                            draw.AddLine(new Vector2(deltaPosition.X, deltaPosition.Y + 5), new Vector2(deltaPosition.X, deltaPosition.Y - 5), traceColor_uint);
                            draw.AddText(new Vector2(deltaPosition.X - textSize.X / 2, deltaPosition.Y - (textSize.Y) - 2), Color.White.ToUint(), $"Delta Marker {c + 1}");
                            
                            var deltaDB = (currentActiveMarkers[c].value - currentActiveMarkers[c].DeltadB).ToString().TruncateLongString(5);
                            currentActiveMarkers[c].txtStatus += $"Delta \n Freq {((currentActiveMarkers[c].position - currentActiveMarkers[c].DeltaFreq) / 1e6).ToString().TruncateLongString(5)} Mhz \n {deltaDB} dB\n";
                        }
                        if (currentActiveMarkers[c].bandPower)
                        {
                            KeyValuePair<float, float> powerBandLeft = tab_Trace.getClosestSampeledFrequency(x, (float)(currentActiveMarkers[c].position - (currentActiveMarkers[c].bandPowerSpan / 2)));
                            KeyValuePair<float, float> powerBandRight = tab_Trace.getClosestSampeledFrequency(x, (float)(currentActiveMarkers[c].position + (currentActiveMarkers[c].bandPowerSpan / 2)));
                            var scaledPowerBandLeft = scaleToGraph(left, top, right, bottom, powerBandLeft.Key, powerBandLeft.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                            var scaledPowerBandRight = scaleToGraph(left, top, right, bottom, powerBandRight.Key, powerBandRight.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                            draw.AddLine(new Vector2(scaledPowerBandLeft.X, top), new Vector2(scaledPowerBandLeft.X, bottom), traceColor_uint);
                            draw.AddLine(new Vector2(scaledPowerBandRight.X, top), new Vector2(scaledPowerBandRight.X, bottom), traceColor_uint);
                            currentActiveMarkers[c].txtStatus += $"Band Power \n {(currentActiveMarkers[c].bandPowerValue + (double)dbOffset).ToString().TruncateLongString(5)} dB\n";
                        }
                        string markerstatusText = currentActiveMarkers[c].txtStatus;
                        var textStatusSize = ImGui.CalcTextSize(markerstatusText);
                        draw.AddText(new Vector2(right + graphStatus.X - textStatusSize.X, top + graphStatus.Y), traceColor_uint, markerstatusText);
                        graphStatus.X -= textStatusSize.X + 5;
                        currentActiveMarkers[c].txtStatus = string.Empty; //clear
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Sequence"))
                    _logger.Trace($"Render Error -> {ex.Message}");
            }
        }
    }
}