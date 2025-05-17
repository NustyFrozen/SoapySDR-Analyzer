using Pothosware.SoapySDR;
using Range = Pothosware.SoapySDR.Range;

namespace SoapyVNACommon.Extentions;

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

public struct sdrDeviceCOM
{
    public Device sdrDevice;
    private string[] deviceSensorData;

    //channel,anntenna
    public Tuple<uint, string> rxAntenna = new Tuple<uint, string>(0, string.Empty), txAntenna = new Tuple<uint, string>(0, string.Empty);

    private double RxSampleRate, TxSampleRate;

    //channel, anntennas
    public Dictionary<uint, StringList> availableRxAntennas, availableTxAntennas;

    public uint availableRxChannels, availableTxChannels;
    public Dictionary<int, RangeList> deviceRxFrequencyRange = new(), deviceTxFrequencyRange = new();
    public Dictionary<int, RangeList> deviceRxSampleRates = new(), deviceTxSampleRates = new();

    public sdrDeviceCOM(string sdrKwargs) => sdrDevice = new Device(sdrKwargs);

    public sdrDeviceCOM(Device sdr) => sdrDevice = sdr;

    public void fetchSDRData()
    {
        availableRxChannels = sdrDevice.GetNumChannels(Direction.Rx);
        availableTxChannels = sdrDevice.GetNumChannels(Direction.Tx);
        availableRxAntennas = new Dictionary<uint, StringList>();
        availableTxAntennas = new Dictionary<uint, StringList>();

        deviceRxSampleRates.Clear();
        deviceTxSampleRates.Clear();
        deviceRxFrequencyRange.Clear();
        deviceTxFrequencyRange.Clear();

        var i = 0;
        for (; i < availableRxChannels; i++)
        {
            availableRxAntennas.Add((uint)i, sdrDevice.ListAntennas(Direction.Rx, (uint)i));
            deviceRxSampleRates[i] = sdrDevice.GetSampleRateRange(Direction.Rx, (uint)i);
            deviceRxSampleRates[i].Add(new Range(0, double.MaxValue, 0));
            deviceRxFrequencyRange.Add(i, sdrDevice.GetFrequencyRange(Direction.Rx, (uint)i));
        }
        RxSampleRate = deviceRxSampleRates[0].OrderByDescending(x => x.Maximum).First().Maximum;
        i = 0;
        for (; i < availableTxChannels; i++)
        {
            availableTxAntennas.Add((uint)i, sdrDevice.ListAntennas(Direction.Tx, (uint)i));
            deviceTxSampleRates[i] = sdrDevice.GetSampleRateRange(Direction.Tx, (uint)i);
            deviceTxSampleRates[i].Add(new Range(0, double.MaxValue, 0));
            deviceTxFrequencyRange.Add(i, sdrDevice.GetFrequencyRange(Direction.Tx, (uint)i));
        }
        TxSampleRate = deviceTxSampleRates[0].OrderByDescending(x => x.Maximum).First().Maximum;

        var sensors = sdrDevice.ListSensors();
        deviceSensorData = new string[sensors.Count];
        i = 0;
        foreach (var sensor in sensors) deviceSensorData[i++] = $"{sensor}: {sdrDevice.ReadSensor(sensor)}";
    }
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