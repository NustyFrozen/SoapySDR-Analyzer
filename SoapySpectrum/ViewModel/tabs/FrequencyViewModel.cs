using NLog;

namespace SoapySA.View.tabs;

public partial class FrequencyView
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly MainWindowView _parent = initiator;
    public string SDisplayFreqCenter = "945M";
    public string SDisplayFreqStart = "930M";
    public string SDisplayFreqStop = "960M";
    public string SDisplaySpan = "30M";
}