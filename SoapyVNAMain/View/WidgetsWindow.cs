using System.Numerics;
using ClickableTransparentOverlay;
using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using SoapyRL.View;
using SoapySA;
using SoapySA.View;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapyVNAMain.View;

internal class WidgetsWindow : Overlay
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static Dictionary<string, DefinedWidget> Widgets = new();

    private static bool _visble = true, _initializedResources;

    private static ushort[] _iconRange = new ushort[] { 0xe005, 0xf8ff, 0 };

    private static ImFontPtr _poppinsFont, _iconFont;
    public static bool EditMode;
    private DefinedWidget _selectedWidget = new() { IsComplete = false };

    public WidgetsWindow() : base(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
    {
        VSync = true;
    }

    public unsafe void LoadResources()
    {
        Logger.Debug("Loading Application Resources");
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
        Logger.Debug("Replaced font");

        _poppinsFont = io.Fonts.AddFontFromFileTTF(@"Fonts\Poppins-Light.ttf", 16);
        //IconFont = io.Fonts.AddFontFromFileTTF(@"Fonts\fa-solid-900.ttf", 16,, new ushort[] { 0xe005,
        //0xf8ff,0});
    }

    private void LoadExistingWidgets()
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

    protected override void Render()
    {
        if (!_initializedResources)
        {
            Theme.InitDefaultTheme();
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.SetNextWindowSize(Configuration.GetScreenSize());
            _initializedResources = true;
            LoadResources();
            ImGui.GetIO().FontGlobalScale = 1.4f;
            Theme.InitDefaultTheme();
            Theme.SetScaleSize(Configuration.GetDefaultScaleSize());
            LoadExistingWidgets();
        }

        double valuee = 0;
        if (Imports.GetAsyncKeyState(Imports.Keys.Insert))
        {
            Thread.Sleep(200);
            _visble = !_visble;
        }

        if (!_visble) return;
        ImGui.Begin("Widget Manager", Configuration.MainWindowFlags);

        if (!EditMode)
            foreach (var widget in Widgets)
            {
                ImGui.SameLine();
                if (Theme.DrawTextButton($"{FontAwesome5.MobileScreen} {widget.Key}"))
                {
                    //can be default null which doesn't have a window handle
                    if (_selectedWidget.IsComplete)
                        _selectedWidget.Window.ReleaseSdr();

                    _selectedWidget = widget.Value;
                    _selectedWidget.Window.HandleSdr();
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
                            Configuration.GetScreenSize() - ImGui.GetCursorPos(), value.Device);
                        break;

                    case 1:
                        value.Window = new MainWindow(key, ImGui.GetCursorPos(),
                            Configuration.GetScreenSize() - ImGui.GetCursorPos(), value.Device);
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