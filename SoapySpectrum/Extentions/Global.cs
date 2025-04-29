using Pothosware.SoapySDR;
using Range = Pothosware.SoapySDR.Range;

namespace SoapySA.Extentions;

public enum saVar
{
    leakageSleep,
    deviecOptions,
    iqCorrection,
    graphStartDB,
    graphStopDB,
    graphOffsetDB,
    graphRefLevel,
    fftWindow,
    fftSize,
    fftSegment,
    fftOverlap,
    refreshRate,
    automaticLevel,
    scalePerDivision
}

public enum traceViewStatus
{
    active,
    clear,
    view
}

public enum traceDataStatus
{
    normal,
    Average,
    maxHold,
    minHold
}

public struct trace
{
    public int average;

    private traceDataStatus datastatus;

    //channel,anntena
    public Tuple<int, int> source;

    public trace()
    {
        plot = new SortedDictionary<float, float>();
        dataStatus = traceDataStatus.normal;
        average = 1;
        viewStatus = traceViewStatus.clear;
    }

    public traceDataStatus dataStatus // property
    {
        get => datastatus; // get method
        set
        {
            average = 1;
            datastatus = value;
            plot.Clear();
        } // set method
    }

    public traceViewStatus viewStatus;
    public SortedDictionary<float, float> plot;
}

public struct channelStreamData
{
    public channelStreamData()
    {
    }

    public double freqStart;
    public double freqStop;
    public bool active;
    public StringList? anntenas;
    public Tuple<string, Range>[]? gains;
    public float[]? gains_values;
    public RangeList? frequencyRange;
    public RangeList? sample_rates;
    public string customSampleRate = "0";
    public int selectedSampleRate;
    public string selectedAnntena = "RX";
    public double sample_rate = 20e6;
}

public class Global
{
    public static int selectedMarker = 0;
    public static int selectedTrace = 0;
    public static uint selectedChannel = 0;
}