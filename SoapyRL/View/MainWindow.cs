using System.Numerics;
using ImGuiNET;
using NLog;
using SoapyRL.View.tabs;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapyRL.View;

public class MainWindow : IWidget
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public Configuration Configuration;
    public Graph Graph;
    public PerformRl RlManager;
    public TabDevice TabDevice;
    public TabMarker TabMarker;
    public TabTrace TabTrace;

    public event EventHandler? OnWidgetExit;
    public event EventHandler? OnWidgetEnter;

    public MainWindow(string widgetName, Vector2 position, Vector2 windowSize, SdrDeviceCom deviceCom)
    {
        Configuration = new Configuration(widgetName, this, windowSize, position);
        Graph = new Graph(this);
        TabDevice = new TabDevice(this, deviceCom);
        RlManager = new PerformRl(this);
        TabMarker = new TabMarker(this);
        TabTrace = new TabTrace(this);
    }

    public void RenderWidget()
    {
        Render();
    }

    public void InitWidget()
    {
        Configuration.InitDefaultConfig();
        Theme.InitDefaultTheme();
        Graph.SWaitForMouseClick.Start();
        Graph.InitializeGraphElements();
    }

    protected void Render()
    {
        ImGui.BeginChild("RL Graph", Configuration.GraphSize);
        Graph.DrawGraph();
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(Configuration.GraphSize.X + 60 * Configuration.ScaleSize.X, 10));
        ImGui.BeginChild("Options", Configuration.OptionSize);
        Theme.NewLine();
        TabDevice.RenderDevice();

        ImGui.EndChild();
        ;
    }

    public void WidgetEnter() => OnWidgetEnter?.Invoke(this, EventArgs.Empty);

    public void WidgetExit() => OnWidgetExit?.Invoke(this, EventArgs.Empty);
}