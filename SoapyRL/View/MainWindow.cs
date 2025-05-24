using ImGuiNET;
using NLog;
using SoapyRL.View.tabs;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using System.Numerics;

namespace SoapyRL.View;

public class MainWindow : Widget
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public tab_Device tab_Device;
    public tab_Marker tab_Marker;
    public tab_Trace tab_Trace;
    public Graph Graph;
    public Configuration Configuration;
    public PerformRL rlManager;

    public MainWindow(string widgetName, Vector2 position, Vector2 windowSize, sdrDeviceCOM deviceCom)
    {
        Configuration = new Configuration(widgetName, this, windowSize, position);
        Graph = new Graph(this);
        tab_Device = new tab_Device(this, deviceCom);
        rlManager = new PerformRL(this);
        tab_Marker = new tab_Marker(this);
        tab_Trace = new tab_Trace(this);
    }

    public void renderWidget() => Render();

    public void releaseSDR() => rlManager.stopRL();

    public void handleSDR()
    {/* user will enable RL on his own */ }

    public void initWidget()
    {
        Configuration.initDefaultConfig();
        Theme.initDefaultTheme();
        Graph.s_waitForMouseClick.Start();
        Graph.initializeGraphElements();
    }

    protected void Render()
    {
        ImGui.BeginChild("RL Graph", Configuration.graphSize);
        Graph.drawGraph();
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(Configuration.graphSize.X + 60 * Configuration.scaleSize.X, 10));
        ImGui.BeginChild("Options", Configuration.optionSize);
        Theme.newLine();
        tab_Device.renderDevice();

        ImGui.EndChild(); ;
    }
}