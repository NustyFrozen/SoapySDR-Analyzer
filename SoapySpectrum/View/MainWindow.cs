using ImGuiNET;
using NLog;
using SoapySA.View.measurements;
using SoapySA.View.tabs;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using System.Numerics;

namespace SoapySA.View;

public class MainWindow : SoapyVNACommon.Widget
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private int tabID;
    public tab_Device tab_Device;
    public tab_Amplitude tab_Amplitude;
    public tab_Frequency tab_Frequency;
    public tab_Marker tab_Marker;
    public tab_Measurement tab_Measurement;
    public tab_Video tab_Video;
    public tab_Trace tab_Trace;
    public Graph Graph;
    public Configuration Configuration;
    public View.measurements.NormalMeasurement normalMeasurement;
    public View.measurements.ChannelPower channelPower;
    public View.measurements.FilterBandwith FilterBandwith;
    public PerformFFT fftManager;

    public MainWindow(string widgetName, Vector2 position, Vector2 windowSize, sdrDeviceCOM deviceCom)
    {
        Configuration = new Configuration(widgetName, this, windowSize, position);
        Graph = new Graph(this);
        fftManager = new PerformFFT(this);

        tab_Device = new tab_Device(this, deviceCom);
        tab_Amplitude = new tab_Amplitude(this);
        tab_Frequency = new tab_Frequency(this);
        tab_Marker = new tab_Marker(this);
        tab_Measurement = new tab_Measurement(this);
        tab_Video = new tab_Video(this);
        tab_Trace = new tab_Trace(this);
        normalMeasurement = new NormalMeasurement(this);
        channelPower = new ChannelPower(this);
        FilterBandwith = new FilterBandwith(this);
    }

    private readonly string[] availableTabs = new[]
    {
        "\uf2db Device", "\ue473 Amplitude", "\uf1fe BW", $"{FontAwesome5.WaveSquare} Frequency",
        $"{FontAwesome5.Marker} Markers", "\uf3c5 Trace", "\uf085 Calibration", $"{ FontAwesome5.Calculator} Measurement"
    };

    public static void drawCursor()
    {
        ImGui.SetMouseCursor(ImGuiMouseCursor.None);
        var cursorpos = ImGui.GetMousePos();
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X - 5, cursorpos.Y),
            new Vector2(cursorpos.X + 5, cursorpos.Y), Color.White.ToUint());
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X, cursorpos.Y - 5),
            new Vector2(cursorpos.X, cursorpos.Y + 5), Color.White.ToUint());
    }

    public void drawToolTip()
    {
        var draw = ImGui.GetForegroundDrawList();
        var start = ImGui.GetWindowPos();
        start.X = Configuration.positionOffset.X;
        var end = start;
        end.X += Configuration.graphSize.X;
        draw.AddRectFilled(start, end, Color.FromArgb(12, 12, 12).ToUint());
        ImGui.Text("Selected Marker");
        var currentMarker = tab_Marker.s_markers[tab_Marker.s_selectedMarker];
        foreach (var marker in tab_Marker.s_markers)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{marker.id + 1}", currentMarker.id == marker.id))
            {
                if (currentMarker.id == marker.id)
                    tab_Marker.s_markers[marker.id].isActive = !tab_Marker.s_markers[marker.id].isActive;
                else
                    tab_Marker.s_markers[marker.id].isActive = true;
                tab_Marker.s_selectedMarker = marker.id;
            }
        }
        ImGui.SameLine();
        if (Theme.drawTextButton("Save User Preset"))
            Configuration.saveConfig();
        ImGui.SameLine();
        if (Theme.drawTextButton("Load User Preset"))
            Configuration.loadConfig();
    }

    private void renderTabSelector()
    {
        ImGui.SetCursorPosY(Configuration.optionSize.Y / 2.0f -
                            (availableTabs.Length - 1) * Theme.buttonTheme.size.Y / 2.0f);

        for (var i = 0; i < availableTabs.Length; i++)
        {
            Theme.buttonTheme.text = $"{availableTabs[i]}";
            if (Theme.button(availableTabs[i], Theme.buttonTheme))
                tabID = i;
            Theme.newLine();
        }
    }

    public void renderWidget() => Render();

    public void releaseSDR() => fftManager.stopFFT();

    public void handleSDR() => fftManager.beginFFT();

    public void initWidget()
    {
        Configuration.initConfiguration();
        Theme.initDefaultTheme();

        measurements.NormalMeasurement.s_waitForMouseClick.Start();
        tab_Marker.markerMoveKeys.Start();
        Graph.initializeGraphElements();
        ImGui.SetNextWindowPos(Configuration.mainWindowPos);
        ImGui.SetNextWindowSize(Configuration.s_widgetSize);
        Configuration.config.CollectionChanged += normalMeasurement.updateCanvasData;
        Configuration.config.CollectionChanged += channelPower.updateCanvasData;
        Configuration.config.CollectionChanged += FilterBandwith.updateCanvasData;
        Theme.setScaleSize(Configuration.scaleSize);
        normalMeasurement.updateCanvasData(null, null);
        channelPower.updateCanvasData(null, null);
        FilterBandwith.updateCanvasData(null, null);
        ImGui.GetIO().FontGlobalScale = 1.4f;
    }

    public void Render()
    {
        drawToolTip();
        ImGui.BeginChild("Spectrum Graph", Configuration.graphSize);

        Graph.drawGraph();
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(Configuration.graphSize.X + 60 * Configuration.scaleSize.X,
            Configuration.positionOffset.Y + 30 * Configuration.scaleSize.Y));
        ImGui.BeginChild("Spectrum Options", Configuration.optionSize);
        Theme.inputTheme.prefix = "RBW";
        if (tabID != -1)
        {
            Theme.buttonTheme.text = $"{availableTabs[tabID]}";
            if (Theme.button(availableTabs[tabID], Theme.buttonTheme))
            {
                tabID = -1;
                tab_Measurement.s_selectedPage = 0;
            }
        }

        Theme.newLine();
        switch (tabID)
        {
            case -1:
                renderTabSelector();
                break;

            case 0:
                tab_Device.renderDevice();
                break;

            case 1:
                tab_Amplitude.renderAmplitude();
                break;

            case 2:
                tab_Video.renderVideo();
                break;

            case 3:
                tab_Frequency.renderFrequency();
                break;

            case 4:
                tab_Marker.renderMarker();
                break;

            case 5:
                tab_Trace.renderTrace();
                break;

            case 6:
                //tab_Cal.renderCalibration();
                break;

            case 7:
                tab_Measurement.renderMeasurements();
                break;
        }

        ImGui.EndChild();
        drawCursor();
    }
}