using NLog;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using System.ComponentModel;

namespace SoapySA.View.tabs;

public partial class FrequencyView
{
    public override string tabName => $"{FontAwesome5.WaveSquare} Frequency";

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly SdrDeviceCom _deviceCom;
    private readonly Configuration _config;
    private readonly PerformFft _fftService;
    public string SDisplayFreqCenter = "945M";
    public string SDisplayFreqStart = "930M";
    public string SDisplayFreqStop = "960M";
    public string SDisplaySpan = "30M";

    public FrequencyView(SdrDeviceCom deviceCom, Configuration config,PerformFft fftService)
    {
        this._deviceCom = deviceCom;
        _config = config;
        this._fftService = fftService;
        HookConfig();
    }

    public void ChangeFrequencyBySpan(double center, double span)
        => ChangeFrequencyByRange(center - span / 2, center + span / 2);

    public void ChangeFrequencyByRange(double freqStart, double freqStop)
    {
        if (freqStart >= freqStop || !_deviceCom
                .DeviceRxFrequencyRange[(int)_deviceCom.RxAntenna.Item1]
                .ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
        {
            _logger.Error($"Start or End Frequency is not valid {freqStart}-{freqStop}");
        }
        else
        {
            // Update local display strings
            SDisplaySpan = (freqStop - freqStart).ToString();
            SDisplayFreqCenter = ((freqStop - freqStart) / 2.0 + freqStart).ToString();
            SDisplayFreqStart = freqStart.ToString();
            SDisplayFreqStop = freqStop.ToString();

            // Update config (strongly typed)
            _config.FreqStart = freqStart;
            _config.FreqStop = freqStop;
        }

        _fftService.ResetIqFilter();
    }

    private void HookConfig()
    {
        SyncFromConfig();

        _config.PropertyChanged -= OnConfigPropertyChanged;
        _config.PropertyChanged += OnConfigPropertyChanged;
        _config.OnConfigLoadBegin += (object? s, EventArgs e) => {
            SDisplayFreqCenter = _config.FreqCenter.ToString();
            SDisplaySpan = _config.Span.ToString();
            SDisplayFreqStart = _config.FreqStart.ToString();
            SDisplayFreqStop = _config.FreqStop.ToString();
        };
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Configuration.FreqStart)
            or nameof(Configuration.FreqStop)
            or nameof(Configuration.Span)
            or nameof(Configuration.FreqCenter))
        {
            SyncFromConfig();
        }
    }

    private void SyncFromConfig()
    {
        // Keep UI fields aligned with config
        var start = _config.FreqStart;
        var stop = _config.FreqStop;

        SDisplaySpan = (stop - start).ToString();
        SDisplayFreqCenter = ((stop - start) / 2.0 + start).ToString();
        SDisplayFreqStart = start.ToString();
        SDisplayFreqStop = stop.ToString();
    }
}