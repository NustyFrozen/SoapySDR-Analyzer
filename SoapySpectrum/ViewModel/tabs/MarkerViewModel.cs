using System.Diagnostics;
using NLog;
using SoapySA.Model;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class MarkerView
{
    public override string tabName => $"{FontAwesome5.Marker} Markers";
    private static readonly string[] MarkerCombo = new[]
        { "Marker 1", "Marker 2", "Marker 3", "Marker 4", "Marker 5", "Marker 6", "Marker 7", "Marker 8", "Marker 9" };

    public static string[] SMarkerTraceCombo;
    public static string[] SMarkerRefPoint = new[] { "trace" }.Concat(MarkerCombo).ToArray();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly MainWindowView _parent = initiator;
    public Marker[] SMarkers = new Marker[9];
    public int SSelectedMarker;

    public void MarkerMoveNext(Marker marker)
    {
        lock (_parent.TraceView.STraces[marker.Reference].Plot) //could get updateData so we gotta lock it up
        {
            var plotData = _parent.TraceView.STraces[marker.Reference].Plot.ToArray();
            for (var i = 0; i < plotData.Length; i++)
                if (plotData[i].Key == marker.Position)
                {
                    if (i + 1 == plotData.Length) return; //you are out of the bounderies
                    marker.Position = plotData[i + 1].Key;
                    return;
                }
        }
    }

    public void MarkerMovePrevious(Marker marker)
    {
        lock (_parent.TraceView.STraces[marker.Reference].Plot) //could get updateData so we gotta lock it up
        {
            var plotData = _parent.TraceView.STraces[marker.Reference].Plot.ToArray();
            for (var i = 0; i < plotData.Length; i++)
                if (plotData[i].Key == marker.Position)
                {
                    if (i == 0) return; //you are out of the bounderies
                    marker.Position = plotData[i - 1].Key;
                    return;
                }
        }
    }

    public void MarkerSetDelta(int markerid)
    {
        SMarkers[markerid].DeltaFreq = SMarkers[markerid].Position;
        SMarkers[markerid].DeltadB = SMarkers[markerid].Value;
    }

    public void PeakSearch(Marker marker, float minimumFreq, float maxFreq)
    {
        lock (_parent.TraceView.STraces[marker.Reference].Plot) //could get updateData so we gotta lock it up
        {
            var peakPointArry = _parent.TraceView.STraces[marker.Reference].Plot
                .Where(x => x.Key >= minimumFreq && x.Key <= maxFreq).OrderByDescending(entry => entry.Value).ToList();
            SMarkers[marker.Id].Position = peakPointArry[0].Key;
            SMarkers[marker.Id].Value = peakPointArry[0].Value;
        }
    }
}