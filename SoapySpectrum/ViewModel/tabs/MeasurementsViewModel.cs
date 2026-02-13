using SoapySA.Model;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class MeasurementsView
{
    public override string tabName => $"{FontAwesome5.Calculator} Measurement";
    private static readonly string[] AvailableMeasurements =
        { "None", "Channel Power", "Filter Bandwidth", "Adjacent Channel Power","Noise Figure (Y method)","Power Source" };

    private readonly MainWindowView _parent = initiator;

    public MeasurementMode SSelectedMeasurementMode = MeasurementMode.None;
    public int SSelectedPage = 0;
}