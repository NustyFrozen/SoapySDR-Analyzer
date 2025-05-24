using NLog;

namespace SoapyRL.View.tabs;

public class tab_Marker(MainWindow initiator)
{
    public MainWindow parent = initiator;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public marker s_Marker;

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