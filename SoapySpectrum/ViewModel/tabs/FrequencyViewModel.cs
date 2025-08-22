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

    public void ChangeFrequencyBySpan(double center,double span)
    => ChangeFrequencyByRange(center - span / 2,center + span / 2);
    
    public void ChangeFrequencyByRange(double freqStart, double freqStop)
    {
            if (freqStart >= freqStop || !_parent.DeviceView.DeviceCom
                    .DeviceRxFrequencyRange[(int)_parent.DeviceView.DeviceCom.RxAntenna.Item1]
                    .ToList().Exists(x => x.Minimum <= freqStart && x.Maximum >= freqStop))
            {
                _logger.Error($"Start or End Frequency is not valid {freqStart}-{freqStop}");
            }
            else
            {
                SDisplaySpan = (freqStop - freqStart).ToString();
                SDisplayFreqCenter = ((freqStop - freqStart) / 2.0 + freqStart).ToString();
                _parent.Configuration.Config[Configuration.SaVar.FreqStart] = freqStart;
                _parent.Configuration.Config[Configuration.SaVar.FreqStop] = freqStop;
            }
            _parent.FftManager.ResetIqFilter();
    }
}