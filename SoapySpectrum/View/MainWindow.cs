using ClickableTransparentOverlay;
using ImGuiNET;
using NLog;
using SoapySA.Extentions;
using SoapySA.View.measurements;
using SoapySA.View.tabs;
using SoapyVNACommon.Fonts;
using System.Numerics;

namespace SoapySA.View;

public class UI : Overlay
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private static int tabID;

    private static ushort[] iconRange = new ushort[] { 0xe005, 0xf8ff, 0 };

    private static ImFontPtr PoppinsFont, IconFont;

    private readonly string[] availableTabs = new[]
    {
        "\uf2db Device", "\ue473 Amplitude", "\uf1fe BW", $"{FontAwesome5.WaveSquare} Frequency",
        $"{FontAwesome5.Marker} Markers", "\uf3c5 Trace", "\uf085 Calibration", $"{ FontAwesome5.Calculator} Measurement"
    };

    public bool initializedResources;
    private bool visble = true;

    public UI() : base(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
    {
        VSync = true;
    }

    protected override Task PostInitialized()
    {
        VSync = false;
        return Task.CompletedTask;
    }

    public static uint ToUint(Color c)
    {
        var u = (uint)c.A << 24;
        u += (uint)c.B << 16;
        u += (uint)c.G << 8;
        u += c.R;
        return u;
    }

    public unsafe void loadResources()
    {
        _logger.Debug("Loading Application Resources");
        var io = ImGui.GetIO();

        ReplaceFont(config =>
        {
            var io = ImGui.GetIO();
            io.Fonts.AddFontFromFileTTF(@"Fonts\Poppins-Light.ttf", 16, config,
                io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
            config->MergeMode = 1;
            config->OversampleH = 1;
            config->OversampleV = 1;
            config->PixelSnapH = 1;

            var custom2 = new ushort[] { 0xe005, 0xf8ff, 0x00 };
            fixed (ushort* p = &custom2[0])
            {
                io.Fonts.AddFontFromFileTTF("Fonts\\fa-solid-900.ttf", 16, config, new IntPtr(p));
            }
        });
        _logger.Debug("Replaced font");

        PoppinsFont = io.Fonts.AddFontFromFileTTF(@"Fonts\Poppins-Light.ttf", 16);
        //IconFont = io.Fonts.AddFontFromFileTTF(@"Fonts\fa-solid-900.ttf", 16,, new ushort[] { 0xe005,
        //0xf8ff,0});
    }

    public static void drawCursor()
    {
        ImGui.SetMouseCursor(ImGuiMouseCursor.None);
        var cursorpos = ImGui.GetMousePos();
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X - 5, cursorpos.Y),
            new Vector2(cursorpos.X + 5, cursorpos.Y), Color.White.ToUint());
        ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X, cursorpos.Y - 5),
            new Vector2(cursorpos.X, cursorpos.Y + 5), Color.White.ToUint());
    }

    public static void drawToolTip()
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

    protected override void Render()
    {
        if (Imports.GetAsyncKeyState(Keys.Insert))
        {
            Thread.Sleep(200);
            visble = !visble;
        }

        if (!visble) return;
        if (!initializedResources)
        {
            Theme.initDefaultTheme();
            tab_Device.setupSoapyEnvironment();
            tab_Device.refreshDevices();
            measurements.NormalMeasurement.s_waitForMouseClick.Start();

            tab_Marker.markerMoveKeys.Start();
            Graph.initializeGraphElements();
            loadResources();
            ImGui.SetNextWindowPos(Configuration.mainWindowPos);
            ImGui.SetNextWindowSize(Configuration.mainWindowSize);
            Configuration.config.CollectionChanged += View.measurements.NormalMeasurement.updateCanvasData;
            Configuration.config.CollectionChanged += View.measurements.ChannelPower.updateCanvasData;
            Configuration.config.CollectionChanged += FilterBandwith.updateCanvasData;
            View.measurements.NormalMeasurement.updateCanvasData(null, null);
            View.measurements.ChannelPower.updateCanvasData(null, null);
            View.measurements.FilterBandwith.updateCanvasData(null, null);
            initializedResources = true;
            ImGui.GetIO().FontGlobalScale = 1.4f;
        }

        ImGui.Begin("Spectrum Analyzer", Configuration.mainWindowFlags);
        Theme.drawExitButton(10, Color.Gray, Color.White);
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
                tab_Cal.renderCalibration();
                break;

            case 7:
                tab_Measurement.renderMeasurements();
                break;
        }

        ImGui.EndChild();
        drawCursor();
        ImGui.End();
    }
}