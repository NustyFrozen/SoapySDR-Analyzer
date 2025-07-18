using NLog;
using SoapySA.View.measurements;
using SoapySA.View.tabs;
using SoapyVNACommon.Fonts;

namespace SoapySA.View;

public partial class MainWindowView
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly string[] _availableTabs = new[]
    {
        "\uf2db Device", "\ue473 Amplitude", "\uf1fe BW", $"{FontAwesome5.WaveSquare} Frequency",
        $"{FontAwesome5.Marker} Markers", "\uf3c5 Trace", "\uf085 Calibration", $"{FontAwesome5.Calculator} Measurement"
    };

    public AmplitudeView AmplitudeView;
    public ChannelPowerView ChannelPowerView;
    public Configuration Configuration;
    public DeviceView DeviceView;
    public PerformFft FftManager;
    public FilterBandwithView FilterBandwithView;
    public FrequencyView FrequencyView;
    public GraphView GraphView;
    public MarkerView MarkerView;
    public NormalMeasurementView NormalMeasurementView;
    private int _tabId;
    public MeasurementsView TabMeasurementView;
    public TraceView TraceView;
    public VideoView VideoView;

    public void ReleaseSdr()
    {
        FftManager.StopFft();
    }

    public void HandleSdr()
    {
        FftManager.BeginFft();
    }
}