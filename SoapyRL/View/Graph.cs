using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using NLog;
using SoapyRL.Extentions;
using SoapyRL.View.tabs;

namespace SoapyRL.View;

public class Graph(MainWindow initiator)
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private Thread _calculateBandPowerThread;
    public MainWindow Parent = initiator;

    public Stopwatch SWaitForMouseClick = new();

    public void InitializeGraphElements()
    {
        for (var i = 0; i < Parent.TabTrace.STraces.Length; i++)
        {
            Parent.TabTrace.STraces[i] = new TabTrace.Trace();
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

            Parent.TabTrace.STraces[i].Color = color;
        }

        Parent.TabMarker.SMarker = new TabMarker.Marker();
        Parent.TabMarker.SMarker.Id = 0;
        Parent.TabTrace.STraces[0].ViewStatus = TabTrace.TraceViewStatus.Active;
        Parent.TabMarker.SMarker.IsActive = true;
    }

    public void ClearPlotData()
    {
        for (var i = 0; i < Parent.TabTrace.STraces.Length; i++)
        {
            if (Parent.TabTrace.STraces[i].ViewStatus != TabTrace.TraceViewStatus.Active) continue;
            var plot = Parent.TabTrace.STraces[i].Plot;
            plot.Clear();
        }
    }

    public void UpdateData(float[][] psd)
    {
        var data = psd.AsSpan();
        for (var i = 0; i < Parent.TabTrace.STraces.Length; i++)
        {
            if (Parent.TabTrace.STraces[i].ViewStatus != TabTrace.TraceViewStatus.Active) continue;
            var plot = Parent.TabTrace.STraces[i].Plot;
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

    private Vector2 ScaleToGraph(float left, float top, float right, float bottom, float freq, float dB,
        double freqStart, double freqStop, double graphStartDb, double graphEndDb)
    {
        var scale = freqStop - freqStart;
        var scale2 = freq - freqStart;
        var scaledX = left + Parent.Configuration.GraphSize.X * (scale2 / scale);
        //endb = 0

        var scaledY = Imports.Scale(dB, graphStartDb, graphEndDb, bottom, top);
        return new Vector2((float)scaledX, (float)scaledY);
    }

    public void DrawGraph()
    {
        #region Canvas_Data

        var draw = ImGui.GetForegroundDrawList();

        var left = ImGui.GetWindowPos().X + Parent.Configuration.PositionOffset.X;
        var right = left + Parent.Configuration.GraphSize.X;
        var top = ImGui.GetWindowPos().Y + Parent.Configuration.PositionOffset.Y;
        var bottom = top + Parent.Configuration.GraphSize.Y;

        var graphLabelIdx = 20.0f;

        var freqStart = (double)Parent.Configuration.Config[Configuration.SaVar.FreqStart];
        var freqStop = (double)Parent.Configuration.Config[Configuration.SaVar.FreqStop];
        var mousePos = ImGui.GetMousePos();
        double graphStartDb = 100;
        double graphEndDb = 0;
        var graphStatus = new Vector2();
        draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(16, 16, 16).ToUint());
        var mouseRange = new Vector2();
        float mousePosFreq = 0, mousePosdB;

        #endregion Canvas_Data

        #region backgroundDraw

        if (new RectangleF(left, top, Parent.Configuration.GraphSize.X, Parent.Configuration.GraphSize.Y).Contains(
                mousePos.X,
                mousePos.Y))
        {
            draw.AddLine(new Vector2(left, mousePos.Y), new Vector2(right, mousePos.Y),
                Color.FromArgb(100, 100, 100).ToUint());
            draw.AddLine(new Vector2(mousePos.X, top), new Vector2(mousePos.X, bottom),
                Color.FromArgb(100, 100, 100).ToUint());

            mousePosFreq =
                (float)(freqStart + (mousePos.X - left) / Parent.Configuration.GraphSize.X * (freqStop - freqStart));
            mousePosdB = (float)(graphStartDb -
                                 (bottom - mousePos.Y + top) / bottom *
                                 (Math.Abs(graphEndDb) - Math.Abs(graphStartDb)));
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
            var posX = left + i / graphLabelIdx * Parent.Configuration.GraphSize.X - ImGui.CalcTextSize(text).X / 2;
            draw.AddText(new Vector2(posX, bottom), Color.LightGray.ToUint(), text);

            draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, bottom),
                new Vector2(posX + ImGui.CalcTextSize(text).X / 2, top), Color.FromArgb(100, Color.Gray).ToUint());

            //draw Y axis
            text = Imports.Scale(i, 0, graphLabelIdx, graphEndDb, graphStartDb).ToString().TruncateLongString(5);
            //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
            var posY = top + i / graphLabelIdx * Parent.Configuration.GraphSize.Y;
            draw.AddText(new Vector2(left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                Color.LightGray.ToUint(), text);
            draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), Color.FromArgb(100, Color.Gray).ToUint());
        }

        #endregion backgroundDraw

        try
        {
            var data = Parent.TabTrace.STraces[0].Plot.ToArray();
            var referenceData = data.AsSpan();
            var minDb = data.Length == 0 ? 0 : data.MinBy(x => x.Value).Value;
            var traceColorUint = Parent.TabTrace.STraces[0].Color;
            var fadedColorYellow = Parent.TabTrace.STraces[0].LiteColor;
            for (var i = 1; i < referenceData.Length; i++)
            {
                var sampleA = referenceData[i - 1];
                var sampleAdb = 0;
                var sampleB = referenceData[i];
                var sampleBdb = 0;
                var sampleAPos = ScaleToGraph(left, top, right, bottom, sampleA.Key, sampleAdb, freqStart, freqStop,
                    graphStartDb, graphEndDb);
                var sampleBPos = ScaleToGraph(left, top, right, bottom, sampleB.Key, sampleBdb, freqStart, freqStop,
                    graphStartDb, graphEndDb);
                //bounds check
                if (sampleBPos.X > right || sampleAPos.X < left) continue;

                draw.AddLine(sampleAPos, sampleBPos, Parent.TabTrace.STraces[0].Color, 1.0f);

                var sampleAPosRef = ScaleToGraph(left, top, right, bottom, sampleA.Key,
                    sampleA.Value - minDb + (float)graphStartDb / 2, freqStart, freqStop, graphStartDb, graphEndDb);
                var sampleBPosRef = ScaleToGraph(left, top, right, bottom, sampleB.Key,
                    sampleB.Value - minDb + (float)graphStartDb / 2, freqStart, freqStop, graphStartDb, graphEndDb);

                draw.AddLine(sampleAPosRef, sampleBPosRef, Parent.TabTrace.STraces[0].LiteColor, 1.0f);
            }

            var impedanceTol = (float)Parent.Configuration.Config[Configuration.SaVar.ValidImpedanceTol];
            var anntennaData = Parent.TabTrace.STraces[1].Plot.ToArray().AsSpan(); //asspan is fastest iteration
            double rangeStart = 0.0, rangeEnd = 0.0;
            var iscalculatingValidRange = false;
            var validRanges = new List<Tuple<double, double>>();
            for (var i = 1; i < anntennaData.Length; i++)
            {
                var sampleA = anntennaData[i - 1];
                var sampleArl = referenceData[i - 1].Value - sampleA.Value;
                var sampleB = anntennaData[i];
                var valueRefB = referenceData[i].Value;
                var sampleBrl = valueRefB - sampleB.Value;

                var sampleAPos = ScaleToGraph(left, top, right, bottom, sampleA.Key, sampleArl, freqStart, freqStop,
                    graphStartDb, graphEndDb);
                var sampleBPos = ScaleToGraph(left, top, right, bottom, sampleB.Key, sampleBrl, freqStart, freqStop,
                    graphStartDb, graphEndDb);
                //bounds check
                if (sampleAPos.Y < top || sampleBPos.Y < top
                                       || sampleAPos.Y > bottom || sampleBPos.Y > bottom)
                {
                    sampleAPos.Y = sampleAPos.Y < top ? top : sampleAPos.Y > bottom ? bottom : sampleAPos.Y;
                    sampleBPos.Y = sampleBPos.Y < top ? top : sampleBPos.Y > bottom ? bottom : sampleBPos.Y;
                }

                var sampleAPosRef = ScaleToGraph(left, top, right, bottom, sampleA.Key,
                    sampleA.Value - minDb + (float)graphStartDb / 2, freqStart, freqStop, graphStartDb, graphEndDb);
                var sampleBPosRef = ScaleToGraph(left, top, right, bottom, sampleB.Key,
                    sampleB.Value - minDb + (float)graphStartDb / 2, freqStart, freqStop, graphStartDb, graphEndDb);
                if (Parent.TabDevice.IsShowValidRangeEnabled)
                {
                    var foward = 1 - ((double)-(valueRefB - sampleB.Value)).ToMw();
                    if (foward >= impedanceTol)
                    {
                        if (iscalculatingValidRange)
                        {
                            rangeEnd = sampleB.Key;
                        }
                        else
                        {
                            iscalculatingValidRange = true;
                            rangeStart = sampleB.Key;
                            Parent.TabTrace.STraces[1].Color = Color.White.ToUint();
                        }
                    }
                    else
                    {
                        if (iscalculatingValidRange)
                        {
                            iscalculatingValidRange = false;
                            Parent.TabTrace.STraces[1].Color = Color.LimeGreen.ToUint();
                            validRanges.Add(new Tuple<double, double>(rangeStart, rangeEnd));
                        }
                    }

                    if (i + 1 > anntennaData.Length && iscalculatingValidRange)
                    {
                        validRanges.Add(new Tuple<double, double>(rangeStart, rangeEnd));
                        Parent.TabTrace.STraces[1].Color = Color.LimeGreen.ToUint();
                    }
                }

                draw.AddLine(sampleAPos, sampleBPos, Parent.TabTrace.STraces[1].Color, 1.0f);
                draw.AddLine(sampleAPosRef, sampleBPosRef, Parent.TabTrace.STraces[1].LiteColor, 1.0f);

                //apply new db value for marker
                if (Parent.TabMarker.SMarker.Position >= sampleA.Key &&
                    Parent.TabMarker.SMarker.Position <= sampleB.Key)
                {
                    Parent.TabMarker.SMarker.Value = sampleB.Value;
                    Parent.TabMarker.SMarker.ValueRef = valueRefB;
                }
            }

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(new Vector2(left, top),
                                                             new Vector2(right, bottom))
                                                         && SWaitForMouseClick.ElapsedMilliseconds > 100)
                Parent.TabMarker.SMarker.Position =
                    Parent.TabTrace.GetClosestSampeledFrequency(Parent.TabMarker.SMarker.Reference, mousePosFreq)
                        .Key;
            if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                Parent.TabMarker.SMarker.Position =
                    Parent.TabTrace.FindMaxHoldRange(Parent.TabTrace.STraces[1].Plot, mouseRange.X, mouseRange.Y)
                        .Key;
                SWaitForMouseClick.Restart();
            }

            var markerValue = Parent.TabMarker.SMarker.Value;
            var markerPosition = Parent.TabMarker.SMarker.Position;
            var markerRefValue = Parent.TabMarker.SMarker.ValueRef;

            double rl = markerRefValue - markerValue,
                rc = Math.Pow(10, (markerValue - markerRefValue) / 20.0),
                vswr = (1.0 + rc) / (1.0 - rc),
                mismatchLoss = -10 * Math.Log10(1 - Math.Pow(rc, 2));
            var reflected = (-rl).ToMw();
            var forwarded = 1 - reflected;
            var markerPosOnGraph = ScaleToGraph(left, top, right, bottom, (float)Parent.TabMarker.SMarker.Position,
                (float)rl, freqStart, freqStop, graphStartDb, graphEndDb);
            draw.AddCircleFilled(markerPosOnGraph, 4f, Parent.TabTrace.STraces[1].Color);
            draw.AddCircle(markerPosOnGraph, 4.1f, Color.Black.ToUint()); //outline

            Parent.TabMarker.SMarker.TxtStatus += $"Marker\n" +
                                                    $"Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}" +
                                                    $"\nReturn Loss: {rl.ToString().TruncateLongString(5)}dB" +
                                                    $"\nReflection Coefficient: {rc.ToString().TruncateLongString(5)}" +
                                                    $"\nVSWR: {vswr.ToString().TruncateLongString(5)}" +
                                                    $"\nMismatch Loss {mismatchLoss.ToString().TruncateLongString(5)}" +
                                                    $"\nForward {(forwarded * 100).ToString().TruncateLongString(5)}% reflected {(reflected * 100).ToString().TruncateLongString(5)}%";
            var markerstatusText = Parent.TabMarker.SMarker.TxtStatus;
            var textStatusSize = ImGui.CalcTextSize(markerstatusText);
            draw.AddText(new Vector2(left + Parent.Configuration.GraphSize.X / 2 - textStatusSize.X / 2,
                    bottom - graphStatus.Y - textStatusSize.Y), Parent.TabTrace.STraces[1].Color,
                markerstatusText);
            Parent.TabMarker.SMarker.TxtStatus = string.Empty; //clear
            if (Parent.TabDevice.IsShowValidRangeEnabled)
            {
                var text = "Valid Ranges\n";
                foreach (var range in validRanges)
                    text += $"{range.Item1.ToString()} - {range.Item2.ToString()}\n";
                var textSize = ImGui.CalcTextSize(text);
                draw.AddText(new Vector2(left,
                    bottom - graphStatus.Y - textSize.Y), Parent.TabTrace.STraces[1].Color, text);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Render Error -> {ex.Message} {ex.StackTrace}");
        }
    }
}