using SoapySA.Model;

namespace SoapySA.View.tabs;

public partial class MeasurementsView
{
    private static readonly string[] AvailableMeasurements =
        { "None", "Channel Power", "Filter Bandwidth", "Adjacent Channel Power" };

    private readonly MainWindowView _parent = initiator;

    public MeasurementMode SSelectedMeasurementMode = MeasurementMode.None;
    public int SSelectedPage = 0;
}