using System.Numerics;
using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using TraceViewStatus = SoapySA.Model.TraceViewStatus;

namespace SoapySA.View.measurements;

public partial class NormalMeasurementView(MainWindowView initiator)
{
    public void RenderNormal()
    {
        #region Canvas_Data

        var windowPos = ImGui.GetWindowPos();
        var draw = ImGui.GetForegroundDrawList();
        var mousePos = ImGui.GetMousePos();
        var graphStatus = new Vector2();
        Left = windowPos.X + _parent.Configuration.PositionOffset.X;
        Right = Left + _parent.Configuration.GraphSize.X;
        Top = windowPos.Y + _parent.Configuration.PositionOffset.Y;
        Bottom = Top + _parent.Configuration.GraphSize.Y;
        var mouseRange = new Vector2();
        float mousePosFreq = 0, mousePosdB;

        #endregion Canvas_Data

        #region backgroundDraw

        draw.AddRectFilled(new Vector2(Left, Top), new Vector2(Right, Bottom), Color.FromArgb(16, 16, 16).ToUint());
        if (new RectangleF(Left, Top, _parent.Configuration.GraphSize.X, _parent.Configuration.GraphSize.Y).Contains(
                mousePos.X,
                mousePos.Y))
        {
            draw.AddLine(new Vector2(Left, mousePos.Y), new Vector2(Right, mousePos.Y),
                Color.FromArgb(100, 100, 100).ToUint());
            draw.AddLine(new Vector2(mousePos.X, Top), new Vector2(mousePos.X, Bottom),
                Color.FromArgb(100, 100, 100).ToUint());

            mousePosFreq =
                (float)(FreqStart + (mousePos.X - Left) / _parent.Configuration.GraphSize.X * (FreqStop - FreqStart));
            mousePosdB = (float)(GraphStartDb -
                (Bottom - mousePos.Y + Top) / Bottom * (Math.Abs(GraphEndDb) - Math.Abs(GraphStartDb)) + DbOffset);
            mouseRange.X = (float)(mousePosFreq - (FreqStop - FreqStart) / GraphLabelIdx);
            mouseRange.Y = (float)(mousePosFreq + (FreqStop - FreqStart) / GraphLabelIdx);
            draw.AddText(new Vector2(mousePos.X + 5, mousePos.Y + 5), Color.FromArgb(100, 100, 100).ToUint(),
                $"Freq {(mousePosFreq / 1e6).ToString().TruncateLongString(5)}M\ndBm {mousePosdB}");
        }

        for (float i = 0; i <= GraphLabelIdx; i++)
        {
            //draw X axis
            var text = $"{(FreqStart + i / GraphLabelIdx * (FreqStop - FreqStart)) / 1e6}".TruncateLongString(5);
            text += "M";
            var posX = Left + i / GraphLabelIdx * _parent.Configuration.GraphSize.X - ImGui.CalcTextSize(text).X / 2;
            draw.AddText(new Vector2(posX, Bottom), Color.LightGray.ToUint(), text);

            draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Bottom),
                new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Top), Color.FromArgb(100, Color.Gray).ToUint());

            //draw Y axis
            text = Imports.Scale(i, 0, GraphLabelIdx, GraphEndDb + DbOffset, GraphStartDb + DbOffset).ToString()
                .TruncateLongString(5);
            //((graph_startDB - (graphLabelIdx - i) / graphLabelIdx * (Math.Abs(graph_endDB) - Math.Abs(graph_startDB))) + dbOffset).ToString().TruncateLongString(5);
            var posY = Top + i / GraphLabelIdx * _parent.Configuration.GraphSize.Y;
            draw.AddText(new Vector2(Left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                Color.LightGray.ToUint(), text);
            draw.AddLine(new Vector2(Left, posY), new Vector2(Right, posY), Color.FromArgb(100, Color.Gray).ToUint());
        }

        #endregion backgroundDraw

        try
        {
            for (var x = 0; x < _parent.TraceView.STraces.Length; x++)
            {
                if (_parent.TraceView.STraces[x].ViewStatus == TraceViewStatus.Clear) continue;
                var currentActiveMarkers =
                    _parent.MarkerView.SMarkers.Where(d => d.Reference == x && d.IsActive).ToArray();
                var bandPowerDbList = new List<float>();
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

                if (_parent.TraceView.STraces[x].ViewStatus == TraceViewStatus.View)
                    traceColor = Color.FromArgb(100, traceColor);
                var plot = _parent.TraceView.STraces[x].Plot;
                var traceColorUint = traceColor.ToUint();
                var plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration

                for (var i = 1; i < plotData.Length; i++)
                {
                    var sampleA = plotData[i - 1];
                    var sampleB = plotData[i];

                    var sampleAPos = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom, sampleA.Key, sampleA.Value,
                        FreqStart,
                        FreqStop, GraphStartDb, GraphEndDb);
                    var sampleBPos = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom, sampleB.Key, sampleB.Value,
                        FreqStart,
                        FreqStop, GraphStartDb, GraphEndDb);
                    //bounds check
                    if (sampleBPos.X > Right || sampleAPos.X < Left) continue;
                    if (sampleAPos.Y < Top || sampleBPos.Y < Top || sampleAPos.Y > Bottom || sampleBPos.Y > Bottom)
                    {
                        if (!(bool)_parent.Configuration.Config[Configuration.SaVar.AutomaticLevel]) continue;
                        if (sampleAPos.Y < Top || sampleBPos.Y < Top)
                            _parent.Configuration.Config[Configuration.SaVar.GraphStartDb] =
                                (double)Math.Min(sampleA.Value, sampleB.Value);
                        else
                            _parent.Configuration.Config[Configuration.SaVar.GraphStopDb] =
                                (double)Math.Max(sampleA.Value, sampleB.Value);
                    }

                    draw.AddLine(sampleAPos, sampleBPos, traceColorUint, 1.0f);
                    currentActiveMarkers = currentActiveMarkers.Select(marker =>
                    {
                        //apply new db value for marker
                        if (marker.Position >= sampleA.Key && marker.Position <= sampleB.Key)
                        {
                            marker.Value = Math.Abs(marker.Position - sampleA.Key) >=
                                           Math.Abs(marker.Position - sampleB.Key)
                                ? sampleA.Value
                                : sampleB.Value; //to which point is he closer
                            _parent.MarkerView.SMarkers[marker.Id].Value = marker.Value;
                        }

                        //apply bandPower List
                        if (marker.BandPower)
                            if (sampleA.Key >= (float)(marker.Position - marker.BandPowerSpan / 2) && sampleA.Key <=
                                (float)(_parent.MarkerView.SMarkers[marker.Id].Position + marker.BandPowerSpan / 2))
                            {
                                draw.AddLine(sampleAPos, sampleBPos, Color.White.ToUint(), 1.0f);
                                bandPowerDbList.Add(sampleA.Value);
                            }

                        return marker;
                    }).ToArray();
                }

                if (_parent.MarkerView.SMarkers[_parent.MarkerView.SSelectedMarker].IsActive)
                {
                    if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(new Vector2(Left, Top),
                                                                     new Vector2(Right, Bottom))
                                                                 && SWaitForMouseClick.ElapsedMilliseconds > 100)
                        _parent.MarkerView.SMarkers[_parent.MarkerView.SSelectedMarker].Position = _parent.TraceView
                            .GetClosestSampledFrequency(
                                _parent.MarkerView.SMarkers[_parent.MarkerView.SSelectedMarker].Reference,
                                mousePosFreq).Key;
                    if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        _parent.MarkerView.SMarkers[_parent.MarkerView.SSelectedMarker].Position = _parent.TraceView
                            .FindMaxHoldRange(_parent.TraceView.STraces[x].Plot, mouseRange.X, mouseRange.Y).Key;
                        SWaitForMouseClick.Restart();
                    }
                }

                for (var c = 0; c < currentActiveMarkers.Length; c++)
                {
                    if (currentActiveMarkers[c].BandPower) CalculateBandPower(currentActiveMarkers[c], bandPowerDbList);

                    var markerPosOnGraph = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom,
                        (float)currentActiveMarkers[c].Position, (float)currentActiveMarkers[c].Value, FreqStart,
                        FreqStop, GraphStartDb, GraphEndDb);
                    draw.AddCircleFilled(markerPosOnGraph, 6f, traceColorUint);
                    draw.AddCircle(markerPosOnGraph, 6.1f, Color.White.ToUint()); //outline
                    var markerValue = currentActiveMarkers[c].Value;
                    var markerPosition = currentActiveMarkers[c].Position;
                    if (currentActiveMarkers[c].DeltaReference != 0)
                    {
                        markerValue = currentActiveMarkers[c].Value -
                                      _parent.MarkerView.SMarkers[currentActiveMarkers[c].DeltaReference - 1].Value;
                        markerPosition = currentActiveMarkers[c].Position -
                                         _parent.MarkerView.SMarkers[currentActiveMarkers[c].DeltaReference - 1]
                                             .Position;
                    }

                    currentActiveMarkers[c].TxtStatus +=
                        $"Marker {currentActiveMarkers[c].Id + 1} \n Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}M \n {(markerValue + DbOffset).ToString().TruncateLongString(5)} dB\n";
                    if (currentActiveMarkers[c].Delta)
                    {
                        var deltaPosition = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom,
                            (float)currentActiveMarkers[c].DeltaFreq, (float)currentActiveMarkers[c].DeltadB, FreqStart,
                            FreqStop, GraphStartDb, GraphEndDb);
                        var textSize = ImGui.CalcTextSize($"Delta Marker {c + 1}");

                        draw.AddLine(new Vector2(deltaPosition.X + 5, deltaPosition.Y),
                            new Vector2(deltaPosition.X - 5, deltaPosition.Y), traceColorUint);
                        draw.AddLine(new Vector2(deltaPosition.X, deltaPosition.Y + 5),
                            new Vector2(deltaPosition.X, deltaPosition.Y - 5), traceColorUint);
                        draw.AddText(new Vector2(deltaPosition.X - textSize.X / 2, deltaPosition.Y - textSize.Y - 2),
                            Color.White.ToUint(), $"Delta Marker {c + 1}");

                        var deltaDb = (currentActiveMarkers[c].Value - currentActiveMarkers[c].DeltadB).ToString()
                            .TruncateLongString(5);
                        currentActiveMarkers[c].TxtStatus +=
                            $"Delta \n Freq {((currentActiveMarkers[c].Position - currentActiveMarkers[c].DeltaFreq) / 1e6).ToString().TruncateLongString(5)} Mhz \n {deltaDb} dB\n";
                    }

                    if (currentActiveMarkers[c].BandPower)
                    {
                        var powerBandLeft = _parent.TraceView.GetClosestSampledFrequency(x,
                            (float)(currentActiveMarkers[c].Position - currentActiveMarkers[c].BandPowerSpan / 2));
                        var powerBandRight = _parent.TraceView.GetClosestSampledFrequency(x,
                            (float)(currentActiveMarkers[c].Position + currentActiveMarkers[c].BandPowerSpan / 2));
                        var scaledPowerBandLeft = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom,
                            powerBandLeft.Key,
                            powerBandLeft.Value, FreqStart, FreqStop, GraphStartDb, GraphEndDb);
                        var scaledPowerBandRight = _parent.GraphView.ScaleToGraph(Left, Top, Right, Bottom,
                            powerBandRight.Key,
                            powerBandRight.Value, FreqStart, FreqStop, GraphStartDb, GraphEndDb);
                        draw.AddLine(new Vector2(scaledPowerBandLeft.X, Top),
                            new Vector2(scaledPowerBandLeft.X, Bottom), traceColorUint);
                        draw.AddLine(new Vector2(scaledPowerBandRight.X, Top),
                            new Vector2(scaledPowerBandRight.X, Bottom), traceColorUint);
                        currentActiveMarkers[c].TxtStatus +=
                            $"Band Power \n {(currentActiveMarkers[c].BandPowerValue + DbOffset).ToString().TruncateLongString(5)} dB\n";
                    }

                    var markerstatusText = currentActiveMarkers[c].TxtStatus;
                    var textStatusSize = ImGui.CalcTextSize(markerstatusText);
                    draw.AddText(new Vector2(Right + graphStatus.X - textStatusSize.X, Top + graphStatus.Y),
                        traceColorUint, markerstatusText);
                    graphStatus.X -= textStatusSize.X + 5;
                    currentActiveMarkers[c].TxtStatus = string.Empty; //clear
                }
            }
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("Sequence"))
                Logger.Trace($"NormalMeasurement Render Error -> {ex.Message}");
        }
    }
}