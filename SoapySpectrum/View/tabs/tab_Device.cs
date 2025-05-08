using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using Logger = NLog.Logger;
using Range = Pothosware.SoapySDR.Range;

namespace SoapySA.View.tabs;

public static class tab_Device
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    //UI input pereference
    private static int _comboSelectedDevice = -1;

    private static string[] _comboAvailableDevices = new[] { "No Devices Found" };
    public static uint s_selectedChannel;
    public static string s_selectedAntenna = "TX/RX";
    private static int _selectedSampleRate;
    public static string s_customSampleRate = "0";
    public static float s_osciliatorLeakageSleep;
    public static bool s_isCorrectIQEnabled = true;

    //SDR device Data
    private static StringList _availableAntennas;

    public static Device s_sdrDevice;
    private static uint _availableChannels;
    public static Tuple<string, Range>[] s_deviceGains;
    private static float[] _deviceGainValues;
    private static string[] _deviceSensorData;
    public static Dictionary<int, RangeList> s_deviceFrequencyRange = new();
    public static Dictionary<int, RangeList> s_deviceSampleRates = new();

    public static void setupSoapyEnvironment()
    {
        var currentPath = Path.GetDirectoryName(Application.ExecutablePath);
        var soapyPath = Path.Combine(currentPath, @"SoapySDR");
        var libsPath = Path.Combine(soapyPath, @"Libs");
        Environment.SetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH",
            Path.Combine(currentPath, @"SoapySDR\root\SoapySDR\lib\SoapySDR\modules0.8-3\"));
        Environment.SetEnvironmentVariable("SOAPY_SDR_ROOT", Path.Combine(currentPath, @"SoapySDR\root\SoapySDR"));
        Environment.SetEnvironmentVariable("PATH",
            $"{Environment.GetEnvironmentVariable("PATH")};{soapyPath};{libsPath}");
    }

    private static string insertNewLines(string text, int X)
    {
        if (text.Length <= X) return text; // No need to wrap short items

        var formattedText = new List<char>();
        for (var i = 0; i < text.Length; i++)
        {
            formattedText.Add(text[i]);
            if ((i + 1) % X == 0) formattedText.Add('\n');
        }

        return new string(formattedText.ToArray());
    }

    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public static void refreshDevices()
    {
        var devices = Device.Enumerate().ToList();
        var deviceLabels = new List<string>();
        foreach (var device in devices)
        {
            var idenefiers = string.Empty;
            if (device.ContainsKey("label"))
                idenefiers += $"label={device["label"]},";

            if (device.ContainsKey("driver"))
                idenefiers += $"driver={device["driver"]},";

            if (device.ContainsKey("serial"))
                idenefiers += $"serial={device["serial"]},";

            if (device.ContainsKey("hardware"))
                idenefiers += $"hardware={device["hardware"]}";
            if (idenefiers.EndsWith(","))
                idenefiers = idenefiers.Substring(0, idenefiers.Length - 1);
            deviceLabels.Add(insertNewLines(idenefiers, 60));
        }

        if (deviceLabels.Count > 0)
        {
            _comboAvailableDevices = deviceLabels.ToArray();
            _comboSelectedDevice = 0;
            updateDevice();
        }
        else
        {
            _comboAvailableDevices = new[] { "No Devices Found" };
        }
    }

    public static void updateDevice()
    {
        PerformFFT.isRunning = false;
        try
        {
            s_sdrDevice = new Device(_comboAvailableDevices[_comboSelectedDevice]);
            fetchSDR_Data();
            PerformFFT.beginFFT();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to open Device -> {ex.Message}");
        }
    }

    /// <summary>
    ///     gets all of the sdr data to the ui elements
    /// </summary>
    private static void fetchSDR_Data()
    {
        _availableAntennas = s_sdrDevice.ListAntennas(Direction.Rx, s_selectedChannel);
        _availableChannels = s_sdrDevice.GetNumChannels(Direction.Rx);
        var listgains = s_sdrDevice.ListGains(Direction.Rx, s_selectedChannel);
        s_deviceGains = new Tuple<string, Range>[listgains.Count()];
        _deviceGainValues = new float[listgains.Count()];
        var i = 0;
        foreach (var gain in listgains)
        {
            var range = s_sdrDevice.GetGainRange(Direction.Rx, s_selectedChannel, gain);
            var value = (float)s_sdrDevice.GetGain(Direction.Rx, s_selectedChannel, gain);
            _deviceGainValues[i] = value;
            s_deviceGains[i++] = new Tuple<string, Range>(gain, range);
        }

        var sensors = s_sdrDevice.ListSensors();
        _deviceSensorData = new string[sensors.Count];
        i = 0;
        foreach (var sensor in sensors) _deviceSensorData[i++] = $"{sensor}: {s_sdrDevice.ReadSensor(sensor)}";
        i = 0;
        s_deviceSampleRates.Clear();
        for (; i < _availableChannels; i++)
        {
            s_deviceSampleRates[i] = s_sdrDevice.GetSampleRateRange(Direction.Rx, (uint)i);
            s_deviceSampleRates[i].Add(new Range(0, double.MaxValue, 0));
            s_customSampleRate = s_deviceSampleRates[i].First().Maximum.ToString();
        }

        i = 0;
        s_deviceFrequencyRange.Clear();
        for (; i < _availableChannels; i++)
            s_deviceFrequencyRange.Add(i, s_sdrDevice.GetFrequencyRange(Direction.Rx, (uint)i));
        i = 0;
        s_sdrDevice.SetAntenna(Direction.Rx, s_selectedChannel, s_selectedAntenna);
    }

    public static void renderDeviceData()
    {
        if (s_sdrDevice == null) return;

        Theme.Text("Channel", Theme.inputTheme);
        for (uint i = 0; i < _availableChannels; i++)
            if (ImGui.RadioButton($"{i}", i == s_selectedChannel))
                s_selectedChannel = i;

        Theme.Text("Anntena", Theme.inputTheme);
        foreach (var antenna in _availableAntennas)
            if (ImGui.RadioButton($"{antenna}", antenna == s_selectedAntenna))
            {
                s_selectedAntenna = antenna;
                s_sdrDevice.SetAntenna(Direction.Rx, s_selectedChannel, s_selectedAntenna);
            }

        Theme.Text("Sample Rate", Theme.inputTheme);

        var sampleRateComboData = new List<string>();
        var sampleRateInputData = new List<Range>();
        foreach (var samplerate in s_deviceSampleRates[(int)s_selectedChannel])
            if (samplerate.Minimum == samplerate.Maximum && samplerate.Step == 0)
                //selection
                sampleRateComboData.Add(samplerate.Minimum.ToString());
            else
                //any value between min and max (Input)
                sampleRateInputData.Add(samplerate);

        if (Theme.glowingCombo("sample_rate_Tab", ref _selectedSampleRate, sampleRateComboData.ToArray(),
                Theme.inputTheme))
        {
            Configuration.config[Configuration.saVar.sampleRate] =
                Convert.ToDouble(sampleRateComboData[_selectedSampleRate]);
            PerformFFT.resetIQFilter();
        }

        Theme.Text("Custom Sample Rates", Theme.inputTheme);
        if (sampleRateInputData.Count == 0)
            ImGui.Text("None");
        else
            foreach (var rateRange in sampleRateInputData)
                if (rateRange.Step == 0)
                {
                    //any value
                    Theme.Text($"Between {rateRange.Minimum} - {rateRange.Maximum}", Theme.inputTheme);
                    if (Theme.glowingInput($"rate_range{rateRange.Minimum}_{rateRange.Maximum}", ref s_customSampleRate,
                            Theme.inputTheme))
                    {
                        double customSampleRate = 0;
                        if (double.TryParse(s_customSampleRate, out customSampleRate))
                        {
                            if (rateRange.Minimum <= customSampleRate && rateRange.Maximum >= customSampleRate)
                            {
                                Configuration.config[Configuration.saVar.sampleRate] =
                                    Convert.ToDouble(customSampleRate);
                                PerformFFT.resetIQFilter();
                            }
                            else
                            {
                                _logger.Error("Value is not in the range");
                            }
                        }
                        else
                        {
                            _logger.Error("Value is not A valid double");
                        }
                    }
                }
                else
                {
                    //any value with relation to step
                    double customSampleRate = 0;
                    if (double.TryParse(s_customSampleRate, out customSampleRate))
                    {
                        customSampleRate = Math.Round(customSampleRate / rateRange.Step) * rateRange.Step;
                        if (rateRange.Minimum <= customSampleRate && rateRange.Maximum >= customSampleRate)
                        {
                            Configuration.config[Configuration.saVar.sampleRate] = Convert.ToDouble(customSampleRate);
                            PerformFFT.resetIQFilter();
                        }
                        else
                        {
                            _logger.Error("Value is not in the range");
                        }
                    }
                    else
                    {
                        _logger.Error("Value is not A valid double");
                    }
                }

        Theme.Text("Amplifiers", Theme.inputTheme);
        for (var i = 0; i < s_deviceGains.Count(); i++)
        {
            var gain = s_deviceGains[i];
            var range = gain.Item2;
            ImGui.Text($"{gain.Item1}");
            if (Theme.slider($"{gain.Item1}", (float)range.Minimum, (float)range.Maximum, ref _deviceGainValues[i],
                    Theme.sliderTheme))
            {
                if (range.Step != 0)
                    s_sdrDevice.SetGain(Direction.Rx, s_selectedChannel, gain.Item1,
                        Math.Round(_deviceGainValues[i] / range.Step) * range.Step);
                else
                    //free value
                    s_sdrDevice.SetGain(Direction.Rx, s_selectedChannel, gain.Item1, _deviceGainValues[i]);
            }
        }

        Theme.Text("Sensors Data", Theme.inputTheme);

        foreach (var sensor in _deviceSensorData)
            ImGui.Text(sensor);

        Theme.buttonTheme.text = "Refresh Sensors Data";
        if (Theme.button("Refresh_Sensors", Theme.buttonTheme))
        {
            var i = 0;
            foreach (var sensor in s_sdrDevice.ListSensors())
                _deviceSensorData[i++] = $"{sensor}: {s_sdrDevice.ReadSensor(sensor)}";
        }

        Theme.newLine();
    }

    public static void renderDevice()
    {
        Theme.buttonTheme.text = "Refresh";
        if (Theme.button("Refresh_Devices", Theme.buttonTheme))
            refreshDevices();
        Theme.newLine();
        Theme.Text("SDR", Theme.inputTheme);
        if (Theme.glowingCombo("devicetabs", ref _comboSelectedDevice, _comboAvailableDevices, Theme.inputTheme))
            updateDevice();
        Theme.newLine();
        renderDeviceData();
        Theme.newLine();
        Theme.Text("LO/PLL Leakage sleep", Theme.inputTheme);
        if (Theme.slider("Leakage", ref s_osciliatorLeakageSleep, Theme.sliderTheme))
        {
            Configuration.config[Configuration.saVar.leakageSleep] = (int)(s_osciliatorLeakageSleep * 100);
            _logger.Debug(Configuration.config[Configuration.saVar.leakageSleep]);
        }

        if (ImGui.Checkbox("IQ correction", ref s_isCorrectIQEnabled))
            Configuration.config[Configuration.saVar.iqCorrection] = s_isCorrectIQEnabled;
    }
}