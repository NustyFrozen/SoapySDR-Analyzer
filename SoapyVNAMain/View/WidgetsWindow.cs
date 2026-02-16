using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using Silk.NET.OpenGL.Extensions.ImGui;
using SoapyRL.View;
using SoapySA;
using SoapySA.Extentions;
using SoapySA.View;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapyVNAMain.View;

internal class WidgetsWindow() : Overlay
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static Dictionary<string, DefinedWidget> Widgets = new();

    private static bool _visble = true, _initializedResources;

    private static ushort[] _iconRange = new ushort[] { 0xe005, 0xf8ff, 0 };

    private static ImFontPtr _poppinsFont, _iconFont;
    public static bool EditMode;
    private DefinedWidget _selectedWidget = new() { IsComplete = false };

    public static unsafe void LoadResources()
    {
        Logger.Debug("Loading Application Resources");
        var io = ImGui.GetIO();

        // 1) Clear old fonts
        io.Fonts.Clear();

        // --- SMOOTHNESS FIX 1: Increase Font Size ---
        // On a 1080p screen, 16.0f is often too small and gets aliased.
        // 18.0f or 20.0f usually looks much crisper.
        float baseFontSize = 16.0f;

        // Base text font
        ImFontPtr poppins = io.Fonts.AddFontFromFileTTF(
            "Fonts/Poppins-Light.ttf",
            baseFontSize,
            null,
            io.Fonts.GetGlyphRangesChineseSimplifiedCommon()
        );

        // 2) Merge icon font
        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        config->MergeMode = 1;
        config->PixelSnapH = 1;

        // --- SMOOTHNESS FIX 2: Increase Oversampling ---
        // Changing these from 1 to 3 makes the font atlas much smoother.
        config->OversampleH = 3;
        config->OversampleV = 1;

        ushort[] iconRanges = { 0xF000, 0xF8FF, 0 };

        fixed (ushort* pRanges = iconRanges)
        {
            io.Fonts.AddFontFromFileTTF(
                "Fonts/fa-solid-900.ttf",
                baseFontSize, // Match base font size for alignment
                config,
                (IntPtr)pRanges
            );
        }

        // Set default font
        ImGuiNative.igGetIO()->FontDefault = poppins.NativePtr;

        // 3) Rebuild + upload atlas texture
        // Ensure your 'renderer' variable is the ImGuiRenderer instance
        

        // Clean up native config
        ImGuiNative.ImFontConfig_destroy(config);
    }

    public void LoadExistingWidgets()
    {
        if (!Directory.Exists(Global.ConfigPath))
            Directory.CreateDirectory(Global.ConfigPath);
        foreach (var widgetFullName in Directory.GetDirectories(Global.ConfigPath))
            try
            {
                var name = widgetFullName.Replace($"{Global.ConfigPath}\\", "");
                var widget = DefinedWidget.LoadWidget(widgetFullName);
                //making sure device is connected and re-intializing the device in the
                widget.IsComplete = false;
                try
                {
                    widget.Device.SdrDevice = new Device(widget.Device.Descriptor);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error Loading Widget Device Initialization {widgetFullName} --> {ex.Message}");
                    widget.Attempted = true;
                }

                Widgets.Add(name, widget);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error Loading Widget {widgetFullName} --> {ex.Message}");
            }
    }

     void Overlay.Render()
    {
        if (!_initializedResources)
        {
            Theme.SetScaleSize(UserScreenConfiguration.GetDefaultScaleSize());
            _initializedResources = true;
        }
        

        if (!_visble) return;
        ImGui.Begin("Widget Manager", UserScreenConfiguration.MainWindowFlags);

        if (!EditMode)
            foreach (var widget in Widgets)
            {
                ImGui.SameLine();
                if (Theme.DrawTextButton($"{FontAwesome5.MobileScreen} {widget.Key}"))
                {
                    //can be default null which doesn't have a window handle
                    if (_selectedWidget.IsComplete)
                        _selectedWidget.Window.WidgetExit();

                    _selectedWidget = widget.Value;
                    _selectedWidget.Window.WidgetEnter();
                }
            }

        ImGui.SameLine();
        if (Theme.DrawTextButton(EditMode ? $"{FontAwesome5.ArrowLeft}" : $"{FontAwesome5.Plus}"))
            EditMode = !EditMode;
        if (EditMode)
        {
            ConfiguratorWindow.Render();
            return;
        }

        foreach (var key in Widgets.Keys)
            if (!Widgets[key].IsComplete)
            {
                if (Widgets[key].Attempted)
                {
                    ImGui.Text($"{key} UnInitiated Device {Widgets[key].Device.Descriptor} not found");
                    Theme.NewLine();
                    if (Theme.DrawTextButton("Retry Initializing"))
                        try
                        {
                            Widgets[key].Device.SdrDevice = new Device(Widgets[key].Device.Descriptor);
                            Widgets[key].Attempted = false;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error Loading Widget Device Initialization {key} --> {ex.Message}");
                            Widgets[key].Attempted = true;
                            continue; //still can't find device
                        }
                }

                var value = Widgets[key];
                switch (value.WidgetType)
                {
                    case 0:
                        value.Window = new MainWindowView(key, ImGui.GetCursorPos(),
                            UserScreenConfiguration.windowSize - ImGui.GetCursorPos(), value.Device);
                        break;

                    case 1:
                        value.Window = new MainWindow(key, ImGui.GetCursorPos(),
                            UserScreenConfiguration.windowSize - ImGui.GetCursorPos(), value.Device);
                        break;
                }

                value.IsComplete = true;
                value.Window.InitWidget();
                Widgets[key] = value;
            }

        if (_selectedWidget.IsComplete)
            _selectedWidget.Window.RenderWidget();
        foreach (var touchScreenMenu in TouchScreenMenu.picks)
        {
            touchScreenMenu.RenderFrequencyPicker();
        }
        ImGui.End();
    }


}