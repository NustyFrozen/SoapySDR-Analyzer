using ClickableTransparentOverlay;
using ImGuiNET;
using NLog;
using SoapySA;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapyVNAMain.View
{
    internal class WidgetsWindow : Overlay
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static Dictionary<string, definedWidget> Widgets = new Dictionary<string, definedWidget>();
        private definedWidget selectedWidget = new definedWidget() { isComplete = false };

        private static bool visble = true, initializedResources = false;

        private static ushort[] iconRange = new ushort[] { 0xe005, 0xf8ff, 0 };

        private static ImFontPtr PoppinsFont, IconFont;
        public static bool editMode = false;

        public WidgetsWindow() : base(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
        {
            VSync = true;
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

        private void loadExistingWidgets()
        {
            if (!Directory.Exists(Global.configPath))
                Directory.CreateDirectory(Global.configPath);
            foreach (var widgetFullName in Directory.GetDirectories(Global.configPath))
                try
                {
                    var name = widgetFullName.Replace($"{Global.configPath}\\", "");
                    var widget = definedWidget.loadWidget(widgetFullName);
                    //making sure device is connected and re-intializing the device in the
                    widget.isComplete = false;
                    try
                    {
                        widget.device.sdrDevice = new Pothosware.SoapySDR.Device(widget.device.Descriptor);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error Loading Widget Device Initialization {widgetFullName} --> {ex.Message}");
                        widget.attempted = true;
                    }
                    Widgets.Add(name, widget);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error Loading Widget {widgetFullName} --> {ex.Message}");
                }
        }

        protected override void Render()
        {
            if (!initializedResources)
            {
                Theme.initDefaultTheme();
                ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 0));
                ImGui.SetNextWindowSize(SoapySA.Configuration.getScreenSize());
                initializedResources = true;
                loadResources();
                ImGui.GetIO().FontGlobalScale = 1.4f;
                Theme.initDefaultTheme();
                Theme.setScaleSize(Configuration.getDefaultScaleSize());
                loadExistingWidgets();
            }
            if (Imports.GetAsyncKeyState(Imports.Keys.Insert))
            {
                Thread.Sleep(200);
                visble = !visble;
            }
            if (!visble) return;
            ImGui.Begin("Widget Manager", SoapySA.Configuration.mainWindowFlags);

            if (!editMode)
                foreach (var widget in Widgets)
                {
                    ImGui.SameLine();
                    if (Theme.drawTextButton($"{FontAwesome5.MobileScreen} {widget.Key}"))
                    {
                        //can be default null which doesn't have a window handle
                        if (selectedWidget.isComplete)
                            selectedWidget.window.releaseSDR();

                        selectedWidget = widget.Value;
                        selectedWidget.window.handleSDR();
                    }
                }
            ImGui.SameLine();
            if (Theme.drawTextButton((editMode) ? $"{FontAwesome5.ArrowLeft}" : $"{FontAwesome5.Plus}"))
                editMode = !editMode;
            if (editMode)
            {
                ConfiguratorWindow.Render();
                return;
            }
            foreach (var key in Widgets.Keys)
            {
                if (!Widgets[key].isComplete)
                {
                    if (Widgets[key].attempted)
                    {
                        ImGui.Text($"{key} UnInitiated Device {Widgets[key].device.Descriptor} not found");
                        Theme.newLine();
                        if (Theme.drawTextButton("Retry Initializing"))
                        {
                            try
                            {
                                Widgets[key].device.sdrDevice = new Pothosware.SoapySDR.Device(Widgets[key].device.Descriptor);
                                Widgets[key].attempted = false;
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Error Loading Widget Device Initialization {key} --> {ex.Message}");
                                Widgets[key].attempted = true;
                                continue;//still can't find device
                            }
                        }
                    }
                    var value = Widgets[key];
                    switch (value.widgetType)
                    {
                        case 0:
                            value.window = new SoapySA.View.MainWindow(key, ImGui.GetCursorPos(), Configuration.getScreenSize() - ImGui.GetCursorPos(), value.device);
                            break;

                        case 1:
                            value.window = new SoapyRL.View.MainWindow(key, ImGui.GetCursorPos(), Configuration.getScreenSize() - ImGui.GetCursorPos(), value.device);
                            break;
                    }

                    value.isComplete = true;
                    value.window.initWidget();
                    Widgets[key] = value;
                }
            }
            if (selectedWidget.isComplete)
                selectedWidget.window.renderWidget();

            ImGui.End();
        }
    }
}