using System.Numerics;
using NLog;
using SoapySA.Model;
using SoapySA.View.tabs;
using SoapyVNACommon.Extentions;
using Trace = SoapySA.Model.Trace;
using TraceDataStatus = SoapySA.Model.TraceDataStatus;
using TraceViewStatus = SoapySA.Model.TraceViewStatus;

namespace SoapySA.View;

public partial class GraphView
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly MainWindowView _parent = initiator;

    public void InitializeGraphElements()
    {
        for (var i = 0; i < _parent.TraceView.STraces.Length; i++) _parent.TraceView.STraces[i] = new Trace();
        for (var i = 0; i < _parent.MarkerView.SMarkers.Length; i++)
        {
            _parent.MarkerView.SMarkers[i] = new Marker();
            _parent.MarkerView.SMarkers[i].DeltaReference = 0;
            _parent.MarkerView.SMarkers[i].Id = i;
        }

        MarkerView.SMarkerTraceCombo = TraceView.SComboTraces;
        _parent.TraceView.STraces[0].ViewStatus = TraceViewStatus.Active;
    }

    public void ClearPlotData()
    {
        for (var i = 0; i < _parent.TraceView.STraces.Length; i++)
        {
            if (_parent.TraceView.STraces[i].ViewStatus != TraceViewStatus.Active) continue;
            var plot = _parent.TraceView.STraces[i].Plot;
            plot.Clear();
        }
    }

    public void UpdateData(float[][] psd)
    {
        var data = psd.AsSpan();
        for (var i = 0; i < _parent.TraceView.STraces.Length; i++)
        {
            if (_parent.TraceView.STraces[i].ViewStatus != TraceViewStatus.Active) continue;
            var plot = _parent.TraceView.STraces[i].Plot;
            lock (plot)
            {
                switch (_parent.TraceView.STraces[i].DataStatus)
                {
                    case TraceDataStatus.Normal:
                        for (var k = 0; k < data[0].Length; k++)
                            if (plot.ContainsKey(data[1][k]))
                                plot[data[1][k]] = data[0][k];
                            else
                                plot.Add(data[1][k], data[0][k]);
                        break;

                    case TraceDataStatus.MinHold:
                        for (var k = 0; k < data[0].Length; k++)
                            if (plot.ContainsKey(data[1][k]))
                            {
                                if (plot[data[1][k]] > data[0][k])
                                    plot[data[1][k]] = data[0][k];
                            }
                            else
                            {
                                plot.Add(data[1][k], data[0][k]);
                            }

                        break;

                    case TraceDataStatus.MaxHold:
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

                        break;

                    case TraceDataStatus.Average:
                        for (var k = 0; k < data[0].Length; k++)
                            if (plot.ContainsKey(data[1][k]))
                                plot[data[1][k]] =
                                    (plot[data[1][k]] * _parent.TraceView.STraces[i].Average + data[0][k]) /
                                    (_parent.TraceView.STraces[i].Average + 1);
                            else
                                plot.Add(data[1][k], data[0][k]);

                        _parent.TraceView.STraces[i].Average = Math.Min(10000, _parent.TraceView.STraces[i].Average);
                        //maximaizing average to be 10000 so it wont be able to pass int.maxSize
                        break;
                }
            }
        }
    }

    public Vector2 ScaleToGraph(float left, float top, float right, float bottom, float freq, float dB,
        double freqStart, double freqStop, double graphStartDb, double graphEndDb)
    {
        var scaledX = Imports.Scale(freq, freqStart, freqStop, left, right);
        //endb = 0

        var scaledY = Imports.Scale(dB, graphStartDb, graphEndDb, bottom, top);
        return new Vector2((float)scaledX, (float)scaledY);
    }
}