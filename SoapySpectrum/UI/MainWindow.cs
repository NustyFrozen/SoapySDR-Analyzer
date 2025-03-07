using ClickableTransparentOverlay;
using Design_imGUINET;
using ImGuiNET;
using SoapySpectrum.Extentions;
using SoapySpectrum.Extentions.Design_imGUINET;
using System.Numerics;
namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        static int tabID = 0;
        string[] availableTabs = new string[] { $"\uf2db Device", $"\ue473 Amplitude", $"\uf1fe BW", $"{FontAwesome5.WaveSquare} Frequency", $"\uf3c5 Trace & Marker", $"\uf085 Calibration" };
        bool visble = true;
        public UI() : base(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
        {
            this.VSync = true;
        }

        protected override Task PostInitialized()
        {
            VSync = false;
            return Task.CompletedTask;
        }
        public static uint ToUint(Color c)
        {
            uint u = (uint)c.A << 24;
            u += (uint)c.B << 16;
            u += (uint)c.G << 8;
            u += c.R;
            return u;
        }

        private static ushort[] iconRange = new ushort[] { 0xe005, 0xf8ff, 0 };

        static ImFontPtr PoppinsFont, IconFont;
        public bool initializedResources = false;

        public unsafe void loadResources()
        {
            Logger.Debug("Loading Application Resources");
            var io = ImGui.GetIO();

            this.ReplaceFont(config =>
            {
                var io = ImGui.GetIO();
                io.Fonts.AddFontFromFileTTF(@"Fonts\Poppins-Light.ttf", 16, config, io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
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

            PoppinsFont = io.Fonts.AddFontFromFileTTF(@"Fonts\Poppins-Light.ttf", 16);
            //IconFont = io.Fonts.AddFontFromFileTTF(@"Fonts\fa-solid-900.ttf", 16,, new ushort[] { 0xe005,
            //0xf8ff,0});
        }

        public static void drawCursor()
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.None);
            var cursorpos = ImGui.GetMousePos();
            ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X - 5, cursorpos.Y), new Vector2(cursorpos.X + 5, cursorpos.Y), Color.White.ToUint());
            ImGui.GetForegroundDrawList().AddLine(new Vector2(cursorpos.X, cursorpos.Y - 5), new Vector2(cursorpos.X, cursorpos.Y + 5), Color.White.ToUint());
        }
        protected unsafe override void Render()
        {
            Thread.Sleep(1);
            var inputTheme = ImGuiTheme.getTextTheme();
            if (Imports.GetAsyncKeyState(Keys.Insert))
            {
                Thread.Sleep(200);
                visble = !visble;
            }
            if (!visble) return;
            if (!initializedResources)
            {
                setupSoapyEnvironment();
                refreshDevices();
                waitForMouseClick.Start();
                markerMoveKeys.Start();
                initializeTraces();
                loadResources();
                ImGui.SetNextWindowPos(Configuration.mainWindow_Pos);
                ImGui.SetNextWindowSize(Configuration.mainWindow_Size);
                initializedResources = true;
            }
            ImGui.Begin("Spectrum Analyzer", Configuration.mainWindow_flags);
            ImGuiTheme.drawExitButton(15, Color.Gray, Color.White);

            ImGui.BeginChild("Spectrum Graph", Configuration.graph_Size);
            drawGraph();
            ImGui.EndChild();


            ImGui.SetCursorPos(new Vector2(Configuration.graph_Size.X + 60 * Configuration.scale_Size.X, 10));
            ImGui.BeginChild("Spectrum Options", Configuration.option_Size);
            ImGuiTheme.newLine();
            ImGuiTheme.newLine();
            inputTheme.prefix = "RBW";
            ImGuiTheme.glowingCombo("InputSelectortext4", ref tabID, availableTabs, inputTheme);
            ImGuiTheme.newLine();
            switch (tabID)
            {
                case 0:
                    renderDevice();
                    break;
                case 1:
                    renderAmplitude();
                    break;
                case 2:
                    renderVideo();
                    break;
                case 3:
                    renderFrequency();
                    break;
                case 4:
                    renderTrace();
                    break;
                case 5:
                    renderCalibration();
                    break;
            }
            ImGui.EndChild();
            drawCursor();
            ImGui.End();
        }
    }
}
