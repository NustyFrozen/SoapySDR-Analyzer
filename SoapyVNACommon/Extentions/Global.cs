

using Pothosware.SoapySDR;
using Range = Pothosware.SoapySDR.Range;

namespace SoapyVNACommon.Extentions;

public static class Global
{
    public static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
    public static readonly string CalibrationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calibration");

    public static bool TryFormatFreq(string input, out double value)
    {
        input = input.ToUpper();
        double exponent = 1;
        if (input.Contains("K"))
            exponent = 1e3;
        if (input.Contains("M"))
            exponent = 1e6;
        if (input.Contains("G"))
            exponent = 1e9;
        double results = 80000000;
        if (!double.TryParse(input.Replace("K", "").Replace("M", "").Replace("G", ""), out results))
        {
            value = 0;
            return false;
        }

        value = results * exponent;
        return true;
    }
}

public struct SdrDeviceCom
{
    public Device SdrDevice;
    private string[] _deviceSensorData;

    /// <summary>
    /// channel, anntennas
    /// </summary>
    public Tuple<uint, string> RxAntenna = new(0, string.Empty), TxAntenna = new(0, string.Empty);

    public double RxSampleRate, TxSampleRate;

    /// <summary>
    /// channel, anntennas
    /// </summary>
    public Dictionary<uint, StringList> AvailableRxAntennas, AvailableTxAntennas;

    public uint AvailableRxChannels, AvailableTxChannels;
    
    public Dictionary<int, RangeList> DeviceRxFrequencyRange = new(), DeviceTxFrequencyRange = new();
    /// <summary>
    /// channel, sample rates
    /// </summary>
    public Dictionary<int, RangeList> DeviceRxSampleRates = new(), DeviceTxSampleRates = new();

    /// <summary>
    /// (channel, Antenna), (gain,value)
    /// </summary>
    public Dictionary<Tuple<uint, string>, Tuple<Range, int>> RxGains = new(), TxGains = new();

    public List<double> RxGainValues = new(), TxGainValues = new();
    public string Descriptor;
    public string SensorData;

    /// <summary>
    ///     Extra driver arguments the device was opened with - UHD transport buffer sizing and the like.
    ///     Kept apart from <see cref="Descriptor" /> so the label stays readable, and carried in the widget
    ///     file so a restart reopens the device exactly the same way. Only ever applied at open time:
    ///     transports are built when the driver constructs, long before a stream is set up.
    /// </summary>
    public string DeviceArgs = string.Empty;

    public SdrDeviceCom(string sdrKwargs)
        : this(sdrKwargs, string.Empty)
    {
    }

    public SdrDeviceCom(string sdrKwargs, string? deviceArgs)
    {
        Descriptor = sdrKwargs;
        DeviceArgs = deviceArgs?.Trim() ?? string.Empty;
        SdrDevice = new Device(OpenArguments(sdrKwargs, DeviceArgs));
    }

    /// <summary>The kwargs string a device is actually opened with.</summary>
    public static string OpenArguments(string descriptor, string? deviceArgs)
    {
        if (string.IsNullOrWhiteSpace(deviceArgs))
            return descriptor;

        var extra = deviceArgs.Trim().TrimStart(',');
        return string.IsNullOrWhiteSpace(descriptor) ? extra : $"{descriptor},{extra}";
    }

    public SdrDeviceCom(SdrDeviceCom cpy)
    {
        this = cpy;
    }

    public void FetchSdrData()
    {
        AvailableRxChannels = SdrDevice.GetNumChannels(Direction.Rx);
        AvailableTxChannels = SdrDevice.GetNumChannels(Direction.Tx);
        AvailableRxAntennas = new Dictionary<uint, StringList>();
        AvailableTxAntennas = new Dictionary<uint, StringList>();

        DeviceRxSampleRates.Clear();
        DeviceTxSampleRates.Clear();
        DeviceRxFrequencyRange.Clear();
        DeviceTxFrequencyRange.Clear();
        SensorData = string.Empty;
        uint i = 0;
        var gainCounter = 0;
        for (; i < AvailableRxChannels; i++)
        {
            var gains = SdrDevice.ListGains(Direction.Rx, i).ToArray();
            foreach (var gain in gains)
            {
                RxGainValues.Add(SdrDevice.GetGain(Direction.Rx, i, gain));
                RxGains.Add(new Tuple<uint, string>(i, gain),
                    new Tuple<Range, int>(SdrDevice.GetGainRange(Direction.Rx, i, gain), gainCounter++));
            }

            AvailableRxAntennas.Add(i, SdrDevice.ListAntennas(Direction.Rx, i));
            DeviceRxSampleRates[(int)i] = SdrDevice.GetSampleRateRange(Direction.Rx, i);
            DeviceRxSampleRates[(int)i].Add(new Range(0, double.MaxValue, 0));
            DeviceRxFrequencyRange.Add((int)i, SdrDevice.GetFrequencyRange(Direction.Rx, i));
        }

        i = 0;
        gainCounter = 0;
        for (; i < AvailableTxChannels; i++)
        {
            var gains = SdrDevice.ListGains(Direction.Tx, i).ToArray();
            foreach (var gain in gains)
            {
                TxGainValues.Add(SdrDevice.GetGain(Direction.Tx, i, gain));
                TxGains.Add(new Tuple<uint, string>(i, gain),
                    new Tuple<Range, int>(SdrDevice.GetGainRange(Direction.Tx, i, gain), gainCounter++));
            }

            AvailableTxAntennas.Add(i, SdrDevice.ListAntennas(Direction.Tx, i));
            DeviceTxSampleRates[(int)i] = SdrDevice.GetSampleRateRange(Direction.Tx, i);
            DeviceTxSampleRates[(int)i].Add(new Range(0, double.MaxValue, 0));
            DeviceTxFrequencyRange.Add((int)i, SdrDevice.GetFrequencyRange(Direction.Tx, i));
        }

        var sensors = SdrDevice.ListSensors();
        foreach (var sensor in sensors) SensorData += $"{sensor}: {SdrDevice.ReadSensor(sensor)}\n";
    }
}