using ClickableTransparentOverlay;
using ImGuiNET;
using SoapySpectrum.Extentions;
using SoapySpectrum.Extentions.Design_imGUINET;
using System.Diagnostics;
using System.Numerics;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        public enum traceViewStatus
        {
            active, clear, view
        }
        public enum traceDataStatus
        {
            normal, Average, maxHold, minHold
        }
        public struct marker
        {
            public string bandPower_Span_str;
            public double bandPowerSpan;
            public marker()
            {
                bandPower_Span_str = "5M";
            }
            public string txtStatus;
            public bool active;
            public bool delta;
            public bool calculatingBandPower;
            public bool bandPower;
            public decimal bandPowerValue;
            public double position;
            public double DeltaFreq;
            public double DeltadB;
            public double freqLeft;
            public double freqRight;
        }
        public struct trace
        {
            public marker marker;
            public int average;
            private traceDataStatus datastatus;
            public trace()
            {
                plot = new SortedDictionary<float, float>();
                dataStatus = traceDataStatus.normal;
                average = 1;
                viewStatus = traceViewStatus.clear;
            }
            public traceDataStatus dataStatus   // property
            {
                get
                {
                    return datastatus;
                }   // get method
                set
                {
                    average = 1;
                    datastatus = value;
                    plot.Clear();
                }  // set method
            }
            public traceViewStatus viewStatus;
            public SortedDictionary<float, float> plot;
        }
        static trace[] traces = new trace[3];
        public static void initializeTraces()
        {
            for (int i = 0; i < traces.Length; i++)
            {
                traces[i] = new trace();
                traces[i].marker = new marker();
            }
            traces[0].viewStatus = traceViewStatus.active;
        }
        public static void clearPlotData()
        {

            for (int i = 0; i < traces.Length; i++)
            {
                if (traces[i].viewStatus != traceViewStatus.active) continue;
                var plot = traces[i].plot;
                plot.Clear();
            }
        }
        public static KeyValuePair<float, float> getClosestSampeledFrequency(int traceID, float Mhz)
        {
            lock (traces[traceID].plot)
                return traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key - Mhz));
        }
        public static float getFloordB(int traceID)
        {
            lock (traces[traceID].plot)
                return traces[traceID].plot.MinBy(x => Math.Abs((long)x.Key)).Value;
        }
        public static void updateData(Dictionary<float, float> data) //called by soapyPower
        {
            for (int i = 0; i < traces.Length; i++)
            {
                if (traces[i].viewStatus != traceViewStatus.active) continue;
                var plot = traces[i].plot;
                lock (plot)
                {
                    switch (traces[i].dataStatus)
                    {
                        case traceDataStatus.normal:
                            plot.AddRangeOverride(data);
                            break;
                        case traceDataStatus.minHold:
                            foreach (KeyValuePair<float, float> dataPoint in data)
                            {
                                if (plot.ContainsKey(dataPoint.Key))
                                {
                                    if (plot[dataPoint.Key] > dataPoint.Value)
                                        plot[dataPoint.Key] = dataPoint.Value;
                                }
                                else
                                    plot.Add(dataPoint.Key, dataPoint.Value);

                            }
                            break;
                        case traceDataStatus.maxHold:
                            foreach (KeyValuePair<float, float> dataPoint in data)
                            {
                                if (plot.ContainsKey(dataPoint.Key))
                                {
                                    if (plot[dataPoint.Key] < dataPoint.Value)
                                        plot[dataPoint.Key] = dataPoint.Value;
                                }
                                else
                                    plot.Add(dataPoint.Key, dataPoint.Value);

                            }
                            break;
                        case traceDataStatus.Average:
                            foreach (KeyValuePair<float, float> dataPoint in data)
                            {
                                if (plot.ContainsKey(dataPoint.Key))
                                {

                                    plot[dataPoint.Key] = (plot[dataPoint.Key] * traces[i].average + dataPoint.Value) / (traces[i].average + 1);
                                }
                                else
                                    plot.Add(dataPoint.Key, dataPoint.Value);

                            }
                            traces[i].average++;
                            break;
                    }
                }

            }
        }
        static Vector2 scaleToGraph(float left, float top, float right, float bottom, float freq, float dB, double freqStart, double freqStop, double graph_startDB, double graph_endDB)
        {

            double scale = freqStop - freqStart;
            double scale2 = freq - freqStart;
            double scaledX = left + Configuration.graph_Size.X * (scale2 / scale);
            //endb = 0


            var scaledY = Scale(dB, graph_startDB, graph_endDB, bottom, top);
            return new Vector2((float)scaledX, (float)scaledY);
        }
        public static void calculateBandPower(int traceID, List<float> dBArray)
        {
            /*
            calculating band power require to change db values to watt and calculate with high significant digits (in this case decimal 26-27~ significant)
            and since its very big numbers it takes a lot of time to calculate log and pow so we use a different thread to not make the renderer stuck
            and calculate less often
            this method opens at most 3 more threads (as many as the amount of traces)
            */
            if (traces[traceID].marker.calculatingBandPower) return; //Already in calculations return
            traces[traceID].marker.calculatingBandPower = true;
            new Thread(() =>
            {
                decimal tempMarkerBandPowerDecimal = 0;
                foreach (float b in dBArray)
                {
                    tempMarkerBandPowerDecimal = tempMarkerBandPowerDecimal + ((decimal)b).toMW();
                    Thread.Sleep(1); //this thread is not important, telling system to focus more on stuff like rendering
                }
                if (tempMarkerBandPowerDecimal == 0) //not enough values in dbArray --> log(0) --> overflow -inf
                {
                    traces[traceID].marker.calculatingBandPower = false;
                    return;
                }
                traces[traceID].marker.bandPowerValue = tempMarkerBandPowerDecimal.toDBm();
                traces[traceID].marker.calculatingBandPower = false; //allowing to recalculate that trace
            })
            { Priority = ThreadPriority.Lowest }.Start();
        }
        static Stopwatch waitForMouseClick = new Stopwatch();
        public static double Scale(double value, double oldMin, double oldMax, double newMin, double newMax)
        {
            return newMin + (value - oldMin) * (newMax - newMin) / (oldMax - oldMin);
        }
        public static void drawGraph()
        {
            var draw = ImGui.GetForegroundDrawList();
            var dbOffset = (double)Configuration.config["graph_OffsetDB"];
            var refLevel = (double)Configuration.config["graph_RefLevel"];
            var positionOffset = new Vector2(50 * Configuration.scale_Size.X, 10 * Configuration.scale_Size.Y);
            float left = ImGui.GetWindowPos().X + positionOffset.X;
            float right = left + Configuration.graph_Size.X;
            float top = ImGui.GetWindowPos().Y + positionOffset.Y;
            float bottom = top + Configuration.graph_Size.Y;

            float graphLabelIdx = 20;
            float ratioX = graphLabelIdx / Configuration.graph_Size.X;

            double freqStart = (double)Configuration.config["freqStart"];
            double freqStop = (double)Configuration.config["freqStop"];
            var mousePos = ImGui.GetMousePos();

            double graph_startDB = (double)Configuration.config["graph_startDB"] + refLevel;
            double graph_endDB = (double)Configuration.config["graph_endDB"] + refLevel;

            Vector2 graphStatus = new Vector2();
            draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), ToUint(Color.FromArgb(16, 16, 16)));
            Vector2 mouseRange = new Vector2();
            float mousePosFreq = 0, mousePosdB;
            if (new RectangleF(left, top, Configuration.graph_Size.X, Configuration.graph_Size.Y).Contains(mousePos.X, mousePos.Y))
            {
                draw.AddLine(new Vector2(left, mousePos.Y), new Vector2(right, mousePos.Y), Color.FromArgb(100, 100, 100).ToUint());
                draw.AddLine(new Vector2(mousePos.X, top), new Vector2(mousePos.X, bottom), Color.FromArgb(100, 100, 100).ToUint());

                mousePosFreq = (float)((freqStart + ((mousePos.X - left) / (Configuration.graph_Size.X)) * (freqStop - freqStart)));
                mousePosdB = (float)((graph_startDB - (bottom - mousePos.Y + top) / bottom * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset);
                mouseRange.X = (float)(freqStart + (freqStop - freqStart));
                mouseRange.Y = (float)(freqStart + (freqStop - freqStart));
                draw.AddText(new Vector2(mousePos.X + 5, mousePos.Y + 5), Color.FromArgb(100, 100, 100).ToUint(), $"Freq {(mousePosFreq / 1e6).ToString().TruncateLongString(5)}M\ndBm {mousePosdB}");
            }
            for (float i = 0; i <= graphLabelIdx; i++)
            {
                //draw X axis
                string text = $"{(freqStart + i / graphLabelIdx * (freqStop - freqStart)) / 1e6}".TruncateLongString(5);
                text += "M";
                float posX = left + i / graphLabelIdx * Configuration.graph_Size.X - ImGui.CalcTextSize(text).X / 2;
                draw.AddText(new Vector2(posX, bottom), ToUint(Color.LightGray), text);


                draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, bottom), new Vector2(posX + ImGui.CalcTextSize(text).X / 2, top), ToUint(Color.FromArgb(100, Color.Gray)));

                //draw Y axis
                text = Scale(i, 0, graphLabelIdx, graph_endDB + dbOffset, graph_startDB + dbOffset).ToString().TruncateLongString(5);
                //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
                float posY = top + i / graphLabelIdx * Configuration.graph_Size.Y;
                draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2), ToUint(Color.LightGray), text);
                draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), ToUint(Color.FromArgb(100, Color.Gray)));

            }
            try
            {
                for (int x = 0; x < traces.Length; x++)
                {
                    float tempMarkerDB = 0;
                    List<float> bandpowerdB = new List<float>();
                    if (traces[x].viewStatus == traceViewStatus.clear) continue;
                    var floor = getFloordB(x);
                    Color traceColor = Color.Yellow;
                    if (x == 1) traceColor = Color.FromArgb(0, 255, 255);
                    if (x == 2) traceColor = Color.FromArgb(255, 0, 255);
                    if (traces[x].viewStatus == traceViewStatus.view)
                        traceColor = Color.FromArgb(100, traceColor);
                    var plot = traces[x].plot;
                    var uintTraceColor = ToUint(traceColor);
                    KeyValuePair<float, float>[] plotData = plot.ToArray();

                    for (int i = 1; i < plotData.Length; i++)
                    {

                        var sample1 = plotData[i - 1];
                        var sample2 = plotData[i];

                        Vector2 firstPoint = scaleToGraph(left, top, right, bottom, sample1.Key, sample1.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                        Vector2 secondPoint = scaleToGraph(left, top, right, bottom, sample2.Key, sample2.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                        if (secondPoint.X > right || firstPoint.X < left) continue; //out of bounds
                        if (firstPoint.Y < top || secondPoint.Y < top || firstPoint.Y > bottom || secondPoint.Y > bottom) continue; //revaya
                        draw.AddLine(firstPoint, secondPoint, uintTraceColor, 1.0f);
                        if (traces[x].marker.active)
                        {
                            if (tempMarkerDB == 0 && traces[x].marker.position >= sample1.Key && traces[x].marker.position <= sample2.Key)
                            {
                                tempMarkerDB = sample2.Value;
                            }
                            if (traces[x].marker.bandPower)
                                if (sample1.Key >= (float)(traces[x].marker.position - (traces[x].marker.bandPowerSpan / 2)) && (sample1.Key <= (float)(traces[x].marker.position + (traces[x].marker.bandPowerSpan / 2))))
                                {
                                    draw.AddLine(firstPoint, secondPoint, Color.White.ToUint(), 1.0f);
                                    bandpowerdB.Add(sample1.Value);
                                }
                        }
                    }
                    if (traces[x].marker.active)
                    {
                        if (traces[x].marker.bandPower) calculateBandPower(x, bandpowerdB);
                        if (x == selectedTrace)
                        {
                            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(new Vector2(left, top), new Vector2(right, bottom))
                                && waitForMouseClick.ElapsedMilliseconds > 100)
                            {
                                traces[x].marker.position = getClosestSampeledFrequency(x, mousePosFreq).Key;
                            }
                            if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                traces[x].marker.position = findMaxHoldRange(traces[x].plot, mouseRange.X, mouseRange.Y).Key;
                                waitForMouseClick.Restart();

                            }
                        }
                        var markerPosOnGraph = scaleToGraph(left, top, right, bottom, (float)traces[x].marker.position, tempMarkerDB, freqStart, freqStop, graph_startDB, graph_endDB);
                        draw.AddCircleFilled(markerPosOnGraph, 4f, ToUint(Color.FromArgb(255, traceColor)));
                        draw.AddCircle(markerPosOnGraph, 4.1f, ToUint(Color.Black)); //outline
                        traces[x].marker.txtStatus += $"Marker {x + 1} \n Freq {(traces[x].marker.position / 1e6).ToString().TruncateLongString(5)}M \n {(tempMarkerDB + dbOffset).ToString().TruncateLongString(5)} dB\n";
                        if (traces[x].marker.delta)
                        {
                            var deltaPosition = scaleToGraph(left, top, right, bottom, (float)traces[x].marker.DeltaFreq, (float)traces[x].marker.DeltadB, freqStart, freqStop, graph_startDB, graph_endDB);
                            var textSize = ImGui.CalcTextSize($"Delta Marker {x + 1}");
                            draw.AddLine(new Vector2(deltaPosition.X + 5, deltaPosition.Y), new Vector2(deltaPosition.X - 5, deltaPosition.Y), ToUint(Color.FromArgb(255, traceColor)));
                            draw.AddLine(new Vector2(deltaPosition.X, deltaPosition.Y + 5), new Vector2(deltaPosition.X, deltaPosition.Y - 5), ToUint(Color.FromArgb(255, traceColor)));
                            draw.AddText(new Vector2(deltaPosition.X - textSize.X / 2, deltaPosition.Y - (textSize.Y) - 2), Color.White.ToUint(), $"Delta Marker {x + 1}");
                            var deltaDB = (tempMarkerDB - traces[x].marker.DeltadB).ToString().TruncateLongString(5);
                            traces[x].marker.txtStatus += $"Delta \n Freq {((traces[x].marker.DeltaFreq - traces[x].marker.position) / 1e6).ToString().TruncateLongString(5)} Mhz \n {deltaDB} dB\n";
                        }
                        if (traces[x].marker.bandPower)
                        {
                            KeyValuePair<float, float> powerBandLeft = getClosestSampeledFrequency(x, (float)(traces[x].marker.position - (traces[x].marker.bandPowerSpan / 2)));
                            KeyValuePair<float, float> powerBandRight = getClosestSampeledFrequency(x, (float)(traces[x].marker.position + (traces[x].marker.bandPowerSpan / 2)));
                            var scaledPowerBandLeft = scaleToGraph(left, top, right, bottom, powerBandLeft.Key, powerBandLeft.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                            var scaledPowerBandRight = scaleToGraph(left, top, right, bottom, powerBandRight.Key, powerBandRight.Value, freqStart, freqStop, graph_startDB, graph_endDB);
                            draw.AddLine(new Vector2(scaledPowerBandLeft.X, top), new Vector2(scaledPowerBandLeft.X, bottom), ToUint(Color.FromArgb(200, traceColor)));
                            draw.AddLine(new Vector2(scaledPowerBandRight.X, top), new Vector2(scaledPowerBandRight.X, bottom), ToUint(Color.FromArgb(200, traceColor)));
                            traces[x].marker.txtStatus += $"Band Power \n {(traces[x].marker.bandPowerValue + (decimal)dbOffset).ToString().TruncateLongString(5)} dB\n";
                        }
                        string markerStatusText = traces[x].marker.txtStatus;
                        var textStatusSize = ImGui.CalcTextSize(markerStatusText);
                        draw.AddText(new Vector2(right - textStatusSize.X, top + graphStatus.Y), uintTraceColor, markerStatusText);
                        graphStatus.Y += textStatusSize.Y;
                        traces[x].marker.txtStatus = string.Empty; //clear
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
