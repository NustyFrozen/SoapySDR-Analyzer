using ImGuiNET;
using NLog;
using SoapyRL.Extentions;
using SoapyRL.View.tabs;
using System.Diagnostics;
using System.Numerics;

namespace SoapyRL.View;

public static class Graph
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static Thread _calculateBandPowerThread;

    public static Stopwatch s_waitForMouseClick = new();

    public static void initializeGraphElements()
    {
        for (var i = 0; i < tab_Trace.s_traces.Length; i++) tab_Trace.s_traces[i] = new tab_Trace.Trace();
        tab_Marker.s_Marker = new tab_Marker.marker();
        tab_Marker.s_Marker.id = 0;
        tab_Trace.s_traces[0].viewStatus = tab_Trace.traceViewStatus.active;
        tab_Marker.s_Marker.isActive = true;
    }

    public static void clearPlotData()
    {
        for (var i = 0; i < tab_Trace.s_traces.Length; i++)
        {
            if (tab_Trace.s_traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
            var plot = tab_Trace.s_traces[i].plot;
            plot.Clear();
        }
    }

    public static void updateData(float[][] psd)
    {
        var data = psd.AsSpan();
        for (var i = 0; i < tab_Trace.s_traces.Length; i++)
        {
            if (tab_Trace.s_traces[i].viewStatus != tab_Trace.traceViewStatus.active) continue;
            var plot = tab_Trace.s_traces[i].plot;
            lock (plot)
            {
                for (var k = 0; k < data[0].Length; k++)
                    if (plot.ContainsKey(data[1][k]))
                        plot[data[1][k]] = data[0][k];
                    else
                        plot.Add(data[1][k], data[0][k]);
            }
        }
    }

    private static Vector2 scaleToGraph(float left, float top, float right, float bottom, float freq, float dB,
        double freqStart, double freqStop, double graph_startDB, double graph_endDB)
    {
        var scale = freqStop - freqStart;
        var scale2 = freq - freqStart;
        var scaledX = left + Configuration.graphSize.X * (scale2 / scale);
        //endb = 0

        var scaledY = Imports.Scale(dB, graph_startDB, graph_endDB, bottom, top);
        return new Vector2((float)scaledX, (float)scaledY);
    }

    public static void drawGraph()
    {
        #region Canvas_Data

        var draw = ImGui.GetForegroundDrawList();

        var left = ImGui.GetWindowPos().X + Configuration.positionOffset.X;
        var right = left + Configuration.graphSize.X;
        var top = ImGui.GetWindowPos().Y + Configuration.positionOffset.Y;
        var bottom = top + Configuration.graphSize.Y;

        var graphLabelIdx = 20.0f;

        var freqStart = (double)Configuration.config[Configuration.saVar.freqStart];
        var freqStop = (double)Configuration.config[Configuration.saVar.freqStop];
        var mousePos = ImGui.GetMousePos();
        double graph_startDB = 100;
        double graph_endDB = 0;
        var graphStatus = new Vector2();
        draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(16, 16, 16).ToUint());
        var mouseRange = new Vector2();
        float mousePosFreq = 0, mousePosdB;

        #endregion Canvas_Data

        #region backgroundDraw

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
            var posX = left + i / graphLabelIdx * Configuration.graphSize.X - ImGui.CalcTextSize(text).X / 2;
            draw.AddText(new Vector2(posX, bottom), Color.LightGray.ToUint(), text);

            draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, bottom),
                new Vector2(posX + ImGui.CalcTextSize(text).X / 2, top), Color.FromArgb(100, Color.Gray).ToUint());

            //draw Y axis
            text = Imports.Scale(i, 0, graphLabelIdx, graph_endDB, graph_startDB).ToString().TruncateLongString(5);
            //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
            var posY = top + i / graphLabelIdx * Configuration.graphSize.Y;
            draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                Color.LightGray.ToUint(), text);
            draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), Color.FromArgb(100, Color.Gray).ToUint());
        }

        #endregion backgroundDraw

        try
        {
            var data = tab_Trace.s_traces[0].plot.ToArray();
            var referenceData = data.AsSpan();
            var minDB = data.MinBy(x => x.Value).Value;
            var traceColor_uint = Color.White.ToUint();
            var fadedColorYellow = Color.FromArgb(100, Color.White).ToUint();
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

                draw.AddLine(sampleAPos, sampleBPos, traceColor_uint, 1.0f);

                var sampleAPosRef = scaleToGraph(left, top, right, bottom, sampleA.Key,
                    sampleA.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);
                var sampleBPosRef = scaleToGraph(left, top, right, bottom, sampleB.Key,
                    sampleB.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);

                draw.AddLine(sampleAPosRef, sampleBPosRef, fadedColorYellow, 1.0f);
            }

            var x = 1;
            traceColor_uint = Color.Green.ToUint();
            var fadedColorGreen = Color.FromArgb(100, Color.Gray).ToUint();
            var anntennaData = tab_Trace.s_traces[1].plot.ToArray().AsSpan(); //asspan is fastest iteration
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

                ;

                draw.AddLine(sampleAPos, sampleBPos, traceColor_uint, 1.0f);
                var sampleAPosRef = scaleToGraph(left, top, right, bottom, sampleA.Key,
                    sampleA.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);
                var sampleBPosRef = scaleToGraph(left, top, right, bottom, sampleB.Key,
                    sampleB.Value - minDB + (float)graph_startDB / 2, freqStart, freqStop, graph_startDB, graph_endDB);
                draw.AddLine(sampleAPosRef, sampleBPosRef, fadedColorGreen, 1.0f);

                //apply new db value for marker
                if (tab_Marker.s_Marker.position >= sampleA.Key && tab_Marker.s_Marker.position <= sampleB.Key)
                {
                    tab_Marker.s_Marker.value = sampleB.Value;
                    tab_Marker.s_Marker.valueRef = valueRefB;
                }
            }

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(new Vector2(left, top),
                                                             new Vector2(right, bottom))
                                                         && s_waitForMouseClick.ElapsedMilliseconds > 100)
                tab_Marker.s_Marker.position =
                    tab_Trace.getClosestSampeledFrequency(tab_Marker.s_Marker.reference, mousePosFreq).Key;
            if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                tab_Marker.s_Marker.position =
                    tab_Trace.findMaxHoldRange(tab_Trace.s_traces[x].plot, mouseRange.X, mouseRange.Y).Key;
                s_waitForMouseClick.Restart();
            }


            var markerValue = tab_Marker.s_Marker.value;
            var markerPosition = tab_Marker.s_Marker.position;
            var markerRefValue = tab_Marker.s_Marker.valueRef;
            double RL = markerRefValue - markerValue,
                RC = Math.Pow(10, (markerValue - markerRefValue) / 20.0),
                VSWR = (1.0 + RC) / (1.0 - RC),
                mismatchLoss = -10 * Math.Log10(1 - Math.Pow(RC, 2));

            var markerPosOnGraph = scaleToGraph(left, top, right, bottom, (float)tab_Marker.s_Marker.position,
                (float)RL, freqStart, freqStop, graph_startDB, graph_endDB);
            draw.AddCircleFilled(markerPosOnGraph, 4f, traceColor_uint);
            draw.AddCircle(markerPosOnGraph, 4.1f, Color.Black.ToUint()); //outline

            tab_Marker.s_Marker.txtStatus += $"Marker\n" +
                                             $"Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}" +
                                             $"\nReturn Loss: {RL.ToString().TruncateLongString(5)}dB" +
                                             $"\nReflection Coefficient: {RC.ToString().TruncateLongString(5)}" +
                                             $"\nVSWR: {VSWR.ToString().TruncateLongString(5)}\n" +
                                             $"\nMismatch Loss {mismatchLoss.ToString().TruncateLongString(5)}";

            var markerstatusText = tab_Marker.s_Marker.txtStatus;
            var textStatusSize = ImGui.CalcTextSize(markerstatusText);
            draw.AddText(new Vector2(left, bottom - graphStatus.Y - textStatusSize.Y), traceColor_uint,
                markerstatusText);
            tab_Marker.s_Marker.txtStatus = string.Empty; //clear
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("Sequence"))
                _logger.Trace($"Render Error -> {ex.Message} {ex.StackTrace}");
        }
    }
}