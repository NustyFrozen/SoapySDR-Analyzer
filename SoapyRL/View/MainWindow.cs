﻿using ClickableTransparentOverlay;
using ImGuiNET;
using NLog;
using SoapyRL.Extentions;
using SoapyRL.View.tabs;
using System.Numerics;

namespace SoapyRL.View;

public class UI : Overlay
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static ushort[] iconRange = new ushort[] { 0xe005, 0xf8ff, 0 };

    private static ImFontPtr PoppinsFont, IconFont;

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

    protected override void Render()
    {
        var inputTheme = Theme.getTextTheme();
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
            Graph.s_waitForMouseClick.Start();
            Graph.initializeGraphElements();
            loadResources();
            ImGui.SetNextWindowPos(Configuration.mainWindowPos);
            ImGui.SetNextWindowSize(Configuration.mainWindowSize);
            ImGui.GetIO().FontGlobalScale = 1.4f;
            initializedResources = true;
        }

        ImGui.Begin("Return Loss", Configuration.mainWindowFlags);
        Theme.drawExitButton(15, Color.Gray, Color.White);

        ImGui.BeginChild("Spectrum Graph", Configuration.graphSize);
        Graph.drawGraph();
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(Configuration.graphSize.X + 60 * Configuration.scaleSize.X, 10));
        ImGui.BeginChild("Options", Configuration.optionSize);
        Theme.newLine();
        tab_Device.renderDevice();

        ImGui.EndChild();
        drawCursor();
        ImGui.End();
    }
}