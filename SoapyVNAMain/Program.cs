// To customize application configuration such as set high DPI settings or default font,
// see https://aka.ms/applicationconfiguration.

using ImGuiNET;
using SoapyRL;
using SoapySA;
using SoapySA.Extentions;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNAMain;
using SoapyVNAMain.View;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

//ApplicationConfiguration.Initialize();
DeviceHelper.SetupSoapyEnvironment();
if (OperatingSystem.IsWindows())
{
    SoapyVNACommon.Extentions.Imports.AllocConsole();
}
int screenWidth = 1920;
int screenHeight = 1080;
Veldrid.Rectangle rect;
unsafe
{
    // 1. Force SDL to talk to the OS video drivers
    Sdl2Native.SDL_Init(SDLInitFlags.Video);

    // 2. Now query the primary monitor (Index 0)
    if (Sdl2Native.SDL_GetDisplayBounds(0, &rect) != 0)
    {
        // Fallback if the driver fails (common in some Linux headless environments)
        screenWidth = 1920;
        screenHeight = 1080;
    }
    else
    {
        screenWidth = rect.Width;
        screenHeight = rect.Height;
    }
}
UserScreenConfiguration.UpdateWindowSize(new Vector2(screenWidth, screenHeight));
// Create window + graphics device (SDL2 window under the hood)
VeldridStartup.CreateWindowAndGraphicsDevice(
    new WindowCreateInfo(
        x: 0, y: 0,
        windowWidth: screenWidth, windowHeight: screenHeight,
        WindowState.Normal,
        windowTitle: "SoapySDR Analyzer"
    ),
    new GraphicsDeviceOptions(debug: true),
    out var window,
    out var gd
);
SoapyRL.Configuration.ScreenSize = new Vector2(screenWidth, screenHeight);
// Make it borderless
window.BorderVisible = false; // requires Veldrid 4.5.0+ :contentReference[oaicite:1]{index=1}

// ImGui renderer
var imgui = new ImGuiRenderer(
    gd,
    gd.MainSwapchain.Framebuffer.OutputDescription,
    window.Width,
    window.Height
);
window.Resized += Window_Resized;

void Window_Resized()
{
    gd.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
    imgui.WindowResized(window.Width, window.Height);
}

var cl = gd.ResourceFactory.CreateCommandList();
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
double last = stopwatch.Elapsed.TotalSeconds;
WidgetsWindow widgetsWindow = new WidgetsWindow(imgui);
widgetsWindow.LoadExistingWidgets();
Theme.InitDefaultTheme();
widgetsWindow.LoadResources();
ImGui.GetIO().FontGlobalScale = 1.4f;
Theme.InitDefaultTheme();

var Overlay = widgetsWindow as Overlay;
while (window.Exists)
{
    var input = window.PumpEvents();
    if (!window.Exists) break;

    double now = stopwatch.Elapsed.TotalSeconds;
    float dt = (float)(now - last);
    last = now;

   

    imgui.Update(dt, input);

    // ---- ImGui UI ----
    try
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
        Overlay.Render();
    }catch (Exception ex){
        Console.WriteLine(ex.Message);
    }
    // -------------------

    cl.Begin();
    cl.SetFramebuffer(gd.MainSwapchain.Framebuffer);
    cl.ClearColorTarget(0, RgbaFloat.Black);
    imgui.Render(gd, cl);
    cl.End();

    gd.SubmitCommands(cl);
    gd.SwapBuffers(gd.MainSwapchain);
}

gd.Dispose();