using ImGuiNET;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapySA.View.measurements;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using System.Drawing;

namespace SoapySA.View.tabs;

public partial class MeasurementsView(PerformFft fftManager,GraphPlotManager _graphData, Configuration Config,SdrDeviceCom COM, List<MeasurementFeature> measurementsModes) : TabViewModel
{
    public override void Render()
    {
        if (pageState == 1) //Showing Settings
        {
            Theme.ButtonTheme.Text = "Return to Measure options";

            Theme.NewLine();
            //clicked return or
            //does not have special settings for this mode returning page state to 0
            Theme.ButtonTheme.Text = $"Return to Measurement options";
            bool returnClicked = Theme.Button("Return to Measurement options", Theme.ButtonTheme);
            Theme.NewLine();
            if ( returnClicked || !SSelectedMeasurementMode.renderSettings()) pageState = 0;
        }
        else
        {
            ImGui.SetCursorPosY(UserScreenConfiguration.OptionSize.Y / 2.0f -
                                (measurementsModes.Count - 1) * Theme.ButtonTheme.Size.Y / 2.0f);
            for (var i = 0; i < measurementsModes.Count; i++)
            {
                Theme.ButtonTheme.Text = $"{measurementsModes[i].Name}";
                if (Theme.Button(measurementsModes[i].Name, Theme.ButtonTheme))
                {
                    SSelectedMeasurementMode = measurementsModes[i];
                    if (SSelectedMeasurementMode is ChannelPowerView)
                    {
                        var start = (double)Config.FreqStart;
                        var stop = (double)Config.FreqStop;
                        var center = (stop - start) / 2.0 + start;
                        start = center - COM.RxSampleRate / 2.0;
                        stop = center + COM.RxSampleRate / 2.0;
                        Config.FreqStart = start;
                        Config.FreqStop = stop;
                        fftManager.ResetIqFilter();
                    }
                    pageState = 1; //showing settings Data
                }
                Theme.NewLine();
            }
        }

        Theme.NewLine();
    }
    public void DrawToolTip()
    {
        var draw = ImGui.GetForegroundDrawList();
        var start = ImGui.GetWindowPos();
        start.X = UserScreenConfiguration.PositionOffset.X;
        var end = start;
        end.X += UserScreenConfiguration.GraphSize.X;
        draw.AddRectFilled(start, end, Color.FromArgb(12, 12, 12).ToUint());
        ImGui.Text("Selected Marker");
        var currentMarker = _graphData.Markers[_graphData.SSelectedMarker];
        foreach (var marker in _graphData.Markers)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{marker.Id + 1}", currentMarker.Id == marker.Id))
            {
                if (currentMarker.Id == marker.Id)
                    _graphData.Markers[marker.Id].IsActive = !_graphData.Markers[marker.Id].IsActive;
                else
                    _graphData.Markers[marker.Id].IsActive = true;
                _graphData.SSelectedMarker = marker.Id;
            }
        }

        ImGui.SameLine();
        if (Theme.DrawTextButton("Save User Preset"))
            Config.SaveConfig();
        ImGui.SameLine();
        if (Theme.DrawTextButton("Load User Preset"))
            Config.LoadConfig();
    }
    public void drawGraph()
    {
        //mode does not have special graph drawing, draw default graph
        if (!SSelectedMeasurementMode.renderGraph())
            measurementsModes[0].renderGraph(); // draw default graph      
    }
}