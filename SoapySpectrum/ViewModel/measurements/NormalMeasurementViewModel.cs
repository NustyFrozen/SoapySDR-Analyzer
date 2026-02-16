using System.Diagnostics;
using NLog;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using static SoapySA.Configuration;


namespace SoapySA.View.measurements;

public partial class NormalMeasurementView
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static Stopwatch SWaitForMouseClick = new();

    private Thread? _calculateBandPowerThread;

    public float Bottom;
    public double DbOffset;
    public double FreqStart;
    public double FreqStop;
    public double GraphEndDb;
    public double GraphStartDb;
    public float GraphLabelIdx;
    public float Left;
    public double RefLevel;
    public float Right;
    public float Top;

    // Kept signature for compatibility with existing callers,
    // but now it simply refreshes from strongly-typed config.
    public void UpdateCanvasData(object? sender, EventArgs e)
        => UpdateCanvasDataFromConfig();
    private void UpdateCanvasDataFromConfig()
    {
        try
        {
            DbOffset = _config.GraphOffsetDb;
            RefLevel = _config.GraphRefLevel;
            GraphLabelIdx = _config.ScalePerDivision;

            FreqStart = _config.FreqStart;
            FreqStop = _config.FreqStop;

            GraphStartDb = _config.GraphStartDb + RefLevel;
            GraphEndDb = _config.GraphStopDb + RefLevel;
        }
        catch (Exception ex)
        {
            Logger.Error($"error on updateCanvasData -> {ex.Message}");
        }
    }
    public override bool renderSettings() => false;
    public void CalculateBandPower(Marker marker, List<float> dBArray)
    {
        if (_calculateBandPowerThread is not null && _calculateBandPowerThread.IsAlive)
            return;

        _calculateBandPowerThread = new Thread(() =>
        {
            double tempMarkerBandPowerDecimal = 0;
            foreach (var b in dBArray) tempMarkerBandPowerDecimal += ((double)b).ToMw();
            if (tempMarkerBandPowerDecimal != 0)
                _graphData.Markers[marker.Id].BandPowerValue = tempMarkerBandPowerDecimal.ToDBm();
        })
        { Priority = ThreadPriority.Lowest };

        _calculateBandPowerThread.Start();
    }
}