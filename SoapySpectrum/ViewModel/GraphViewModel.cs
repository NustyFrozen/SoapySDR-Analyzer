using Newtonsoft.Json;
using NLog;
using SoapySA.Model;
using SoapySA.View.tabs;
using SoapyVNACommon.Extentions;
using System.Numerics;

namespace SoapySA.View;

public partial class GraphPlotManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public int SSelectedTrace, SSelectedMarker;
    public Trace[] STraces = new Trace[6];
    public Marker[] Markers = new Marker[9];


    public GraphPlotManager(Configuration config)
    {
        // Use Data.STraces directly instead of going through _parent.TraceView
        for (var i = 0; i < STraces.Length; i++)
            STraces[i] = new Trace();

        for (var i = 0; i < Markers.Length; i++)
        {
            Markers[i] = new Marker
            {
                DeltaReference = 0,
                Id = i
            };
        }
        MarkerView.SMarkerTraceCombo = TraceView.SComboTraces;
        STraces[0].ViewStatus = TraceViewStatus.Active;
        config.OnConfigLoadBegin += (object sender,EventArgs e) =>
        {
            Markers = JsonConvert.DeserializeObject<Marker[]>(File.ReadAllText(config.MarkersPath)) ?? Markers;
            STraces = JsonConvert.DeserializeObject<Trace[]>(File.ReadAllText(config.TracesPath)) ?? STraces;
        };
        config.OnConfigSaveBegin += (object sender, EventArgs e) =>
        {
            File.WriteAllText(config.MarkersPath, JsonConvert.SerializeObject(Markers, Formatting.Indented));
            File.WriteAllText(config.TracesPath, JsonConvert.SerializeObject(STraces, Formatting.Indented));

        };
    }

    public void ClearPlotData()
    {
        foreach (var trace in STraces)
        {
            if (trace.ViewStatus == TraceViewStatus.Active)
            {
                trace.Plot.Clear();
            }
        }
    }

    public void UpdateData(float[][] psd)
    {
        var psdSpan = psd.AsSpan();
        var freqData = psdSpan[1];
        var ampData = psdSpan[0];

        for (var i = 0; i < STraces.Length; i++)
        {
            var trace = STraces[i];
            if (trace.ViewStatus != TraceViewStatus.Active) continue;

            var plot = trace.Plot;
            lock (plot)
            {
                switch (trace.DataStatus)
                {
                    case TraceDataStatus.Normal:
                        for (var k = 0; k < ampData.Length; k++)
                        {
                            if (plot.ContainsKey(freqData[k]))
                                plot[freqData[k]] = ampData[k];
                            else
                                plot.Add(freqData[k], ampData[k]);
                        }
                        break;

                    case TraceDataStatus.MinHold:
                        for (var k = 0; k < ampData.Length; k++)
                        {
                            if (plot.TryGetValue(freqData[k], out float currentMin))
                            {
                                if (currentMin > ampData[k]) plot[freqData[k]] = ampData[k];
                            }
                            else
                            {
                                plot.Add(freqData[k], ampData[k]);
                            }
                        }
                        break;

                    case TraceDataStatus.MaxHold:
                        for (var k = 0; k < ampData.Length; k++)
                        {
                            if (plot.TryGetValue(freqData[k], out float currentMax))
                            {
                                if (currentMax < ampData[k]) plot[freqData[k]] = ampData[k];
                            }
                            else
                            {
                                plot.Add(freqData[k], ampData[k]);
                            }
                        }
                        break;

                    case TraceDataStatus.Average:
                        for (var k = 0; k < ampData.Length; k++)
                        {
                            if (plot.ContainsKey(freqData[k]))
                                plot[freqData[k]] = (plot[freqData[k]] * trace.Average + ampData[k]) / (trace.Average + 1);
                            else
                                plot.Add(freqData[k], ampData[k]);
                        }
                        // Cap average to prevent overflow logic
                        trace.Average = Math.Min(10000, trace.Average + 1);
                        break;
                }
            }
        }
    }

    public static Vector2 ScaleToGraph(float left, float top, float right, float bottom, float freq, float dB,
        double freqStart, double freqStop, double graphStartDb, double graphEndDb)
    {
        var scaledX = Imports.Scale(freq, freqStart, freqStop, left, right);
        var scaledY = Imports.Scale(dB, graphStartDb, graphEndDb, bottom, top);
        return new Vector2((float)scaledX, (float)scaledY);
    }
}