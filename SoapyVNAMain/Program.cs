// To customize application configuration such as set high DPI settings or default font,
// see https://aka.ms/applicationconfiguration.

using System.Drawing;
using System.Numerics;
using ImGuiNET;
using NLog;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using SoapyRL;
using SoapySA;
using SoapySA.Extentions;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNAMain;
using SoapyVNAMain.View;

//ApplicationConfiguration.Initialize();
DeviceHelper.SetupSoapyEnvironment();
if (OperatingSystem.IsWindows())
{
    SoapyVNACommon.Extentions.Imports.AllocConsole();
}
int screenWidth = 1920;
int screenHeight = 1080;
using var window = Window.Create(WindowOptions.Default);

// Declare some variables
ImGuiController? controller = null;
GL? gl = null;
IInputContext? inputContext = null;

// Our loading function
window.Load += () =>
{
    controller = new ImGuiController(
        gl = window.CreateOpenGL(), // load OpenGL
        window, // pass in our window
        inputContext =
            window.CreateInput() // create an input context
        ,
        onConfigureIO: () =>
        {
            
            //adding fonts
            WidgetsWindow.LoadResources();
            Theme.InitDefaultTheme();
            ImGui.GetIO().FontGlobalScale = 1.4f * UserScreenConfiguration.GetDefaultScaleSize().X;
            Theme.InitDefaultTheme();
        }
    );
    var monitor = window.Monitor;
    var videoMode = monitor.VideoMode;
    var resolution = videoMode.Resolution;
    screenWidth = resolution.Value[0];
    screenHeight = resolution.Value[1];
    window.Size = new Silk.NET.Maths.Vector2D<int>(screenWidth, screenHeight);
    window.Position = new Silk.NET.Maths.Vector2D<int>(0, 0);
        Logger Logger = LogManager.GetCurrentClassLogger();
Logger.Info($"Window Size -> ({screenWidth},{screenHeight})");
    UserScreenConfiguration.UpdateWindowSize(new Vector2(screenWidth,screenHeight));
     ImGui.GetIO().FontGlobalScale = 1.4f * UserScreenConfiguration.GetDefaultScaleSize().X;
            
};

    window.Resize+= (x) =>
      UserScreenConfiguration.UpdateWindowSize(new Vector2(screenWidth,screenHeight));
// Handle resizes
window.FramebufferResize += s =>
{
    // Adjust the viewport to the new window size
    gl.Viewport(s);
};

// The closing function
window.Closing += () =>
{
    controller?.Dispose();
    inputContext?.Dispose();
    gl?.Dispose();
};
WidgetsWindow widgetsWindow = new WidgetsWindow();
widgetsWindow.LoadExistingWidgets();

var Overlay = widgetsWindow as Overlay;

// The render function
window.Render += delta =>
{
    controller.Update((float)delta);

    try
    {
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
        Overlay.Render();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
    controller.Render();
};
window.Run();
window.Dispose();
