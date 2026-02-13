using System.Diagnostics;
using NLog;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using static SoapySA.Configuration;
using SaVar = SoapySA.Configuration.SaVar;


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

    public override string tabName => "Normal Measurement";

    public void UpdateCanvasData(object? sender, KeyOfChangedValueEventArgs e)
    {
        #region Canvas_Data

        try
        {
            DbOffset = (double)Config[SaVar.GraphOffsetDb];
            RefLevel = (double)Config[SaVar.GraphRefLevel];
            GraphLabelIdx = (int)Config[Configuration.SaVar.ScalePerDivision];

            FreqStart = (double)Config[Configuration.SaVar.FreqStart];
            FreqStop = (double)Config[Configuration.SaVar.FreqStop];

            GraphStartDb = (double)Config[Configuration.SaVar.GraphStartDb] + RefLevel;
            GraphEndDb = (double)Config[Configuration.SaVar.GraphStopDb] + RefLevel;
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
                    SMarkers[marker.Id].BandPowerValue = tempMarkerBandPowerDecimal.ToDBm();
            })
            { Priority = ThreadPriority.Lowest };
        _calculateBandPowerThread.Start();
    }

    public override void UpdateUIView(object? sender, KeyOfChangedValueEventArgs e)
    {
        //nop
    }
}