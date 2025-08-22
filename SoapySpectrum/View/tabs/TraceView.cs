using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapySA.View.tabs;

public partial class TraceView(MainWindowView initiator)
{
    public void RenderTrace()
    {
        Theme.InputTheme.Prefix = "RBW";
        Theme.GlowingCombo("InputSelectortext3", ref SSelectedTrace, SComboTraces, Theme.InputTheme);
        Theme.NewLine();
        Theme.Text($"{FontAwesome5.Eye} View", Theme.InputTheme);
        if (ImGui.RadioButton($"{FontAwesome5.PersonRunning} Active",
                STraces[SSelectedTrace].ViewStatus == TraceViewStatus.Active))
            STraces[SSelectedTrace].ViewStatus = TraceViewStatus.Active;
        if (ImGui.RadioButton($"{FontAwesome5.Eye} View", STraces[SSelectedTrace].ViewStatus == TraceViewStatus.View))
            STraces[SSelectedTrace].ViewStatus = TraceViewStatus.View;
        if (ImGui.RadioButton($"{FontAwesome5.Eraser} Clear",
                STraces[SSelectedTrace].ViewStatus == TraceViewStatus.Clear))
            STraces[SSelectedTrace].ViewStatus = TraceViewStatus.Clear;

        Theme.NewLine();
        Theme.NewLine();
        Theme.Text($"{FontAwesome5.StreetView} Trace Function", Theme.InputTheme);
        if (ImGui.RadioButton($"{FontAwesome5.Equal} Normal",
                STraces[SSelectedTrace].DataStatus == TraceDataStatus.Normal))
            STraces[SSelectedTrace].DataStatus = TraceDataStatus.Normal;
        if (ImGui.RadioButton("\ue4c2 Max Hold", STraces[SSelectedTrace].DataStatus == TraceDataStatus.MaxHold))
            STraces[SSelectedTrace].DataStatus = TraceDataStatus.MaxHold;
        if (ImGui.RadioButton("\ue4b8 Min Hold", STraces[SSelectedTrace].DataStatus == TraceDataStatus.MinHold))
            STraces[SSelectedTrace].DataStatus = TraceDataStatus.MinHold;
        if (ImGui.RadioButton($"{FontAwesome5.Microscope} Average",
                STraces[SSelectedTrace].DataStatus == TraceDataStatus.Average))
            STraces[SSelectedTrace].DataStatus = TraceDataStatus.Average;
    }
}