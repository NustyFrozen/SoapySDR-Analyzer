using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class TraceView(GraphPlotManager _GraphData) : TabViewModel
{
    public override void Render()
    {
        Theme.InputTheme.Prefix = "RBW";
        Theme.GlowingCombo("InputSelectortext3", ref _GraphData.SSelectedTrace, SComboTraces, Theme.InputTheme);
        Theme.NewLine();
        Theme.Text($"{FontAwesome5.Eye} View", Theme.InputTheme);
        if (ImGui.RadioButton($"{FontAwesome5.PersonRunning} Active",
                _GraphData.STraces[_GraphData.SSelectedTrace].ViewStatus == TraceViewStatus.Active))
            _GraphData.STraces[_GraphData.SSelectedTrace].ViewStatus = TraceViewStatus.Active;
        if (ImGui.RadioButton($"{FontAwesome5.Eye} View", _GraphData.STraces[_GraphData.SSelectedTrace].ViewStatus == TraceViewStatus.View))
            _GraphData.STraces[_GraphData.SSelectedTrace].ViewStatus = TraceViewStatus.View;
        if (ImGui.RadioButton($"{FontAwesome5.Eraser} Clear",
                _GraphData.STraces[_GraphData.SSelectedTrace].ViewStatus == TraceViewStatus.Clear))
            _GraphData.STraces[_GraphData.SSelectedTrace].ViewStatus = TraceViewStatus.Clear;

        Theme.NewLine();
        Theme.NewLine();
        Theme.Text($"{FontAwesome5.StreetView} Trace Function", Theme.InputTheme);
        if (ImGui.RadioButton($"{FontAwesome5.Equal} Normal",
                _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus == TraceDataStatus.Normal))
            _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus = TraceDataStatus.Normal;
        if (ImGui.RadioButton("\ue4c2 Max Hold", _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus == TraceDataStatus.MaxHold))
            _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus = TraceDataStatus.MaxHold;
        if (ImGui.RadioButton("\ue4b8 Min Hold", _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus == TraceDataStatus.MinHold))
            _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus = TraceDataStatus.MinHold;
        if (ImGui.RadioButton($"{FontAwesome5.Microscope} Average",
                _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus == TraceDataStatus.Average))
            _GraphData.STraces[_GraphData.SSelectedTrace].DataStatus = TraceDataStatus.Average;
    }
}