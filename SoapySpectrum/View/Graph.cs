using ImGuiNET;
using SoapySpectrum.Extentions;
using System.Diagnostics;
using System.Numerics;

namespace SoapySpectrum.UI
{
    public static class Graph
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();


        public static void initializeGraphElements()
        {
            for (int i = 0; i < tab_Trace.traces.Length; i++)
            {
                tab_Trace.traces[i] = new tab_Trace.trace();
            }
            for (int i = 0; i < tab_Marker.markers.Length; i++)
            {
                tab_Marker.markers[i] = new tab_Marker.marker();
                tab_Marker.markers[i].deltaReference = 0;
                tab_Marker.markers[i].id = i;
            }
            tab_Marker.markerReferences = tab_Trace.Combotraces;
            tab_Trace.traces[0].viewStatus = tab_Trace.traceViewStatus.active;
        }
        public static void clearPlotData()
        {

            for (int i = 0; i < tab_Trace.traces.Length; i++)
            {
                if (tab_Trace.traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
                var plot = tab_Trace.traces[i].plot;
                plot.Clear();
            }
        }

        public static void updateData(float[][] data)
        {
            for (int i = 0; i < tab_Trace.traces.Length; i++)
            {
                if (tab_Trace.traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
                var plot = tab_Trace.traces[i].plot;
                lock (plot)
                {
                    switch (tab_Trace.traces[i].dataStatus)
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

                                    plot[data[1][k]] = (plot[data[1][k]] * tab_Trace.traces[i].average + data[0][k]) / (tab_Trace.traces[i].average + 1);
                                }
                                else
                                    plot.Add(data[1][k], data[0][k]);

                            }
                            tab_Trace.traces[i].average++;
                            break;
                    }
                }

            }
        }
        static Vector2 scaleToGraph(float left, float top, float right, float bottom, float freq, float dB, double freqStart, double freqStop, double graph_startDB, double graph_endDB)
        {

            double scale = freqStop - freqStart;
            double scale2 = freq - freqStart;
            double scaledX = left + Configuration.graphSize.X * (scale2 / scale);
            //endb = 0


            var scaledY = Extentions.Imports.Scale(dB, graph_startDB, graph_endDB, bottom, top);
            return new Vector2((float)scaledX, (float)scaledY);
        }
        static Thread calculateBandPowerThread;
        public static void calculateBandPower(tab_Marker.marker marker, List<float> dBArray)
        {
            if(calculateBandPowerThread is not null)
            if (calculateBandPowerThread.IsAlive) return; //Already in calculations return
            calculateBandPowerThread = new Thread(() =>
            {
                double tempMarkerBandPowerDecimal = 0;
                foreach (float b in dBArray)
                {
                    tempMarkerBandPowerDecimal += ((double)b).toMW();
                }
                if (tempMarkerBandPowerDecimal != 0) //not enough values in dbArray --> log(0) --> overflow -inf
                    tab_Marker.markers[marker.id].bandPowerValue = tempMarkerBandPowerDecimal.toDBm();
            })
            { Priority = ThreadPriority.Lowest };
            calculateBandPowerThread.Start();
        }
        public static Stopwatch waitForMouseClick = new Stopwatch();

        public static void drawGraph()
        {
            #region Canvas_Data
            var draw = ImGui.GetForegroundDrawList();
            var dbOffset = (double)Configuration.config[Configuration.saVar.graphOffsetDB];
            var refLevel = (double)Configuration.config[Configuration.saVar.graphRefLevel];
            var positionOffset = new Vector2(50 * Configuration.scaleSize.X, 10 * Configuration.scaleSize.Y);
            float left = ImGui.GetWindowPos().X + positionOffset.X;
            float right = left + Configuration.graphSize.X;
            float top = ImGui.GetWindowPos().Y + positionOffset.Y;
            float bottom = top + Configuration.graphSize.Y;

            float graphLabelIdx = (float)tab_Amplitude.scalePerDivision;
            float ratioX = graphLabelIdx / Configuration.graphSize.X;

            double freqStart = (double)Configuration.config[Configuration.saVar.freqStart];
            double freqStop = (double)Configuration.config[Configuration.saVar.freqStop];
            var mousePos = ImGui.GetMousePos();

            double graph_startDB = (double)Configuration.config[Configuration.saVar.graphStartDB] + refLevel;
            double graph_endDB = (double)Configuration.config[Configuration.saVar.graphStopDB] + refLevel;

            Vector2 graphStatus = new Vector2();
            draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), ColorExtention.ToUint(Color.FromArgb(16, 16, 16)));
            Vector2 mouseRange = new Vector2();
            float mousePosFreq = 0, mousePosdB;
            #endregion
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
            #endregion

            try
            {
                for (int x = 0; x < tab_Trace.traces.Length; x++)
                {
                    var tracetab_Marker = tab_Marker.markers.Where(d => d.reference == x && d.active).ToArray();
                    List<float> bandpowerdB = new List<float>();
                    if (tab_Trace.traces[x].viewStatus == tab_Trace.traceViewStatus.clear) continue;
                    var floor = tab_Trace.getFloordB(x);
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

                    if (tab_Trace.traces[x].viewStatus == tab_Trace.traceViewStatus.view)
                        traceColor = Color.FromArgb(100, traceColor);
                    var plot = tab_Trace.traces[x].plot;
                    var uintTraceColor = ColorExtention.ToUint(traceColor);
                    KeyValuePair<float, float>[] plotData = plot.ToArray();

                    for (int i = 1; i < plotData.Length; i++)
                    {

                        var sample1 = plotData[i - 1];
                        var sample2 = plotData[i];

                        Vector2 firstPoint = scaleToGraph(left, top, right, bottom, sample1.Key, sample1.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                        Vector2 secondPoint = scaleToGraph(left, top, right, bottom, sample2.Key, sample2.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                        if (secondPoint.X > right || firstPoint.X < left) continue; //out of bounds
                        if (firstPoint.Y < top || secondPoint.Y < top || firstPoint.Y > bottom || secondPoint.Y > bottom)
                        {
                            if (!(bool)Configuration.config[Configuration.saVar.automaticLevel]) continue;
                            if (firstPoint.Y < top || secondPoint.Y < top)
                            {
                                Configuration.config[Configuration.saVar.graphStartDB] = (double)Math.Min(sample1.Value, sample2.Value);

                            }
                            else
                            {
                                Configuration.config[Configuration.saVar.graphStopDB] = (double)Math.Max(sample1.Value, sample2.Value);
                            }
                        }
                        draw.AddLine(firstPoint, secondPoint, uintTraceColor, 1.0f);
                        for (int c = 0; c < tracetab_Marker.Length; c++)
                        {

                            if (tracetab_Marker[c].position >= sample1.Key && tracetab_Marker[c].position <= sample2.Key)
                            {
                                tracetab_Marker[c].value = sample2.Value;
                            }
                            if (tracetab_Marker[c].bandPower)
                                if (sample1.Key >= (float)(tracetab_Marker[c].position - (tracetab_Marker[c].bandPowerSpan / 2)) && (sample1.Key <= (float)(tab_Marker.markers[c].position + (tracetab_Marker[c].bandPowerSpan / 2))))
                                {
                                    draw.AddLine(firstPoint, secondPoint, Color.White.ToUint(), 1.0f);
                                    bandpowerdB.Add(sample1.Value);
                                }

                        }
                    }
                    if (tab_Marker.markers[tab_Marker.selectedMarker].active)
                    {
                        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(new Vector2(left, top), new Vector2(right, bottom))
                                    && waitForMouseClick.ElapsedMilliseconds > 100)
                        {
                            tab_Marker.markers[tab_Marker.selectedMarker].position = tab_Trace.getClosestSampeledFrequency(tab_Marker.markers[tab_Marker.selectedMarker].reference, mousePosFreq).Key;
                        }
                        if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            tab_Marker.markers[tab_Marker.selectedMarker].position = tab_Trace.findMaxHoldRange(tab_Trace.traces[x].plot, mouseRange.X, mouseRange.Y).Key;
                            waitForMouseClick.Restart();

                        }
                    }

                    for (int c = 0; c < tracetab_Marker.Length; c++)
                    {
                        if (tracetab_Marker[c].bandPower) calculateBandPower(tracetab_Marker[c], bandpowerdB);


                        var markerPosOnGraph = scaleToGraph(left, top, right, bottom, (float)tracetab_Marker[c].position, (float)tracetab_Marker[c].value, freqStart, freqStop, graph_startDB, graph_endDB);
                        draw.AddCircleFilled(markerPosOnGraph, 4f, ColorExtention.ToUint(Color.FromArgb(255, traceColor)));
                        draw.AddCircle(markerPosOnGraph, 4.1f, ColorExtention.ToUint(Color.Black)); //outline
                        double markerValue = tracetab_Marker[c].value;
                        double markerPosition = tracetab_Marker[c].position;
                        if (tracetab_Marker[c].deltaReference != 0)
                        {
                            var findReference = tracetab_Marker.Where(x => x.id == tracetab_Marker[c].deltaReference - 1);
                            if (findReference.Any())
                            {
                                markerValue = tracetab_Marker[c].value - findReference.First().value;
                            }
                            else
                            {
                                markerValue = 0;
                            }
                            markerPosition = tracetab_Marker[c].position - tab_Marker.markers[tracetab_Marker[c].deltaReference - 1].position;
                        }
                        tracetab_Marker[c].txtStatus += $"Marker {c + 1} \n Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}M \n {(markerValue + dbOffset).ToString().TruncateLongString(5)} dB\n";
                        if (tracetab_Marker[c].delta)
                        {

                            var deltaPosition = scaleToGraph(left, top, right, bottom, (float)tracetab_Marker[c].DeltaFreq, (float)tracetab_Marker[c].DeltadB, freqStart, freqStop, graph_startDB, graph_endDB);
                            var textSize = ImGui.CalcTextSize($"Delta Marker {c + 1}");

                            draw.AddLine(new Vector2(deltaPosition.X + 5, deltaPosition.Y), new Vector2(deltaPosition.X - 5, deltaPosition.Y), ColorExtention.ToUint(Color.FromArgb(255, traceColor)));
                            draw.AddLine(new Vector2(deltaPosition.X, deltaPosition.Y + 5), new Vector2(deltaPosition.X, deltaPosition.Y - 5), ColorExtention.ToUint(Color.FromArgb(255, traceColor)));
                            draw.AddText(new Vector2(deltaPosition.X - textSize.X / 2, deltaPosition.Y - (textSize.Y) - 2), Color.White.ToUint(), $"Delta Marker {c + 1}");


                            var deltaDB = (tracetab_Marker[c].value - tracetab_Marker[c].DeltadB).ToString().TruncateLongString(5);
                            tracetab_Marker[c].txtStatus += $"Delta \n Freq {((tracetab_Marker[c].position - tracetab_Marker[c].DeltaFreq) / 1e6).ToString().TruncateLongString(5)} Mhz \n {deltaDB} dB\n";
                        }
                        if (tracetab_Marker[c].bandPower)
                        {
                            KeyValuePair<float, float> powerBandLeft = tab_Trace.getClosestSampeledFrequency(x, (float)(tracetab_Marker[c].position - (tracetab_Marker[c].bandPowerSpan / 2)));
                            KeyValuePair<float, float> powerBandRight = tab_Trace.getClosestSampeledFrequency(x, (float)(tracetab_Marker[c].position + (tracetab_Marker[c].bandPowerSpan / 2)));
                            var scaledPowerBandLeft = scaleToGraph(left, top, right, bottom, powerBandLeft.Key, powerBandLeft.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                            var scaledPowerBandRight = scaleToGraph(left, top, right, bottom, powerBandRight.Key, powerBandRight.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                            draw.AddLine(new Vector2(scaledPowerBandLeft.X, top), new Vector2(scaledPowerBandLeft.X, bottom), ColorExtention.ToUint(Color.FromArgb(200, traceColor)));
                            draw.AddLine(new Vector2(scaledPowerBandRight.X, top), new Vector2(scaledPowerBandRight.X, bottom), ColorExtention.ToUint(Color.FromArgb(200, traceColor)));
                            tracetab_Marker[c].txtStatus += $"Band Power \n {(tracetab_Marker[c].bandPowerValue + (double)dbOffset).ToString().TruncateLongString(5)} dB\n";
                        }
                        string markerstatusText = tracetab_Marker[c].txtStatus;
                        var textStatusSize = ImGui.CalcTextSize(markerstatusText);
                        draw.AddText(new Vector2(right + graphStatus.X - textStatusSize.X, top + graphStatus.Y), uintTraceColor, markerstatusText);
                        graphStatus.X -= textStatusSize.X + 5;
                        tracetab_Marker[c].txtStatus = string.Empty; //clear
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("Sequence"))
                    Logger.Trace($"Render Error -> {ex.Message}");
            }
        }
    }
}
