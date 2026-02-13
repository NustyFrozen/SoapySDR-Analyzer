using NLog;
using SoapySA.Model;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.tabs;

public partial class DeviceView
{
    public override string tabName => "\uf2db Device";
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public SdrDeviceCom DeviceCom = com;
    public string[] GainValues = new string[com.RxGainValues.Count];
    private bool _initialized;
    private readonly MainWindowView _parent = initiator;
    public bool SIsCorrectIqEnabled = true;
    public bool SIsinterleavingEnabled;
    public string SOsciliatorLeakageSleep;
}