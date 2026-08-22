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
    private MeasurementsView GraphManager;

    public event EventHandler? OnWidgetExit;
    public event EventHandler? OnWidgetEnter;

    public MainWindowView(string widgetName, Vector2 position, Vector2 windowSize, SdrDeviceCom deviceCom)
    {
        tabsService = FeaturesServiceFactory.createMainFeatures(widgetName,this, deviceCom);
        GraphManager = tabsService.First(x=>x.GetType() == typeof(MeasurementsView)) as MeasurementsView;
    }
    
    public void RenderWidget()
    {
        Render();
    }
    
    public void InitWidget()
    {
        
        Theme.InitDefaultTheme();

        NormalMeasurementView.SWaitForMouseClick.Restart();
        Theme.Refresh();
    }

    /// <summary>Crosshair cursor, half length 5px @1080p -> 0.46% of the screen's shortest side.</summary>
    private const float CursorHalfLengthPct = 5.0f / UserScreenConfiguration.DesignHeight;

    public static void DrawCursor()
    {
        ImGui.SetMouseCursor(ImGuiMouseCursor.None);
        var cursorpos = ImGui.GetMousePos();
        var half = UserScreenConfiguration.PercentUniform(CursorHalfLengthPct);
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X - half, cursorpos.Y),
            new Vector2(cursorpos.X + half, cursorpos.Y), Color.White.ToUint());
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X, cursorpos.Y - half),
            new Vector2(cursorpos.X, cursorpos.Y + half), Color.White.ToUint());
    }

   

    private void RenderTabSelector()
    {
        ImGui.SetCursorPosY(UserScreenConfiguration.OptionSize.Y / 2.0f -
                            (tabsService.Count- 1) * Theme.ButtonTheme.Size.Y / 2.0f);

        for (var i = 0; i < tabsService.Count; i++)
        {
            Theme.ButtonTheme.Text = $"{tabsService[i].tabName}";
            if (Theme.Button(tabsService[i].tabName, Theme.ButtonTheme))
                _ActiveTab = tabsService[i];
            Theme.NewLine();
        }
    }

    public void Render()
    {
        GraphManager.DrawToolTip();

        ImGui.BeginChild("Spectrum Graph", UserScreenConfiguration.GraphSize);

        GraphManager.drawGraph();
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(
            UserScreenConfiguration.GraphSize.X + UserScreenConfiguration.PercentX(UserScreenConfiguration.OptionGapXPct),
            UserScreenConfiguration.PositionOffset.Y + UserScreenConfiguration.PercentY(UserScreenConfiguration.OptionGapYPct)));
        ImGui.BeginChild("Spectrum Options", UserScreenConfiguration.OptionSize);
        Theme.InputTheme.Prefix = "RBW";
        if (_ActiveTab is { } tab)
        {
            Theme.ButtonTheme.Text = $"Return";
            if (Theme.Button("Return Button", Theme.ButtonTheme))
                _ActiveTab = null;
            else
            {
                Theme.NewLine();
                Theme.NewLine();
                tab.Render();
            }
        } else RenderTabSelector();
        ImGui.EndChild();
        DrawCursor();
    }

    public void WidgetEnter() => OnWidgetEnter?.Invoke(this, EventArgs.Empty);

    public void WidgetExit() => OnWidgetExit?.Invoke(this, EventArgs.Empty);
}