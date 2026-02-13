using NLog;

namespace SoapySA.View.tabs;

public partial class VideoView
{
    public override string tabName => "\uf1fe BW";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public string DisplayRefreshRate = "1000";
    public string FftRbw = "0.01M";
    private readonly MainWindowView _parent = initiator;
    public string SFftOverlap = "50%";
    public string SFftSegments = "1600";
    public string SFftWindowAdditionalArgument = "0.5";

    public static double[] NoWindowFunction(int length)
    {
        var result = new double[length];
        for (var i = 0; i < length; i++)
            result[i] = 1;
        return result;
    }
}