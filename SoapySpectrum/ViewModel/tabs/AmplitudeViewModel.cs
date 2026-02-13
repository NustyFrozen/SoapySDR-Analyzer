using NLog;

namespace SoapySA.View.tabs;

public partial class AmplitudeView
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public override string tabName => "\ue473 Amplitude";
    private readonly MainWindowView _parent = initiator;
    public bool SAutomaticLevelingEnabled;
    public string SDisplayOffset = "0";
    public string SDisplayRefLevel = "-40";
    public string SDisplayStartDb = "-138";
    public string SDisplayStopDb = "0";
    public int SScalePerDivision = 20;
}