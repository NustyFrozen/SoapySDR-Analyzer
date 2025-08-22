using SoapySA.Model;

namespace SoapySA.View;

public partial class GraphView(MainWindowView initiator)
{
    public void DrawGraph()
    {
        switch (_parent.TabMeasurementView.SSelectedMeasurementMode)
        {
            case MeasurementMode.None or MeasurementMode.NoiseFigure:
                _parent.NormalMeasurementView.RenderNormal();
                break;

            case MeasurementMode.ChannelPower:
                _parent.ChannelPowerView.RenderChannelPower();
                break;

            case MeasurementMode.FilterBw:
                _parent.FilterBandwithView.RenderFilterBandwith();
                break;
        }
    }
}