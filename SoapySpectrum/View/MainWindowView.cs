using System.Drawing;
using System.Numerics;
using ImGuiNET;
using SoapySA.Extentions;
using SoapySA.View.measurements;
using SoapySA.View.tabs;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapySA.View;

public partial class MainWindowView : IWidget
{
    public MainWindowView(string widgetName, Vector2 position, Vector2 windowSize, SdrDeviceCom deviceCom)
    {
        Configuration = new Configuration(widgetName, this);
        FftManager = new PerformFft(this);
       
        tabsService = FeaturesServiceFactory.createMainFeatures(this, deviceCom);
    }

    public void RenderWidget()
    {
        Render();
    }

    public void InitWidget()
    {
        Configuration.InitConfiguration();
        Theme.InitDefaultTheme();

        NormalMeasurementView.SWaitForMouseClick.Start();
        GraphView.InitializeGraphElements();
        ImGui.SetNextWindowPos(Configuration.MainWindowPos);
        ImGui.SetNextWindowSize(Configuration.SWidgetSize);
        Configuration.Config.CollectionChanged += NormalMeasurementView.UpdateCanvasData;
        Configuration.Config.CollectionChanged += ChannelPowerView.UpdateCanvasData;
        Configuration.Config.CollectionChanged += FilterBandwithView.UpdateCanvasData;
        Theme.SetScaleSize(Configuration.ScaleSize);
        NormalMeasurementView.UpdateCanvasData(null, null);
        ChannelPowerView.UpdateCanvasData(null, null);
        FilterBandwithView.UpdateCanvasData(null, null);
        ImGui.GetIO().FontGlobalScale = 1.4f;
    }

    public static void DrawCursor()
    {
        ImGui.SetMouseCursor(ImGuiMouseCursor.None);
        var cursorpos = ImGui.GetMousePos();
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X - 5, cursorpos.Y),
            new Vector2(cursorpos.X + 5, cursorpos.Y), Color.White.ToUint());
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X, cursorpos.Y - 5),
            new Vector2(cursorpos.X, cursorpos.Y + 5), Color.White.ToUint());
    }

    public void DrawToolTip()
    {
        var draw = ImGui.GetForegroundDrawList();
        var start = ImGui.GetWindowPos();
        start.X = Configuration.PositionOffset.X;
        var end = start;
        end.X += Configuration.GraphSize.X;
        draw.AddRectFilled(start, end, Color.FromArgb(12, 12, 12).ToUint());
        ImGui.Text("Selected Marker");
        var currentMarker = MarkerView.SMarkers[MarkerView.SSelectedMarker];
        foreach (var marker in MarkerView.SMarkers)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{marker.Id + 1}", currentMarker.Id == marker.Id))
            {
                if (currentMarker.Id == marker.Id)
                    MarkerView.SMarkers[marker.Id].IsActive = !MarkerView.SMarkers[marker.Id].IsActive;
                else
                    MarkerView.SMarkers[marker.Id].IsActive = true;
                MarkerView.SSelectedMarker = marker.Id;
            }
        }

        ImGui.SameLine();
        if (Theme.DrawTextButton("Save User Preset"))
            Configuration.SaveConfig();
        ImGui.SameLine();
        if (Theme.DrawTextButton("Load User Preset"))
            Configuration.LoadConfig();
    }

    private void RenderTabSelector()
    {
        ImGui.SetCursorPosY(Configuration.OptionSize.Y / 2.0f -
                            (tabsService.Count- 1) * Theme.ButtonTheme.Size.Y / 2.0f);

        for (var i = 0; i < tabsService.Count; i++)
        {
            Theme.ButtonTheme.Text = $"{tabsService[i].}";
            if (Theme.Button(tabsService[i].tabName, Theme.ButtonTheme))
                _ActiveTab = tabsService[i];
            Theme.NewLine();
        }
    }

    public void Render()
    {
        DrawToolTip();
        ImGui.BeginChild("Spectrum Graph", Configuration.GraphSize);

        GraphView.DrawGraph();
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(Configuration.GraphSize.X + 60 * Configuration.ScaleSize.X,
            Configuration.PositionOffset.Y + 30 * Configuration.ScaleSize.Y));
        ImGui.BeginChild("Spectrum Options", Configuration.OptionSize);
        Theme.InputTheme.Prefix = "RBW";
        if (_ActiveTab is { } tab)
        {
            tab.Render();
            //Theme.ButtonTheme.Text = $"{_availableTabs[_tabId]}";
            //if (Theme.Button(_availableTabs[_tabId], Theme.ButtonTheme))
            //{
            //    _tabId = -1;
            //    TabMeasurementView.SSelectedPage = 0;
            //}
        } else RenderTabSelector();

        Theme.NewLine();
     //render tabs here

        ImGui.EndChild();
        DrawCursor();
    }
}