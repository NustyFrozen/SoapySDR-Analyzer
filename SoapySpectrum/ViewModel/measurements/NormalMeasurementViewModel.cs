using System.Diagnostics;
using NLog;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapyVNACommon.Extentions;

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
    private readonly MainWindowView _parent = initiator;
    public double RefLevel;
    public float Right;
    public float Top;

    public void UpdateCanvasData(object? sender, KeyOfChangedValueEventArgs e)
    {
        #region Canvas_Data

        try
        {
            DbOffset = (double)_parent.Configuration.Config[Configuration.SaVar.GraphOffsetDb];
            RefLevel = (double)_parent.Configuration.Config[Configuration.SaVar.GraphRefLevel];
            GraphLabelIdx = _parent.AmplitudeView.SScalePerDivision;

            FreqStart = (double)_parent.Configuration.Config[Configuration.SaVar.FreqStart];
            FreqStop = (double)_parent.Configuration.Config[Configuration.SaVar.FreqStop];

            GraphStartDb = (double)_parent.Configuration.Config[Configuration.SaVar.GraphStartDb] + RefLevel;
            GraphEndDb = (double)_parent.Configuration.Config[Configuration.SaVar.GraphStopDb] + RefLevel;
        }
        catch (Exception ex)
        {
            Logger.Error($"error on updateCanvasData -> {ex.Message}");
        }

        #endregion Canvas_Data
    }

    public void CalculateBandPower(Marker marker, List<float> dBArray)
    {
        if (_calculateBandPowerThread is not null)
            if (_calculateBandPowerThread.IsAlive)
                return; //Already in calculations return
        _calculateBandPowerThread = new Thread(() =>
            {
                double tempMarkerBandPowerDecimal = 0;
                foreach (var b in dBArray) tempMarkerBandPowerDecimal += ((double)b).ToMw();
                if (tempMarkerBandPowerDecimal != 0) //not enough values in dbArray --> log(0) --> overflow -inf
                    _parent.MarkerView.SMarkers[marker.Id].BandPowerValue = tempMarkerBandPowerDecimal.ToDBm();
            })
            { Priority = ThreadPriority.Lowest };
        _calculateBandPowerThread.Start();
    }
}