using NLog;
using SoapySA.View.tabs;
using SoapyVNACommon.Extentions;
using System.Numerics;

namespace SoapySA.View;

public class Graph(MainWindow initiator)
{
    private MainWindow parent = initiator;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public void initializeGraphElements()
    {
        for (var i = 0; i < parent.tab_Trace.s_traces.Length; i++) parent.tab_Trace.s_traces[i] = new tab_Trace.Trace();
        for (var i = 0; i < parent.tab_Marker.s_markers.Length; i++)
        {
            parent.tab_Marker.s_markers[i] = new tab_Marker.marker();
            parent.tab_Marker.s_markers[i].deltaReference = 0;
            parent.tab_Marker.s_markers[i].id = i;
        }

        tab_Marker.s_markerTraceCombo = tab_Trace.s_comboTraces;
        parent.tab_Trace.s_traces[0].viewStatus = tab_Trace.traceViewStatus.active;
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
                switch (parent.tab_Trace.s_traces[i].dataStatus)
                {
                    case tab_Trace.traceDataStatus.normal:
                        for (var k = 0; k < data[0].Length; k++)
                            if (plot.ContainsKey(data[1][k]))
                                plot[data[1][k]] = data[0][k];
                            else
                                plot.Add(data[1][k], data[0][k]);
                        break;

                    case tab_Trace.traceDataStatus.minHold:
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

                    case tab_Trace.traceDataStatus.maxHold:
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

                    case tab_Trace.traceDataStatus.Average:
                        for (var k = 0; k < data[0].Length; k++)
                            if (plot.ContainsKey(data[1][k]))
                                plot[data[1][k]] = (plot[data[1][k]] * parent.tab_Trace.s_traces[i].average + data[0][k]) /
                                                   (parent.tab_Trace.s_traces[i].average + 1);
                            else
                                plot.Add(data[1][k], data[0][k]);

                        parent.tab_Trace.s_traces[i].average = Math.Min(10000, parent.tab_Trace.s_traces[i].average);
                        //maximaizing average to be 10000 so it wont be able to pass int.maxSize
                        break;
                }
            }
        }
    }

    public Vector2 scaleToGraph(float left, float top, float right, float bottom, float freq, float dB,
        double freqStart, double freqStop, double graph_startDB, double graph_endDB)
    {
        var scaledX = Imports.Scale(freq, freqStart, freqStop, left, right);
        //endb = 0

        var scaledY = Imports.Scale(dB, graph_startDB, graph_endDB, bottom, top);
        return new Vector2((float)scaledX, (float)scaledY);
    }

    public void drawGraph()
    {
        switch (parent.tab_Measurement.s_selectedMeasurementMode)
        {
            case tab_Measurement.measurementMode.none:
                parent.normalMeasurement.renderNormal();
                break;

            case tab_Measurement.measurementMode.channelPower:
                parent.channelPower.renderChannelPower();
                break;

            case tab_Measurement.measurementMode.filterBW:
                parent.FilterBandwith.renderFilterBandwith();
                break;
        }
    }
}