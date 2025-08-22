using NLog;

namespace SoapyRL.View.tabs;

public class TabMarker(MainWindow initiator)
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public MainWindow Parent = initiator;
    public Marker SMarker;

    public struct Marker
    {
        public Marker()
        {
        }

        public int Id, Reference = 1;
        public string TxtStatus;
        public bool IsActive;
        public double Position, Value;
        public double ValueRef;
    }
}