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

    public double rxSampleRate, txSampleRate;

    //channel, anntennas
    public Dictionary<uint, StringList> availableRxAntennas, availableTxAntennas;

    public uint availableRxChannels, availableTxChannels;
    public Dictionary<int, RangeList> deviceRxFrequencyRange = new(), deviceTxFrequencyRange = new();
    public Dictionary<int, RangeList> deviceRxSampleRates = new(), deviceTxSampleRates = new();
    public Dictionary<Tuple<uint, string>, Tuple<Range, int>> rxGains = new Dictionary<Tuple<uint, string>, Tuple<Range, int>>(), txGains = new Dictionary<Tuple<uint, string>, Tuple<Range, int>>();
    public List<double> rxGainValues = new List<double>(), txGainValues = new List<double>();
    public string Descriptor;
    public string sensorData;

    public sdrDeviceCOM(string sdrKwargs)
    {
        Descriptor = sdrKwargs;
        sdrDevice = new Device(sdrKwargs);
    }

    public sdrDeviceCOM(sdrDeviceCOM cpy)
    {
        this = cpy;
    }

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
        sensorData = string.Empty;
        uint i = 0;
        int GainCounter = 0;
        for (; i < availableRxChannels; i++)
        {
            var gains = sdrDevice.ListGains(Direction.Rx, i).ToArray();
            foreach (var gain in gains)
            {
                rxGainValues.Add(sdrDevice.GetGain(Direction.Rx, i, gain));
                rxGains.Add(new Tuple<uint, string>(i, gain),
                    new Tuple<Range, int>(sdrDevice.GetGainRange(Direction.Rx, i, gain), GainCounter++));

            }

            availableRxAntennas.Add(i, sdrDevice.ListAntennas(Direction.Rx, i));
            deviceRxSampleRates[(int)i] = sdrDevice.GetSampleRateRange(Direction.Rx, i);
            deviceRxSampleRates[(int)i].Add(new Range(0, double.MaxValue, 0));
            deviceRxFrequencyRange.Add((int)i, sdrDevice.GetFrequencyRange(Direction.Rx, i));
        }
        rxSampleRate = deviceRxSampleRates[0].OrderByDescending(x => x.Maximum).First().Maximum;
        i = 0;
        GainCounter = 0;
        for (; i < availableTxChannels; i++)
        {
            var gains = sdrDevice.ListGains(Direction.Tx, i).ToArray();
            foreach (var gain in gains)
            {
                txGainValues.Add(sdrDevice.GetGain(Direction.Tx, i, gain));
                txGains.Add(new Tuple<uint, string>(i, gain),
                    new Tuple<Range, int>(sdrDevice.GetGainRange(Direction.Tx, i, gain), GainCounter++));
            }

            availableTxAntennas.Add(i, sdrDevice.ListAntennas(Direction.Tx, i));
            deviceTxSampleRates[(int)i] = sdrDevice.GetSampleRateRange(Direction.Tx, i);
            deviceTxSampleRates[(int)i].Add(new Range(0, double.MaxValue, 0));
            deviceTxFrequencyRange.Add((int)i, sdrDevice.GetFrequencyRange(Direction.Tx, i));
        }
        txSampleRate = deviceTxSampleRates[0].OrderByDescending(x => x.Maximum).First().Maximum;
        var sensors = sdrDevice.ListSensors();

        i = 0;
        foreach (var sensor in sensors) this.sensorData += $"{sensor}: {sdrDevice.ReadSensor(sensor)}\n";
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

public class Global
{
    public static int selectedMarker = 0;
    public static int selectedTrace = 0;
    public static uint selectedChannel = 0;
}