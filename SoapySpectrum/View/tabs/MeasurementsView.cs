using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon;

namespace SoapySA.View.tabs;

public partial class MeasurementsView(MainWindowView initiator)
{
    public void RenderMeasurements()
    {
        if (SSelectedPage != 0)
        {
            Theme.ButtonTheme.Text = "Return to Measure options";
            if (Theme.Button("MeasurementReturn", Theme.ButtonTheme)) SSelectedPage = 0;
        }
        else
        {
            ImGui.SetCursorPosY(_parent.Configuration.OptionSize.Y / 2.0f -
                                (AvailableMeasurements.Length - 1) * Theme.ButtonTheme.Size.Y / 2.0f);
            for (var i = 0; i < AvailableMeasurements.Length; i++)
            {
                Theme.ButtonTheme.Text = $"{AvailableMeasurements[i]}";
                if (Theme.Button(AvailableMeasurements[i], Theme.ButtonTheme))
                {
                    SSelectedMeasurementMode = (MeasurementMode)i;
                    if (SSelectedMeasurementMode == MeasurementMode.ChannelPower)
                    {
                        var start = (double)_parent.Configuration.Config[Configuration.SaVar.FreqStart];
                        var stop = (double)_parent.Configuration.Config[Configuration.SaVar.FreqStop];
                        var center = (stop - start) / 2.0 + start;
                        start = center - _parent.DeviceView.DeviceCom.RxSampleRate / 2.0;
                        stop = center + _parent.DeviceView.DeviceCom.RxSampleRate / 2.0;
                        _parent.Configuration.Config[Configuration.SaVar.FreqStart] = start;
                        _parent.Configuration.Config[Configuration.SaVar.FreqStop] = stop;
                        _parent.FftManager.ResetIqFilter();
                    }

                    SSelectedPage = i;
                }

                Theme.NewLine();
            }
        }

        Theme.NewLine();
        switch (SSelectedPage)
        {
            case 1:
                _parent.ChannelPowerView.RenderChannelPowerSettings();
                break;
            case 4:
                _parent.NoiseFigureMeasurementView.RenderNoiseFigureSettings();
                break;
            case 5:
                _parent.SourceView.RenderSourceViewSettings();
                break;
        }
    }
}