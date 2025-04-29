using NLog;

namespace SoapyRL.View.tabs;

public static class tab_Marker
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public static marker s_Marker;

    public struct marker
    {
        public marker()
        {
        }

        public int id, reference = 1;
        public string txtStatus;
        public bool isActive;
        public double position, value;
        public double valueRef;
    }
}