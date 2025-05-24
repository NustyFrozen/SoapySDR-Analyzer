using ImGuiNET;
using NLog;
using SoapyRL.Extentions;
using SoapyRL.View.tabs;
using System.Diagnostics;
using System.Numerics;

namespace SoapyRL.View;

public class Graph(MainWindow initiator)
{
    public MainWindow parent = initiator;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private Thread _calculateBandPowerThread;

    public Stopwatch s_waitForMouseClick = new();

    public void initializeGraphElements()
    {
        for (var i = 0; i < parent.tab_Trace.s_traces.Length; i++)
        {
            parent.tab_Trace.s_traces[i] = new tab_Trace.Trace();
            var color = Color.White.ToUint();
            switch (i)
            {
                case 1:
                    color = Color.LimeGreen.ToUint();
                    break;

                case 2:
                    color = Color.Red.ToUint();
                    break;
            }
            parent.tab_Trace.s_traces[i].color = color;
        }
        parent.tab_Marker.s_Marker = new tab_Marker.marker();
        parent.tab_Marker.s_Marker.id = 0;
        parent.tab_Trace.s_traces[0].viewStatus = tab_Trace.traceViewStatus.active;
        parent.tab_Marker.s_Marker.isActive = true;
    }

    public void clearPlotData()
    {
        for (var i = 0; i < parent.tab_Trace.s_traces.Length; i++)
        {
            if (parent.tab_Trace.s_traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
            var plot = parent.tab_Trace.s_traces[i].plot;
            plot.Clear();
        }
    }

    public void updateData(float[][] psd)
    {
        var data = psd.AsSpan();
        for (var i = 0; i < parent.tab_Trace.s_traces.Length; i++)
        {
            if (parent.tab_Trace.s_traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
            var plot = parent.tab_Trace.s_traces[i].plot;
            lock (plot)
            {
                for (var k = 0; k < data[0].Length; k++)
                    if (plot.ContainsKey(data[1][k]))
                    {
                        if (plot[data[1][k]] < data[0][k])
                            plot[data[1][k]] = data[0][k];
                    }
                    else
                    {
                        plot.Add(data[1][k], data[0][k]);
                    }
            }
        }
    }

    private Vector2 scaleToGraph(float left, float top, float right, float bottom, float freq, float dB,
        double freqStart, double freqStop, double graph_startDB, double graph_endDB)
    {
        var scale = freqStop - freqStart;
        var scale2 = freq - freqStart;
        var scaledX = left + parent.Configuration.graphSize.X * (scale2 / scale);
        //endb = 0

        var scaledY = Imports.Scale(dB, graph_startDB, graph_endDB, bottom, top);
        return new Vector2((float)scaledX, (float)scaledY);
    }

    public void drawGraph()
    {
        #region Canvas_Data

        var draw = ImGui.GetForegroundDrawList();

        var left = ImGui.GetWindowPos().X + parent.Configuration.positionOffset.X;
        var right = left + parent.Configuration.graphSize.X;
        var top = ImGui.GetWindowPos().Y + parent.Configuration.positionOffset.Y;
        var bottom = top + parent.Configuration.graphSize.Y;

        var graphLabelIdx = 20.0f;

        var freqStart = (double)parent.Configuration.config[Configuration.saVar.freqStart];
        var freqStop = (double)parent.Configuration.config[Configuration.saVar.freqStop];
        var mousePos = ImGui.GetMousePos();
        double graph_startDB = 100;
        double graph_endDB = 0;
        var graphStatus = new Vector2();
        draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(16, 16, 16).ToUint());
        var mouseRange = new Vector2();
        float mousePosFreq = 0, mousePosdB;

        #endregion Canvas_Data

        #region backgroundDraw

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
                                 (bottom - mousePos.Y + top) / bottom *
                                 (Math.Abs(graph_endDB) - Math.Abs(graph_startDB)));
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
            text = Imports.Scale(i, 0, graphLabelIdx, graph_endDB, graph_startDB).ToString().TruncateLongString(5);
            //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
            var posY = top + i / graphLabelIdx * parent.Configuration.graphSize.Y;
            draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                Color.LightGray.ToUint(), text);
            draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), Color.FromArgb(100, Color.Gray).ToUint());
        }

        #endregion backgroundDraw

        try
        {
            var data = parent.tab_Trace.s_traces[0].plot.ToArray();
            var referenceData = data.AsSpan();
            var minDB = (data.Length == 0) ? 0 : data.MinBy(x => x.Value).Value;
            var traceColor_uint = parent.tab_Trace.s_traces[0].color;
            var fadedColorYellow = parent.tab_Trace.s_traces[0].liteColor;
            for (var i = 1; i < referenceData.Length; i++)
            {
                var sampleA = referenceData[i - 1];
                var sampleADB = 0;
                var sampleB = referenceData[i];
                var sampleBDB = 0;
                var sampleAPos = scaleToGraph(left, top, right, bottom, sampleA.Key, sampleADB, freqStart, freqStop,
                    graph_startDB, graph_endDB);
                var sampleBPos = scaleToGraph(left, top, right, bottom, sampleB.Key, sampleBDB, freqStart, freqStop,
                    graph_startDB, graph_endDB);
                //bounds check
                if (sampleBPos.X > right || sampleAPos.X < left) continue;

                draw.AddLine(sampleAPos, sampleBPos, parent.tab_Trace.s_traces[0].color, 1.0f);

                var sampleAPosRef = scaleToGraph(left, top, right, bottom, sampleA.Key,
                    sampleA.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);
                var sampleBPosRef = scaleToGraph(left, top, right, bottom, sampleB.Key,
                    sampleB.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);

                draw.AddLine(sampleAPosRef, sampleBPosRef, parent.tab_Trace.s_traces[0].liteColor, 1.0f);
            }
            var impedanceTol = (float)parent.Configuration.config[Configuration.saVar.validImpedanceTol];
            var anntennaData = parent.tab_Trace.s_traces[1].plot.ToArray().AsSpan(); //asspan is fastest iteration
            double rangeStart = 0.0, RangeEnd = 0.0;
            var iscalculatingValidRange = false;
            List<Tuple<double, double>> validRanges = new List<Tuple<double, double>>();
            for (var i = 1; i < anntennaData.Length; i++)
            {
                var sampleA = anntennaData[i - 1];
                var sampleARL = referenceData[i - 1].Value - sampleA.Value;
                var sampleB = anntennaData[i];
                var valueRefB = referenceData[i].Value;
                var sampleBRL = valueRefB - sampleB.Value;

                var sampleAPos = scaleToGraph(left, top, right, bottom, sampleA.Key, sampleARL, freqStart, freqStop,
                    graph_startDB, graph_endDB);
                var sampleBPos = scaleToGraph(left, top, right, bottom, sampleB.Key, sampleBRL, freqStart, freqStop,
                    graph_startDB, graph_endDB);
                //bounds check
                if (sampleAPos.Y < top || sampleBPos.Y < top
                                       || sampleAPos.Y > bottom || sampleBPos.Y > bottom)
                {
                    sampleAPos.Y = sampleAPos.Y < top ? top : sampleAPos.Y > bottom ? bottom : sampleAPos.Y;
                    sampleBPos.Y = sampleBPos.Y < top ? top : sampleBPos.Y > bottom ? bottom : sampleBPos.Y;
                }

                var sampleAPosRef = scaleToGraph(left, top, right, bottom, sampleA.Key,
                    sampleA.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);
                var sampleBPosRef = scaleToGraph(left, top, right, bottom, sampleB.Key,
                    sampleB.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);
                if (parent.tab_Device.isShowValidRangeEnabled)
                {
                    double foward = 1 - ((double)-(valueRefB - sampleB.Value)).toMW();
                    if (foward >= impedanceTol)
                    {
                        if (iscalculatingValidRange)
                            RangeEnd = sampleB.Key;
                        else
                        {
                            iscalculatingValidRange = true;
                            rangeStart = sampleB.Key;
                            parent.tab_Trace.s_traces[1].color = Color.White.ToUint();
                        }
                    }
                    else
                    {
                        if (iscalculatingValidRange)
                        {
                            iscalculatingValidRange = false;
                            parent.tab_Trace.s_traces[1].color = Color.LimeGreen.ToUint();
                            validRanges.Add(new Tuple<double, double>(rangeStart, RangeEnd));
                        }
                    }
                    if (i + 1 > anntennaData.Length && iscalculatingValidRange)
                    {
                        validRanges.Add(new Tuple<double, double>(rangeStart, RangeEnd));
                        parent.tab_Trace.s_traces[1].color = Color.LimeGreen.ToUint();
                    }
                }
                draw.AddLine(sampleAPos, sampleBPos, parent.tab_Trace.s_traces[1].color, 1.0f);
                draw.AddLine(sampleAPosRef, sampleBPosRef, parent.tab_Trace.s_traces[1].liteColor, 1.0f);

                //apply new db value for marker
                if (parent.tab_Marker.s_Marker.position >= sampleA.Key && parent.tab_Marker.s_Marker.position <= sampleB.Key)
                {
                    parent.tab_Marker.s_Marker.value = sampleB.Value;
                    parent.tab_Marker.s_Marker.valueRef = valueRefB;
                }
            }

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(new Vector2(left, top),
                                                             new Vector2(right, bottom))
                                                         && s_waitForMouseClick.ElapsedMilliseconds > 100)
                parent.tab_Marker.s_Marker.position =
                    parent.tab_Trace.getClosestSampeledFrequency(parent.tab_Marker.s_Marker.reference, mousePosFreq).Key;
            if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                parent.tab_Marker.s_Marker.position =
                    parent.tab_Trace.findMaxHoldRange(parent.tab_Trace.s_traces[1].plot, mouseRange.X, mouseRange.Y).Key;
                s_waitForMouseClick.Restart();
            }

            var markerValue = parent.tab_Marker.s_Marker.value;
            var markerPosition = parent.tab_Marker.s_Marker.position;
            var markerRefValue = parent.tab_Marker.s_Marker.valueRef;

            double RL = markerRefValue - markerValue,
                RC = Math.Pow(10, (markerValue - markerRefValue) / 20.0),
                VSWR = (1.0 + RC) / (1.0 - RC),
                mismatchLoss = -10 * Math.Log10(1 - Math.Pow(RC, 2));
            double reflected = (-RL).toMW();
            double forwarded = 1 - reflected;
            var markerPosOnGraph = scaleToGraph(left, top, right, bottom, (float)parent.tab_Marker.s_Marker.position,
                (float)RL, freqStart, freqStop, graph_startDB, graph_endDB);
            draw.AddCircleFilled(markerPosOnGraph, 4f, parent.tab_Trace.s_traces[1].color);
            draw.AddCircle(markerPosOnGraph, 4.1f, Color.Black.ToUint()); //outline

            parent.tab_Marker.s_Marker.txtStatus += $"Marker\n" +
                                             $"Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}" +
                                             $"\nReturn Loss: {RL.ToString().TruncateLongString(5)}dB" +
                                             $"\nReflection Coefficient: {RC.ToString().TruncateLongString(5)}" +
                                             $"\nVSWR: {VSWR.ToString().TruncateLongString(5)}" +
                                             $"\nMismatch Loss {mismatchLoss.ToString().TruncateLongString(5)}" +
                                             $"\nForward {(forwarded * 100).ToString().TruncateLongString(5)}% reflected {(reflected * 100).ToString().TruncateLongString(5)}%";
            var markerstatusText = parent.tab_Marker.s_Marker.txtStatus;
            var textStatusSize = ImGui.CalcTextSize(markerstatusText);
            draw.AddText(new Vector2(left + parent.Configuration.graphSize.X / 2 - textStatusSize.X / 2,
                                    bottom - graphStatus.Y - textStatusSize.Y), parent.tab_Trace.s_traces[1].color,
                markerstatusText);
            parent.tab_Marker.s_Marker.txtStatus = string.Empty; //clear
            if (parent.tab_Device.isShowValidRangeEnabled)
            {
                var text = "Valid Ranges\n";
                foreach (var range in validRanges)
                    text += $"{range.Item1.ToString()} - {range.Item2.ToString()}\n";
                var textSize = ImGui.CalcTextSize(text);
                draw.AddText(new Vector2(left,
                                    bottom - graphStatus.Y - textSize.Y), parent.tab_Trace.s_traces[1].color, text);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Render Error -> {ex.Message} {ex.StackTrace}");
        }
    }
}