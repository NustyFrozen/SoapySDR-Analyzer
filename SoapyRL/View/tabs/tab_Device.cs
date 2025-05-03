using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;
using Range = Pothosware.SoapySDR.Range;

namespace SoapyRL.View.tabs;

public static class tab_Device
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    //UI input pereference
    private static int _comboSelectedDevice = -1;

    private static string[] _comboAvailableDevices = new[] { "No Devices Found" };
    public static string s_selectedReflectAntenna = "TX/RX", s_selectedForwardAntenna = "TX/RX";
    public static float s_osciliatorLeakageSleep, s_validRangeTolerance = 90.0f;
    public static bool s_isCorrectIQEnabled = true;
    public static bool s_showValidRange;

    //SDR device Data
    private static StringList _availableReflectAntennas, _availableForwardAntennas;

    public static Device s_sdrDevice;
    public static Tuple<string, Range>[] s_deviceTxGains, s_deviceRxGains;
    private static float[] _deviceTxGainValues, _deviceRxGainValues;
    private static RangeList s_transmitRange, s_receiveRange;

    public static string s_displayFreqStart = "800M", s_displayFreqStop = "1000M";

    private static Theme.glowingInputConfigurator inputTheme = Theme.getTextTheme();
    private static Theme.ButtonConfigurator buttonTheme = Theme.getButtonTheme();
    private static readonly Theme.SliderInputConfigurator sliderTheme = Theme.getSliderTheme();

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

    private static string InsertNewlines(string text, int X)
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
            deviceLabels.Add(InsertNewlines(idenefiers, 60));
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
        PerformRL.isRunning = false;
        try
        {
            s_sdrDevice = new Device(_comboAvailableDevices[_comboSelectedDevice]);
            fetchSDR_Data();
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
        var receive = s_sdrDevice.GetNumChannels(Direction.Rx);
        var transmit = s_sdrDevice.GetNumChannels(Direction.Tx);
        if (receive == 0 || transmit == 0 || !s_sdrDevice.GetFullDuplex(Direction.Rx, 0))
            MessageBox.Show("This SDR doesn't support Return Loss\n" +
                            "a receive and transmit channel is required\n" +
                            "and full duplex capabilities");
        _availableReflectAntennas = s_sdrDevice.ListAntennas(Direction.Rx, 0);
        _availableForwardAntennas = s_sdrDevice.ListAntennas(Direction.Tx, 0);
        var gains = s_sdrDevice.ListGains(Direction.Tx, 0);
        var tempList = new List<Tuple<string, Range>>();
        foreach (var ga in gains)
            tempList.Add(new Tuple<string, Range>(ga, s_sdrDevice.GetGainRange(Direction.Tx, 0, ga)));
        s_deviceTxGains = tempList.ToArray();
        gains = s_sdrDevice.ListGains(Direction.Rx, 0);
        _deviceTxGainValues = new float[tempList.Count];
        tempList.Clear();
        foreach (var ga in gains)
            tempList.Add(new Tuple<string, Range>(ga, s_sdrDevice.GetGainRange(Direction.Rx, 0, ga)));
        s_deviceRxGains = tempList.ToArray();
        _deviceRxGainValues = new float[tempList.Count];

        s_transmitRange = s_sdrDevice.GetFrequencyRange(Direction.Tx, 0);
        s_receiveRange = s_sdrDevice.GetFrequencyRange(Direction.Rx, 0);

        //clipping frequency ranges
        Configuration.config[Configuration.saVar.freqStart] = Math.Max(
            s_transmitRange.OrderBy(x => x.Minimum).First().Minimum,
            s_receiveRange.OrderBy(x => x.Minimum).First().Minimum);
        Configuration.config[Configuration.saVar.freqStop] = Math.Min(
            s_transmitRange.OrderByDescending(x => x.Maximum).First().Maximum,
            s_receiveRange.OrderByDescending(x => x.Maximum).First().Maximum);

        Configuration.config[Configuration.saVar.txSampleRate] = s_sdrDevice.GetSampleRateRange(Direction.Tx, 0)
            .OrderBy(x => x.Maximum).Last().Maximum;
        Configuration.config[Configuration.saVar.rxSampleRate] = s_sdrDevice.GetSampleRateRange(Direction.Rx, 0)
            .OrderBy(x => x.Maximum).Last().Maximum;

#if DEBUG
            Configuration.config[Configuration.saVar.freqStart] = 100e6;
            Configuration.config[Configuration.saVar.freqStop] = 100e7;

#endif
    }

    public static void renderDeviceData()
    {
        var inputTheme = Theme.getTextTheme();

        if (s_sdrDevice == null) return;

        Theme.Text("Forward Anntena", inputTheme);
        foreach (var antenna in _availableForwardAntennas)
            if (ImGui.RadioButton($"{antenna}", antenna == s_selectedForwardAntenna))
            {
                s_selectedForwardAntenna = antenna;
                s_sdrDevice.SetAntenna(Direction.Tx, 0, antenna);
            }

        Theme.Text("Reflect Anntena", inputTheme);
        foreach (var antenna in _availableReflectAntennas)
            if (ImGui.RadioButton($"{antenna}", antenna == s_selectedReflectAntenna))
            {
                s_selectedReflectAntenna = antenna;
                s_sdrDevice.SetAntenna(Direction.Rx, 0, antenna);
            }

        ImGui.Text($"Forward Amplifiers\n" +
                   $"WARNING you are reflecting power into your sdr\n" +
                   $"please check your sdr's transmit and receive power capabilities\n" +
                   $"before applying gains");
        for (var i = 0; i < s_deviceTxGains.Count(); i++)
        {
            var gain = s_deviceTxGains[i];
            var range = gain.Item2;
            ImGui.Text($"{gain.Item1}");
            if (Theme.slider($"{gain.Item1}_tx", (float)range.Minimum, (float)range.Maximum, ref _deviceTxGainValues[i],
                    sliderTheme))
            {
                if (range.Step != 0)
                    s_sdrDevice.SetGain(Direction.Tx, 0, gain.Item1,
                        Math.Round(_deviceTxGainValues[i] / range.Step) * range.Step);
                else
                    //free value
                    s_sdrDevice.SetGain(Direction.Tx, 0, gain.Item1, _deviceTxGainValues[i]);
            }
        }
        ImGui.Text($"Reflection Amplifiers\n");
        for (var i = 0; i < s_deviceRxGains.Count(); i++)
        {
            var gain = s_deviceRxGains[i];
            var range = gain.Item2;
            ImGui.Text($"{gain.Item1}");
            if (Theme.slider($"{gain.Item1}_rx", (float)range.Minimum, (float)range.Maximum, ref _deviceRxGainValues[i],
                    sliderTheme))
            {
                if (range.Step != 0)
                    s_sdrDevice.SetGain(Direction.Rx, 0, gain.Item1,
                        Math.Round(_deviceRxGainValues[i] / range.Step) * range.Step);
                else
                    //free value
                    s_sdrDevice.SetGain(Direction.Rx, 0, gain.Item1, _deviceRxGainValues[i]);
            }
        }

        Theme.newLine();
    }

    private static bool tryFormatFreq(string input, out double value)
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

    public static void renderDevice()
    {
        Theme.newLine();
        Theme.newLine();
        Theme.newLine();
        Theme.newLine();
        buttonTheme.text = "Refresh";
        if (Theme.button("Refresh_Devices", buttonTheme))
            refreshDevices();
        Theme.newLine();
        Theme.Text("SDR", inputTheme);
        if (Theme.glowingCombo("devicetabs", ref _comboSelectedDevice, _comboAvailableDevices, inputTheme))
            updateDevice();
        Theme.newLine();
        Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", inputTheme);
        inputTheme.prefix = " start Frequency";
        var hasFrequencyChanged = Theme.glowingInput("InputSelectortext", ref s_displayFreqStart, inputTheme);
        Theme.Text($"{FontAwesome5.ArrowRight} Right Band", inputTheme);
        inputTheme.prefix = "End Frequency";
        hasFrequencyChanged |= Theme.glowingInput("InputSelectortext2", ref s_displayFreqStop, inputTheme);

        if (hasFrequencyChanged) //apply frequency change in settings
        {
            double freqStart, freqStop;
            if (tryFormatFreq(s_displayFreqStart, out freqStart) && tryFormatFreq(s_displayFreqStop, out freqStop))
            {
                if (freqStart >= freqStop ||
                    !(Math.Max(s_transmitRange.OrderBy(x => x.Minimum).First().Minimum,
                          s_receiveRange.OrderBy(x => x.Minimum).First().Minimum) <= freqStart
                      && Math.Min(s_transmitRange.OrderByDescending(x => x.Maximum).First().Maximum,
                          s_receiveRange.OrderByDescending(x => x.Maximum).First().Maximum) >= freqStop))
                {
                    _logger.Error("$ Start or End Frequency is not valid");
                }
                else
                {
                    Configuration.config[Configuration.saVar.freqStart] = freqStart;
                    Configuration.config[Configuration.saVar.freqStop] = freqStop;
                }
            }
            else
            {
                _logger.Error("$ Start or End Frequency span is not a valid double");
            }
        }
        ImGui.Checkbox("Show Valid Impedance Range", ref s_showValidRange);
        if (s_showValidRange)
        {
            Theme.Text("Valid Impedance min forward", inputTheme);
            if (Theme.slider("valid Impadance Range", ref s_validRangeTolerance, sliderTheme))
            {
                Configuration.config[Configuration.saVar.validImpedanceTol] = s_validRangeTolerance;
            }
        }
        Theme.newLine();
        renderDeviceData();
        Theme.newLine();
        Theme.Text("LO/PLL Leakage sleep", inputTheme);
        if (Theme.slider("Leakage", ref s_osciliatorLeakageSleep, sliderTheme))
        {
            Configuration.config[Configuration.saVar.leakageSleep] = (int)(s_osciliatorLeakageSleep * 100);
            _logger.Debug(Configuration.config[Configuration.saVar.leakageSleep]);
        }

        if (ImGui.Checkbox("IQ correction", ref s_isCorrectIQEnabled))
            Configuration.config[Configuration.saVar.iqCorrection] = s_isCorrectIQEnabled;
        if (PerformRL.isFFTQueueEmpty() && !PerformRL.isRunning)
        {
            buttonTheme.text = "Sweep Reference (open port)";
            if (Theme.button("Sweep", buttonTheme))
            {
                for (var i = 0; i < tab_Trace.s_traces.Length; i++)
                {
                    tab_Trace.s_traces[i].viewStatus = tab_Trace.traceViewStatus.clear;
                    tab_Trace.s_traces[i].plot.Clear();
                }

                tab_Trace.s_traces[0].viewStatus = tab_Trace.traceViewStatus.active;
                PerformRL.beginRL();
            }
            Theme.newLine();
            buttonTheme.text = "Sweep range (closed port, optional)";
            if (Theme.button("Sweep", buttonTheme))
            {
                for (var i = 0; i < tab_Trace.s_traces.Length; i++)
                {
                    tab_Trace.s_traces[i].viewStatus = tab_Trace.traceViewStatus.clear;
                    tab_Trace.s_traces[i].plot.Clear();
                }

                tab_Trace.s_traces[2].viewStatus = tab_Trace.traceViewStatus.active;
                PerformRL.beginRL();
            }
            Theme.newLine();
            buttonTheme.text = "Sweep Results";
            if (Theme.button("Sweep", buttonTheme))
            {
                for (var i = 0; i < tab_Trace.s_traces.Length; i++)
                    tab_Trace.s_traces[i].viewStatus = tab_Trace.traceViewStatus.clear;
                tab_Trace.s_traces[1].plot.Clear();
                tab_Trace.s_traces[1].viewStatus = tab_Trace.traceViewStatus.active;
                PerformRL.beginRL();
            }
        }
        else
        {
            Theme.Text("Performing sweep...");
            Theme.Text("(to abort click End)");
        }
        Theme.newLine();
        ImGui.Checkbox("Enable continuous sweep", ref PerformRL.continous);
    }
}