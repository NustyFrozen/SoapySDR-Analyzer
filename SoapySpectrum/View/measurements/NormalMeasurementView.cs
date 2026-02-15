using ImGuiNET;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapySA.View.tabs;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using System.Drawing;
using System.Numerics;
using static SoapySA.Configuration;
using static SoapyVNACommon.Theme;
using Trace = SoapySA.Model.Trace;
using TraceViewStatus = SoapySA.Model.TraceViewStatus;

namespace SoapySA.View.measurements;

public partial class NormalMeasurementView : MeasurementFeature
{
    private readonly Configuration _config;
    private readonly GraphPlotManager _graphData;
    public override string Name => "None";
    public NormalMeasurementView(Configuration config, GraphPlotManager graphData)
    {
        _config = config;
        _graphData = graphData;
        // initial cache
        UpdateCanvasDataFromConfig();

        // keep cached values synced
        _config.PropertyChanged -= ConfigOnPropertyChanged;
        _config.PropertyChanged += ConfigOnPropertyChanged;
    }

    private void ConfigOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Only update cache when relevant fields change (fast + avoids extra work)
        if (e.PropertyName is nameof(Configuration.GraphOffsetDb)
            or nameof(Configuration.GraphRefLevel)
            or nameof(Configuration.ScalePerDivision)
            or nameof(Configuration.FreqStart)
            or nameof(Configuration.FreqStop)
            or nameof(Configuration.GraphStartDb)
            or nameof(Configuration.GraphStopDb))
        {
            UpdateCanvasDataFromConfig();
        }
    }
    public override bool renderGraph()
    {
        #region Canvas_Data

        var windowPos = ImGui.GetWindowPos();
        var draw = ImGui.GetForegroundDrawList();
        var mousePos = ImGui.GetMousePos();
        var graphStatus = new Vector2();
        Left = windowPos.X + UserScreenConfiguration.PositionOffset.X;
        Right = Left + UserScreenConfiguration.GraphSize.X;
        Top = windowPos.Y + UserScreenConfiguration.PositionOffset.Y;
        Bottom = Top + UserScreenConfiguration.GraphSize.Y;
        var mouseRange = new Vector2();
        float mousePosFreq = 0, mousePosdB;

        #endregion Canvas_Data

        #region backgroundDraw

        draw.AddRectFilled(new Vector2(Left, Top), new Vector2(Right, Bottom), Color.FromArgb(16, 16, 16).ToUint());
        if (new RectangleF(Left, Top, UserScreenConfiguration.GraphSize.X, UserScreenConfiguration.GraphSize.Y).Contains(
                mousePos.X,
                mousePos.Y))
        {
            draw.AddLine(new Vector2(Left, mousePos.Y), new Vector2(Right, mousePos.Y),
                Color.FromArgb(100, 100, 100).ToUint());
            draw.AddLine(new Vector2(mousePos.X, Top), new Vector2(mousePos.X, Bottom),
                Color.FromArgb(100, 100, 100).ToUint());

            mousePosFreq =
                (float)(FreqStart + (mousePos.X - Left) / UserScreenConfiguration.GraphSize.X * (FreqStop - FreqStart));
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
            var posX = Left + i / GraphLabelIdx * UserScreenConfiguration.GraphSize.X - ImGui.CalcTextSize(text).X / 2;
            draw.AddText(new Vector2(posX, Bottom), Color.LightGray.ToUint(), text);

            draw.AddLine(new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Bottom),
                new Vector2(posX + ImGui.CalcTextSize(text).X / 2, Top), Color.FromArgb(100, Color.Gray).ToUint());

            //draw Y axis
            text = Imports.Scale(i, 0, GraphLabelIdx, GraphEndDb + DbOffset, GraphStartDb + DbOffset).ToString()
                .TruncateLongString(5);
            var posY = Top + i / GraphLabelIdx * UserScreenConfiguration.GraphSize.Y;
            draw.AddText(new Vector2(Left - ImGui.CalcTextSize(text).X, posY - ImGui.CalcTextSize(text).Y / 2),
                Color.LightGray.ToUint(), text);
            draw.AddLine(new Vector2(Left, posY), new Vector2(Right, posY), Color.FromArgb(100, Color.Gray).ToUint());
        }

        #endregion backgroundDraw

        try
        {
            for (var x = 0; x < _graphData.STraces.Length; x++)
            {
                if (_graphData.STraces[x].ViewStatus == TraceViewStatus.Clear) continue;

                var currentActiveMarkers =
                    _graphData.Markers.Where(d => d.Reference == x && d.IsActive).ToArray();

                var bandPowerDbList = new List<float>();
                var traceColor = Color.Yellow;
                switch (x)
                {
                    case 1: traceColor = Color.FromArgb(0, 255, 255); break;
                    case 2: traceColor = Color.FromArgb(255, 0, 255); break;
                    case 3: traceColor = Color.FromArgb(0, 255, 0); break;
                    case 4: traceColor = Color.FromArgb(0, 0, 255); break;
                    case 5: traceColor = Color.FromArgb(255, 0, 0); break;
                }

                if (_graphData.STraces[x].ViewStatus == TraceViewStatus.View)
                    traceColor = Color.FromArgb(100, traceColor);

                var plot = _graphData.STraces[x].Plot;
                var traceColorUint = traceColor.ToUint();
                var plotData = plot.ToArray().AsSpan(); // fastest iteration

                for (var i = 1; i < plotData.Length; i++)
                {
                    var sampleA = plotData[i - 1];
                    var sampleB = plotData[i];

                    var sampleAPos = GraphPlotManager.ScaleToGraph(Left, Top, Right, Bottom, sampleA.Key, sampleA.Value,
                        FreqStart, FreqStop, GraphStartDb, GraphEndDb);
                    var sampleBPos = GraphPlotManager.ScaleToGraph(Left, Top, Right, Bottom, sampleB.Key, sampleB.Value,
                        FreqStart, FreqStop, GraphStartDb, GraphEndDb);

                    // bounds check
                    if (sampleBPos.X > Right || sampleAPos.X < Left) continue;

                    if (sampleAPos.Y < Top || sampleBPos.Y < Top || sampleAPos.Y > Bottom || sampleBPos.Y > Bottom)
                    {
                        if (!_config.AutomaticLevel) continue;

                        if (sampleAPos.Y < Top || sampleBPos.Y < Top)
                            _config.GraphStartDb = Math.Min(sampleA.Value, sampleB.Value);
                        else
                            _config.GraphStopDb = Math.Max(sampleA.Value, sampleB.Value);
                    }

                    draw.AddLine(sampleAPos, sampleBPos, traceColorUint, 1.0f);

                    currentActiveMarkers = currentActiveMarkers.Select(marker =>
                    {
                        // update marker value
                        if (marker.Position >= sampleA.Key && marker.Position <= sampleB.Key)
                        {
                            marker.Value = Math.Abs(marker.Position - sampleA.Key) >=
                                           Math.Abs(marker.Position - sampleB.Key)
                                ? sampleA.Value
                                : sampleB.Value;

                            _graphData.Markers[marker.Id].Value = marker.Value;
                        }

                        // band power capture
                        if (marker.BandPower)
                        {
                            if (sampleA.Key >= (float)(marker.Position - marker.BandPowerSpan / 2) &&
                                sampleA.Key <= (float)(_graphData.Markers[marker.Id].Position + marker.BandPowerSpan / 2))
                            {
                                draw.AddLine(sampleAPos, sampleBPos, Color.White.ToUint(), 1.0f);
                                bandPowerDbList.Add(sampleA.Value);
                            }
                        }

                        return marker;
                    }).ToArray();
                }

                // marker interaction (unchanged logic, just using _graphData.Markers/_graphData.STraces)
                if (_graphData.Markers[_graphData.SSelectedMarker].IsActive)
                {
                    if (ImGui.IsMouseDown((int)ImGuiMouseButton.Left) &&
                        ImGui.IsMouseHoveringRect(new Vector2(Left, Top), new Vector2(Right, Bottom)) &&
                        SWaitForMouseClick.ElapsedMilliseconds > 100)
                    {
                        _graphData.Markers[_graphData.SSelectedMarker].Position = _graphData.STraces[_graphData.Markers[_graphData.SSelectedMarker].Reference]
                            .GetClosestSampledFrequency(mousePosFreq).Key;
                    }

                    if (mouseRange.X != 0 && ImGui.IsMouseDoubleClicked((int)ImGuiMouseButton.Left))
                    {
                        _graphData.Markers[_graphData.SSelectedMarker].Position =
                            _graphData.STraces[x].FindMaxHoldRange(mouseRange.X, mouseRange.Y).Key;
                        SWaitForMouseClick.Restart();
                    }
                }

                for (var c = 0; c < currentActiveMarkers.Length; c++)
                {
                    if (currentActiveMarkers[c].BandPower)
                        CalculateBandPower(currentActiveMarkers[c], bandPowerDbList);

                    var markerPosOnGraph = GraphPlotManager.ScaleToGraph(Left, Top, Right, Bottom,
                        (float)currentActiveMarkers[c].Position, (float)currentActiveMarkers[c].Value, FreqStart,
                        FreqStop, GraphStartDb, GraphEndDb);

                    draw.AddCircleFilled(markerPosOnGraph, 6f, traceColorUint);
                    draw.AddCircle(markerPosOnGraph, 6.1f, Color.White.ToUint());

                    var markerValue = currentActiveMarkers[c].Value;
                    var markerPosition = currentActiveMarkers[c].Position;

                    if (currentActiveMarkers[c].DeltaReference != 0)
                    {
                        markerValue = currentActiveMarkers[c].Value -
                                      _graphData.Markers[currentActiveMarkers[c].DeltaReference - 1].Value;
                        markerPosition = currentActiveMarkers[c].Position -
                                         _graphData.Markers[currentActiveMarkers[c].DeltaReference - 1].Position;
                    }

                    currentActiveMarkers[c].TxtStatus +=
                        $"Marker {currentActiveMarkers[c].Id + 1} \n Freq {(markerPosition / 1e6).ToString().TruncateLongString(5)}M \n {(markerValue + DbOffset).ToString().TruncateLongString(5)} dB\n";

                    // ... rest of your rendering code stays unchanged below ...
                    // (no config dictionary usage further down)
                }
            }
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("Sequence"))
                Logger.Trace($"NormalMeasurement Render Error -> {ex.Message}");
        }
        return true;
    }
}