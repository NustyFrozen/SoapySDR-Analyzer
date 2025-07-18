using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapyRL.View.tabs;

public class TabDevice(MainWindow initiator, SdrDeviceCom com)
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public SdrDeviceCom DeviceCom = com;

    public string[] GainRxValues = new string[com.RxGainValues.Count],
        GainTxValues = new string[com.TxGainValues.Count];

    private bool _initialized;
    public bool IsCorrectIqEnabled = true, IsShowValidRangeEnabled;
    private readonly MainWindow _parent = initiator;

    public string SDisplayFreqStart = "800M", SDisplayFreqStop = "1000M";
    public bool SIsCorrectIqEnabled = true;
    public string SOsciliatorLeakageSleep = "0";
    public bool SShowValidRange;
    public float SValidRangeTolerance = 90.0f;

    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public void RenderDeviceData()
    {
        if (!_initialized)
        {
            for (var i = 0; i < GainRxValues.Length; i++) GainRxValues[i] = com.RxGainValues[i].ToString();
            for (var i = 0; i < GainTxValues.Length; i++) GainTxValues[i] = com.TxGainValues[i].ToString();
            _initialized = true;
        }

        ImGui.Text($"{FontAwesome5.Microchip} {DeviceCom.Descriptor}\n" +
                   $"Reflection:\nCH {DeviceCom.RxAntenna.Item1}\n" +
                   $"ANT {DeviceCom.RxAntenna.Item2}\n" +
                   $"Foward:\nCH {DeviceCom.TxAntenna.Item1}\n" +
                   $"ANT {DeviceCom.TxAntenna.Item2}");
        Theme.Text("TX Amplifiers", Theme.InputTheme);
        foreach (var gainElm in DeviceCom.TxGains)
            if (gainElm.Key.Item1 == DeviceCom.TxAntenna.Item1)
            {
                var gain = GainTxValues[gainElm.Value.Item2];
                var range = gainElm.Value.Item1;
                ImGui.Text($"{gainElm.Key.Item2} {range.Minimum} - {range.Maximum}");
                if (Theme.GlowingInput($"{gainElm.Key.Item2}_tx", ref GainTxValues[gainElm.Value.Item2],
                        Theme.InputTheme))
                {
                    double results = 0;
                    var valid = double.TryParse(GainTxValues[gainElm.Value.Item2], out results);
                    valid |= results >= range.Minimum && results <= range.Maximum;
                    if (!valid)
                    {
                        _logger.Error("invalid Double Value or value out ouf range");
                    }
                    else
                    {
                        if (range.Step != 0)
                            DeviceCom.SdrDevice.SetGain(Direction.Tx, DeviceCom.TxAntenna.Item1, gainElm.Key.Item2,
                                Math.Round(results / range.Step) * range.Step);
                        else
                            //free value
                            DeviceCom.SdrDevice.SetGain(Direction.Tx, DeviceCom.TxAntenna.Item1, gainElm.Key.Item2,
                                results);
                    }
                }
            }

        Theme.Text("RX Amplifiers", Theme.InputTheme);
        foreach (var gainElm in DeviceCom.RxGains)
            if (gainElm.Key.Item1 == DeviceCom.RxAntenna.Item1)
            {
                var gain = GainRxValues[gainElm.Value.Item2];
                var range = gainElm.Value.Item1;
                ImGui.Text($"{gainElm.Key.Item2} {range.Minimum} - {range.Maximum}");
                if (Theme.GlowingInput($"{gainElm.Key.Item2}_rx", ref GainRxValues[gainElm.Value.Item2],
                        Theme.InputTheme))
                {
                    double results = 0;
                    var valid = double.TryParse(GainRxValues[gainElm.Value.Item2], out results);
                    valid |= results >= range.Minimum && results <= range.Maximum;
                    if (!valid)
                    {
                        _logger.Error("invalid Double Value or value ot ouf range");
                    }
                    else
                    {
                        if (range.Step != 0)
                            DeviceCom.SdrDevice.SetGain(Direction.Rx, DeviceCom.RxAntenna.Item1, gainElm.Key.Item2,
                                Math.Round(results / range.Step) * range.Step);
                        else
                            //free value
                            DeviceCom.SdrDevice.SetGain(Direction.Rx, DeviceCom.RxAntenna.Item1, gainElm.Key.Item2,
                                results);
                    }
                }
            }

        Theme.Text($"Sensors Data\n{DeviceCom.SensorData}", Theme.InputTheme);

        Theme.ButtonTheme.Text = "Refresh Sensors Data";
        if (Theme.Button("Refresh_Sensors", Theme.ButtonTheme))
        {
            // var i = 0;
            // foreach (var sensor in s_sdrDevice.ListSensors())
            //     _deviceSensorData[i++] = $"{sensor}: {s_sdrDevice.ReadSensor(sensor)}";
        }
    }

    public void RenderDevice()
    {
        Theme.NewLine();
        RenderDeviceData();
        Theme.NewLine();
        Theme.Text($"{FontAwesome5.ArrowLeft} Left Band", Theme.InputTheme);
        Theme.InputTheme.Prefix = " start Frequency";
        var hasFrequencyChanged = Theme.GlowingInput("InputSelectortext", ref SDisplayFreqStart, Theme.InputTheme);
        Theme.Text($"{FontAwesome5.ArrowRight} Right Band", Theme.InputTheme);
        Theme.InputTheme.Prefix = "End Frequency";
        hasFrequencyChanged |= Theme.GlowingInput("InputSelectortext2", ref SDisplayFreqStop, Theme.InputTheme);

        if (hasFrequencyChanged) //apply frequency change in settings
        {
            double freqStart, freqStop;
            if (Global.TryFormatFreq(SDisplayFreqStart, out freqStart) &&
                Global.TryFormatFreq(SDisplayFreqStop, out freqStop))
            {
                if (freqStart >= freqStop ||
                    !(Math.Max(
                          DeviceCom.DeviceTxFrequencyRange[(int)DeviceCom.TxAntenna.Item1].OrderBy(x => x.Minimum)
                              .First().Minimum,
                          DeviceCom.DeviceRxFrequencyRange[(int)DeviceCom.RxAntenna.Item1].OrderBy(x => x.Minimum)
                              .First().Minimum) <= freqStart
                      && Math.Min(
                          DeviceCom.DeviceTxFrequencyRange[(int)DeviceCom.TxAntenna.Item1]
                              .OrderByDescending(x => x.Maximum).First().Maximum,
                          DeviceCom.DeviceRxFrequencyRange[(int)DeviceCom.RxAntenna.Item1]
                              .OrderByDescending(x => x.Maximum).First().Maximum) >= freqStop))
                {
                    _logger.Error("$ Start or End Frequency is not valid");
                }
                else
                {
                    _parent.Configuration.Config[Configuration.SaVar.FreqStart] = freqStart;
                    _parent.Configuration.Config[Configuration.SaVar.FreqStop] = freqStop;
                }
            }
            else
            {
                _logger.Error("$ Start or End Frequency span is not a valid double");
            }
        }

        ImGui.Checkbox("Show Valid Impedance Range", ref SShowValidRange);
        if (SShowValidRange)
        {
            Theme.Text("Valid Impedance min forward", Theme.InputTheme);
            if (Theme.Slider("valid Impadance Range", ref SValidRangeTolerance, Theme.SliderTheme))
                _parent.Configuration.Config[Configuration.SaVar.ValidImpedanceTol] = SValidRangeTolerance;
        }

        Theme.Text("LO/PLL Leakage sleep (0-1000ms)", Theme.InputTheme);
        if (Theme.GlowingInput("Leakage", ref SOsciliatorLeakageSleep, Theme.InputTheme))
        {
            int lo;
            if (int.TryParse(SOsciliatorLeakageSleep, out lo))
                if (lo >= 0 && lo <= 1000)
                    _parent.Configuration.Config[Configuration.SaVar.LeakageSleep] = lo;
            _logger.Debug(_parent.Configuration.Config[Configuration.SaVar.LeakageSleep]);
        }

        if (ImGui.Checkbox("IQ correction", ref SIsCorrectIqEnabled))
            _parent.Configuration.Config[Configuration.SaVar.IqCorrection] = SIsCorrectIqEnabled;
        if (_parent.RlManager.IsFftQueueEmpty() && !_parent.RlManager.IsRunning)
        {
            Theme.ButtonTheme.Text = "Sweep Reference (open port)";
            if (Theme.Button("Sweep", Theme.ButtonTheme))
            {
                for (var i = 0; i < _parent.TabTrace.STraces.Length; i++)
                {
                    _parent.TabTrace.STraces[i].ViewStatus = TabTrace.TraceViewStatus.Clear;
                    _parent.TabTrace.STraces[i].Plot.Clear();
                }

                _parent.TabTrace.STraces[0].ViewStatus = TabTrace.TraceViewStatus.Active;
                _parent.RlManager.BeginRl();
            }

            Theme.NewLine();
            Theme.ButtonTheme.Text = "Sweep range (closed port, optional)";
            if (Theme.Button("Sweep", Theme.ButtonTheme))
            {
                for (var i = 0; i < _parent.TabTrace.STraces.Length; i++)
                {
                    _parent.TabTrace.STraces[i].ViewStatus = TabTrace.TraceViewStatus.Clear;
                    _parent.TabTrace.STraces[i].Plot.Clear();
                }

                _parent.TabTrace.STraces[2].ViewStatus = TabTrace.TraceViewStatus.Active;
                _parent.RlManager.BeginRl();
            }

            Theme.NewLine();
            Theme.ButtonTheme.Text = "Sweep Results";
            if (Theme.Button("Sweep", Theme.ButtonTheme))
            {
                for (var i = 0; i < _parent.TabTrace.STraces.Length; i++)
                    _parent.TabTrace.STraces[i].ViewStatus = TabTrace.TraceViewStatus.Clear;
                _parent.TabTrace.STraces[1].Plot.Clear();
                _parent.TabTrace.STraces[1].ViewStatus = TabTrace.TraceViewStatus.Active;
                _parent.RlManager.BeginRl();
            }
        }
        else
        {
            Theme.Text("Performing sweep...");
            Theme.Text("(to abort click End)");
        }

        Theme.NewLine();
        ImGui.Checkbox("Enable continuous sweep", ref _parent.RlManager.Continous);
    }
}