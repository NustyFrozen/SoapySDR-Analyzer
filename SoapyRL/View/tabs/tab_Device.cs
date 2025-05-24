using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapyRL.View.tabs;

public class tab_Device(MainWindow initiator, sdrDeviceCOM com)
{
    private MainWindow parent = initiator;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public bool isCorrectIQEnabled = true, isShowValidRangeEnabled;
    public sdrDeviceCOM deviceCOM = com;
    public string[] gainRxValues = new string[com.rxGainValues.Count], gainTxValues = new string[com.txGainValues.Count];
    private bool initialized = false;
    public string s_osciliatorLeakageSleep = "0";
    public float s_validRangeTolerance = 90.0f;
    public bool s_isCorrectIQEnabled = true;
    public bool s_showValidRange;

    public string s_displayFreqStart = "800M", s_displayFreqStop = "1000M";

    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public void renderDeviceData()
    {
        if (!initialized)
        {
            for (int i = 0; i < gainRxValues.Length; i++)
            {
                gainRxValues[i] = com.rxGainValues[i].ToString();
            }
            for (int i = 0; i < gainTxValues.Length; i++)
            {
                gainTxValues[i] = com.txGainValues[i].ToString();
            }
            initialized = true;
        }
        ImGui.Text($"{FontAwesome5.Microchip} {deviceCOM.Descriptor}\n" +
                   $"Reflection:\nCH {deviceCOM.rxAntenna.Item1}\n" +
                   $"ANT {deviceCOM.rxAntenna.Item2}\n" +
                   $"Foward:\nCH {deviceCOM.txAntenna.Item1}\n" +
                   $"ANT {deviceCOM.txAntenna.Item2}");
        Theme.Text("TX Amplifiers", Theme.inputTheme);
        foreach (var gainElm in deviceCOM.txGains)
            if (gainElm.Key.Item1 == deviceCOM.txAntenna.Item1)
            {
                var gain = gainTxValues[gainElm.Value.Item2];
                var range = gainElm.Value.Item1;
                ImGui.Text($"{gainElm.Key.Item2} {range.Minimum} - {range.Maximum}");
                if (Theme.glowingInput($"{gainElm.Key.Item2}_tx", ref gainTxValues[gainElm.Value.Item2],
                        Theme.inputTheme))
                {
                    double results = 0;
                    bool valid = double.TryParse(gainTxValues[gainElm.Value.Item2], out results);
                    valid |= results >= range.Minimum && results <= range.Maximum;
                    if (!valid)
                    {
                        _logger.Error("invalid Double Value or value out ouf range");
                    }
                    else
                    {
                        if (range.Step != 0)
                            deviceCOM.sdrDevice.SetGain(Direction.Tx, deviceCOM.txAntenna.Item1, gainElm.Key.Item2,
                                Math.Round(results / range.Step) * range.Step);
                        else
                            //free value
                            deviceCOM.sdrDevice.SetGain(Direction.Tx, deviceCOM.txAntenna.Item1, gainElm.Key.Item2, results);
                    }
                }
            }

        Theme.Text("RX Amplifiers", Theme.inputTheme);
        foreach (var gainElm in deviceCOM.rxGains)
            if (gainElm.Key.Item1 == deviceCOM.rxAntenna.Item1)
            {
                var gain = gainRxValues[gainElm.Value.Item2];
                var range = gainElm.Value.Item1;
                ImGui.Text($"{gainElm.Key.Item2} {range.Minimum} - {range.Maximum}");
                if (Theme.glowingInput($"{gainElm.Key.Item2}_rx", ref gainRxValues[gainElm.Value.Item2],
                        Theme.inputTheme))
                {
                    double results = 0;
                    bool valid = double.TryParse(gainRxValues[gainElm.Value.Item2], out results);
                    valid |= results >= range.Minimum && results <= range.Maximum;
                    if (!valid)
                    {
                        _logger.Error("invalid Double Value or value ot ouf range");
                    }
                    else
                    {
                        if (range.Step != 0)
                            deviceCOM.sdrDevice.SetGain(Direction.Rx, deviceCOM.rxAntenna.Item1, gainElm.Key.Item2,
                                Math.Round(results / range.Step) * range.Step);
                        else
                            //free value
                            deviceCOM.sdrDevice.SetGain(Direction.Rx, deviceCOM.rxAntenna.Item1, gainElm.Key.Item2, results);
                    }
                }
            }
        Theme.Text($"Sensors Data\n{deviceCOM.sensorData}", Theme.inputTheme);

        Theme.buttonTheme.text = "Refresh Sensors Data";
        if (Theme.button("Refresh_Sensors", Theme.buttonTheme))
        {
            // var i = 0;
            // foreach (var sensor in s_sdrDevice.ListSensors())
            //     _deviceSensorData[i++] = $"{sensor}: {s_sdrDevice.ReadSensor(sensor)}";
        }
    }

