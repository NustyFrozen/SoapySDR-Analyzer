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

        private static bool editMode = false, initializedResources = false;

        private static ushort[] iconRange = new ushort[] { 0xe005, 0xf8ff, 0 };

        private static ImFontPtr PoppinsFont, IconFont;
        private bool visble = true;

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
            }
            if (Imports.GetAsyncKeyState(Imports.Keys.Insert))
            {
                Thread.Sleep(200);
                visble = !visble;
            }
            if (!visble) return;
            ImGui.Begin("Widget Manager", SoapySA.Configuration.mainWindowFlags);

            if (Theme.drawTextButton($"{FontAwesome5.Gear} Widget Editor"))
                editMode = !editMode;

            if (editMode)
            {
                ConfiguratorWindow.Render();
                return;
            }
            ImGui.BeginTabBar("widgets");
            foreach (var widget in Widgets)
            {
                if (ImGui.BeginTabItem(widget.Key))
                {
                    widget.Value.window.renderWidget();
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
            ImGui.End();
        }
    }
}