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
        Theme.SetScaleSize(UserScreenConfiguration.ScaleSize);
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

        ImGui.SetCursorPos(new Vector2(UserScreenConfiguration.GraphSize.X + 60 * UserScreenConfiguration.ScaleSize.X,
            UserScreenConfiguration.PositionOffset.Y + 30 * UserScreenConfiguration.ScaleSize.Y));
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