    public void renderDevice()
    {
        Theme.newLine();
        renderDeviceData();
        Theme.newLine();
        Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", Theme.inputTheme);
        Theme.inputTheme.prefix = " start Frequency";
        var hasFrequencyChanged = Theme.glowingInput("InputSelectortext", ref s_displayFreqStart, Theme.inputTheme);
        Theme.Text($"{FontAwesome5.ArrowRight} Right Band", Theme.inputTheme);
        Theme.inputTheme.prefix = "End Frequency";
        hasFrequencyChanged |= Theme.glowingInput("InputSelectortext2", ref s_displayFreqStop, Theme.inputTheme);

        if (hasFrequencyChanged) //apply frequency change in settings
        {
            double freqStart, freqStop;
            if (Global.TryFormatFreq(s_displayFreqStart, out freqStart) && Global.TryFormatFreq(s_displayFreqStop, out freqStop))
            {
                if (freqStart >= freqStop ||
                    !(Math.Max(deviceCOM.deviceTxFrequencyRange[(int)deviceCOM.txAntenna.Item1].OrderBy(x => x.Minimum).First().Minimum,
                          deviceCOM.deviceRxFrequencyRange[(int)deviceCOM.rxAntenna.Item1].OrderBy(x => x.Minimum).First().Minimum) <= freqStart
                      && Math.Min(deviceCOM.deviceTxFrequencyRange[(int)deviceCOM.txAntenna.Item1].OrderByDescending(x => x.Maximum).First().Maximum,
                          deviceCOM.deviceRxFrequencyRange[(int)deviceCOM.rxAntenna.Item1].OrderByDescending(x => x.Maximum).First().Maximum) >= freqStop))
                {
                    _logger.Error("$ Start or End Frequency is not valid");
                }
                else
                {
                    parent.Configuration.config[Configuration.saVar.freqStart] = freqStart;
                    parent.Configuration.config[Configuration.saVar.freqStop] = freqStop;
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
            Theme.Text("Valid Impedance min forward", Theme.inputTheme);
            if (Theme.slider("valid Impadance Range", ref s_validRangeTolerance, Theme.sliderTheme))
            {
                parent.Configuration.config[Configuration.saVar.validImpedanceTol] = s_validRangeTolerance;
            }
        }
        Theme.Text("LO/PLL Leakage sleep (0-1000ms)", Theme.inputTheme);
        if (Theme.glowingInput("Leakage", ref s_osciliatorLeakageSleep, Theme.inputTheme))
        {
            int LO;
            if (int.TryParse(s_osciliatorLeakageSleep, out LO))
                if (LO >= 0 && LO <= 1000)
                    parent.Configuration.config[Configuration.saVar.leakageSleep] = LO;
            _logger.Debug(parent.Configuration.config[Configuration.saVar.leakageSleep]);
        }

        if (ImGui.Checkbox("IQ correction", ref s_isCorrectIQEnabled))
            parent.Configuration.config[Configuration.saVar.iqCorrection] = s_isCorrectIQEnabled;
        if (parent.rlManager.isFFTQueueEmpty() && !parent.rlManager.isRunning)
        {
            Theme.buttonTheme.text = "Sweep Reference (open port)";
            if (Theme.button("Sweep", Theme.buttonTheme))
            {
                for (var i = 0; i < parent.tab_Trace.s_traces.Length; i++)
                {
                    parent.tab_Trace.s_traces[i].viewStatus = tab_Trace.traceViewStatus.clear;
                    parent.tab_Trace.s_traces[i].plot.Clear();
                }

                parent.tab_Trace.s_traces[0].viewStatus = tab_Trace.traceViewStatus.active;
                parent.rlManager.beginRL();
            }
            Theme.newLine();
            Theme.buttonTheme.text = "Sweep range (closed port, optional)";
            if (Theme.button("Sweep", Theme.buttonTheme))
            {
                for (var i = 0; i < parent.tab_Trace.s_traces.Length; i++)
                {
                    parent.tab_Trace.s_traces[i].viewStatus = tab_Trace.traceViewStatus.clear;
                    parent.tab_Trace.s_traces[i].plot.Clear();
                }

                parent.tab_Trace.s_traces[2].viewStatus = tab_Trace.traceViewStatus.active;
                parent.rlManager.beginRL();
            }
            Theme.newLine();
            Theme.buttonTheme.text = "Sweep Results";
            if (Theme.button("Sweep", Theme.buttonTheme))
            {
                for (var i = 0; i < parent.tab_Trace.s_traces.Length; i++)
                    parent.tab_Trace.s_traces[i].viewStatus = tab_Trace.traceViewStatus.clear;
                parent.tab_Trace.s_traces[1].plot.Clear();
                parent.tab_Trace.s_traces[1].viewStatus = tab_Trace.traceViewStatus.active;
                parent.rlManager.beginRL();
            }
        }
        else
        {
            Theme.Text("Performing sweep...");
            Theme.Text("(to abort click End)");
        }
        Theme.newLine();
        ImGui.Checkbox("Enable continuous sweep", ref parent.rlManager.continous);
    }
}