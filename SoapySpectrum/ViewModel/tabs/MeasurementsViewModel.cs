using SoapySA.Model;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class MeasurementsView
{
    //pageState = 0 -> select Measurement
    //pagestate = 1 -> Measurement settings + go back button
    private int pageState = 0;
    public override string tabName => $"{FontAwesome5.Calculator} Measurements";
    public MeasurementFeature SSelectedMeasurementMode = measurementsModes.First();
}