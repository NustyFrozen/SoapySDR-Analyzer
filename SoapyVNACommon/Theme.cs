using System.Drawing;
using System.Numerics;
using ImGuiNET;
using SoapyVNACommon.Extentions;

namespace SoapyVNACommon;

public class Theme
{
    public enum CircleState
    {
        FadeIn,
        FadeOut,
        Spin,
        WaitEnd,
        Idle
    }

    public enum Sliderstatus
    {
        Idle,
        Start,
        End
    }

    public static GlowingInputConfigurator InputTheme = GetTextTheme();
    public static ButtonConfigurator ButtonTheme = GetbuttonTheme();
    public static ButtonConfigurator TextbuttonTheme = GetTextButtonTheme();

    public static SliderInputConfigurator SliderTheme = GetSliderTheme();

    private static readonly Dictionary<string, object> FrameData = new();
    private static Vector2 _scaleSize = new(1.5f, 1.5f);

    public static void SetScaleSize(Vector2 size)
    {
        _scaleSize = size;
        InputTheme = GetTextTheme();
        ButtonTheme = GetbuttonTheme();
        SliderTheme = GetSliderTheme();
        TextbuttonTheme = GetTextButtonTheme();
    }

    public static void InitDefaultTheme()
    {
        var io = ImGui.GetIO();
        var style = ImGui.GetStyle();
        style.Colors[ImGuiCol.WindowBg.ToInt()] = Color.FromArgb(255, 19, 20, 25).ToVec4();
        style.Colors[ImGuiCol.Border.ToInt()] = Color.FromArgb(255, 88, 37, 227).ToVec4();
        style.Colors[ImGuiCol.FrameBg.ToInt()] = Color.FromArgb(255, 21, 21, 21).ToVec4();
        style.Colors[ImGuiCol.FrameBgHovered.ToInt()] = Color.FromArgb(255, 203, 203, 203).ToVec4();
        style.Colors[ImGuiCol.FrameBgActive.ToInt()] = Color.FromArgb(255, 203, 203, 203).ToVec4();
        style.Colors[ImGuiCol.CheckMark.ToInt()] = Color.FromArgb(255, 91, 36, 221).ToVec4();
        //style.Colors[ImGuiCol.FrameBg] =
        style.WindowBorderSize = 1;
        style.WindowRounding = 5;
    }

    public static bool Button(string text)
    {
        ButtonTheme.Text = text;
        return Button("label###ID", ButtonTheme);
    }

    public static void AnimateProperties()
    {
        var style = ImGui.GetStyle();
        foreach (var pair in FrameData)
            if (pair.Value is ColFrame)
            {
                var data = (ColFrame)pair.Value;
                data.Progress += data.Speed;
                if (data.Progress >= 1)
                {
                    FrameData.Remove(pair.Key);
                    break;
                }

                style.Colors[data.Type.ToInt()] = data.Start.Lerp(data.End, data.Progress).ToVec4();
                FrameData[pair.Key] = data;
            }
    }

    public static void LerpColorElement(ImGuiCol colId, Vector4 color, float speed)
    {
        FrameData.Add(colId.ToString(), new ColFrame
        {
            Start = ImGui.GetStyle().Colors[colId.ToInt()].ToColor(),
            End = color.ToColor(),
            Speed = speed,
            Type = colId
        });
    }

    public static void Text(string text)
    {
        var cfg = GetTextTheme();
        //a better text method that centers
        var size = cfg.Size.X / 2 - ImGui.CalcTextSize(text).X / 2;
        ImGui.SetCursorPosX(size);
        ImGui.Text(text);
    }

    public static void Text(string text, GlowingInputConfigurator cfg)
    {
        //a better text method that centers
        var size = cfg.Size.X / 2 - ImGui.CalcTextSize(text).X / 2;
        ImGui.SetCursorPosX(size);
        ImGui.Text(text);
    }

    public static bool Button(string label, ButtonConfigurator cfg)
    {
        label = "##" + label;
        var results = false;
        object obj = cfg.Bgcolor;
        var borderColorActive = cfg.ColorHover;
        var fadestep = 0.0f;
        var status = CircleState.Idle;
        var hoverStep = 0.0f;
        var currentSpinValue = 0f;
        CircleButtonFrame frameTickData;
        var flag = FrameData.TryGetValue(label, out obj);
        if (flag)
        {
            frameTickData = (CircleButtonFrame)obj;
            borderColorActive = frameTickData.Color;
            fadestep = frameTickData.WaitFade;
            status = frameTickData.Status;
            currentSpinValue = frameTickData.Current;
            hoverStep = frameTickData.HoverFade;
        }
        else
        {
            FrameData.Add(label, new CircleButtonFrame
            {
                Color = borderColorActive
            });
        }

        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        if (status != CircleState.Idle)
        {
            var yOffset = 0.0f;
            switch (status)
            {
                case CircleState.FadeIn:
                    fadestep += cfg.SlideSpeed;
                    yOffset = fadestep;
                    break;

                case CircleState.FadeOut:
                    fadestep -= cfg.SlideSpeed;
                    yOffset = fadestep;
                    break;

                case CircleState.Spin:
                    fadestep += cfg.WaitSpeed;
                    yOffset = 1;
                    break;
            }

            CircleProgressBarAnimated(label + "_CIRCLESPIN", new Vector2(cursorPos.X + cfg.Size.X / 2.0f
                    , cursorPos.Y + cfg.Size.Y + yOffset * cfg.CirclePositionY)
                , cfg.CircleRadius, cfg.CircleColor, cfg.CircleThickness, cfg.CircleSpeed, 0.9f);
            if (fadestep >= 1 && status == CircleState.FadeIn)
            {
                fadestep = 0;
                status = CircleState.Spin;
            }

            if (fadestep <= 0 && status == CircleState.FadeOut)
            {
                status = CircleState.Idle;
                results = true;
            }

            if (fadestep >= 1 && status == CircleState.Spin) status = CircleState.FadeOut;
        }

        var startDrawBg = new Vector2(windowpos.X + cursorPos.X, windowpos.Y + cursorPos.Y);
        var endDrawBg = new Vector2(windowpos.X + cursorPos.X + cfg.Size.X, windowpos.Y + cursorPos.Y + cfg.Size.Y);
        draw.AddRectFilled(startDrawBg, endDrawBg, borderColorActive, cfg.RoundCorners);
        var windowbgColor = style.Colors[ImGuiCol.WindowBg.ToInt()].ToColor();
        //var temp = style.Colors[ImGuiCol.FrameBg.toInt()];
        var temp2 = style.Colors[ImGuiCol.Text.ToInt()];
        var temp3 = ImGui.GetFontSize();
        //style.Colors[ImGuiCol.FrameBg.toInt()] = cfg.bgcolor.toColor().toVec4();
        style.FrameBorderSize = 0;
        style.Colors[ImGuiCol.Text.ToInt()] = cfg.TextColor.ToColor().ToVec4();

        draw.AddText(
            new Vector2(startDrawBg.X + cfg.Size.X / 2.0f - ImGui.CalcTextSize(cfg.Text).X / 2.0f
                , windowpos.Y + cursorPos.Y + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(cfg.Text).Y / 2.0f)
            , cfg.TextColor.ToColor().ToUint(), cfg.Text);

        if (ImGui.IsMouseHoveringRect(startDrawBg, endDrawBg))
        {
            if (ImGui.IsMouseClicked((int)ImGuiMouseButton.Left) && status == CircleState.Idle) return true;
            borderColorActive = cfg.Bgcolor.ToColor().Lerp(cfg.ColorHover.ToColor(), hoverStep).ToUint();
            if (hoverStep <= 1)
                hoverStep += 0.001f;
        }
        else
        {
            if (hoverStep >= 0)
                hoverStep -= 0.001f;
            borderColorActive = cfg.Bgcolor.ToColor().Lerp(cfg.ColorHover.ToColor(), hoverStep).ToUint();
        }

        FrameData[label] = new CircleButtonFrame
        {
            Color = borderColorActive,
            WaitFade = fadestep,
            Current = currentSpinValue,
            Status = status,
            HoverFade = hoverStep
        };
        //style.Colors[ImGuiCol.FrameBg.toInt()] = temp;
        style.Colors[ImGuiCol.Text.ToInt()] = temp2;
        return results;
    }
    public enum ImGuiMouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        COUNT = 5
    }
    public static bool ButtonWait(string label, ButtonConfigurator cfg)
    {
        label = "##" + label;
        var results = false;
        object obj = cfg.Bgcolor;
        var borderColorActive = cfg.ColorHover;
        var fadestep = 0.0f;
        var status = CircleState.Idle;
        var hoverStep = 0.0f;
        var currentSpinValue = 0f;
        CircleButtonFrame frameTickData;
        var flag = FrameData.TryGetValue(label, out obj);
        if (flag)
        {
            frameTickData = (CircleButtonFrame)obj;
            borderColorActive = frameTickData.Color;
            fadestep = frameTickData.WaitFade;
            status = frameTickData.Status;
            currentSpinValue = frameTickData.Current;
            hoverStep = frameTickData.HoverFade;
        }
        else
        {
            FrameData.Add(label, new CircleButtonFrame
            {
                Color = borderColorActive
            });
        }

        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        if (status != CircleState.Idle)
        {
            var yOffset = 0.0f;
            switch (status)
            {
                case CircleState.FadeIn:
                    fadestep += cfg.SlideSpeed;
                    yOffset = fadestep;
                    break;

                case CircleState.FadeOut:
                    fadestep -= cfg.SlideSpeed;
                    yOffset = fadestep;
                    break;

                case CircleState.Spin:
                    fadestep += cfg.WaitSpeed;
                    yOffset = 1;
                    break;
            }

            CircleProgressBarAnimated(label + "_CIRCLESPIN", new Vector2(cursorPos.X + cfg.Size.X / 2.0f
                    , cursorPos.Y + cfg.Size.Y + yOffset * cfg.CirclePositionY)
                , cfg.CircleRadius, cfg.CircleColor, cfg.CircleThickness, cfg.CircleSpeed, 0.9f);
            if (fadestep >= 1 && status == CircleState.FadeIn)
            {
                fadestep = 0;
                status = CircleState.Spin;
            }

            if (fadestep <= 0 && status == CircleState.FadeOut)
            {
                status = CircleState.Idle;
                results = true;
            }

            if (fadestep >= 1 && status == CircleState.Spin) status = CircleState.FadeOut;
        }

        var startDrawBg = new Vector2(windowpos.X + cursorPos.X, windowpos.Y + cursorPos.Y);
        var endDrawBg = new Vector2(windowpos.X + cursorPos.X + cfg.Size.X, windowpos.Y + cursorPos.Y + cfg.Size.Y);
        draw.AddRectFilled(startDrawBg, endDrawBg, borderColorActive, cfg.RoundCorners);
        var windowbgColor = style.Colors[ImGuiCol.WindowBg.ToInt()].ToColor();
        //var temp = style.Colors[ImGuiCol.FrameBg.toInt()];
        var temp2 = style.Colors[ImGuiCol.Text.ToInt()];
        var temp3 = ImGui.GetFontSize();
        //style.Colors[ImGuiCol.FrameBg.toInt()] = cfg.bgcolor.toColor().toVec4();
        style.FrameBorderSize = 0;
        style.Colors[ImGuiCol.Text.ToInt()] = cfg.TextColor.ToColor().ToVec4();

        draw.AddText(
            new Vector2(startDrawBg.X + cfg.Size.X / 2.0f - ImGui.CalcTextSize(cfg.Text).X / 2.0f
                , windowpos.Y + cursorPos.Y + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(cfg.Text).Y / 2.0f)
            , cfg.TextColor.ToColor().ToUint(), cfg.Text);

        if (ImGui.IsMouseHoveringRect(startDrawBg, endDrawBg))
        {
            if (ImGui.IsMouseClicked((int)ImGuiMouseButton.Left) && status == CircleState.Idle) status = CircleState.FadeIn;
            borderColorActive = cfg.Bgcolor.ToColor().Lerp(cfg.ColorHover.ToColor(), hoverStep).ToUint();
            if (hoverStep <= 1)
                hoverStep += 0.001f;
        }
        else
        {
            if (hoverStep >= 0)
                hoverStep -= 0.001f;
            borderColorActive = cfg.Bgcolor.ToColor().Lerp(cfg.ColorHover.ToColor(), hoverStep).ToUint();
        }

        FrameData[label] = new CircleButtonFrame
        {
            Color = borderColorActive,
            WaitFade = fadestep,
            Current = currentSpinValue,
            Status = status,
            HoverFade = hoverStep
        };
        //style.Colors[ImGuiCol.FrameBg.toInt()] = temp;
        style.Colors[ImGuiCol.Text.ToInt()] = temp2;
        return results;
    }

    public static void GradientBorder(Vector4 topColor, Vector4 bottomColor)
    {
        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var windowsize = ImGui.GetWindowSize();
        var draw = ImGui.GetForegroundDrawList();
        var stepper = windowsize.Y * 0.01f;
        style.WindowBorderSize = 0;
        for (var i = 0.01f; i < 1; i += 0.001f)
        {
            draw.AddLine(
                new Vector2(windowpos.X,
                    windowpos.Y + i * windowsize.Y)
                , new Vector2(windowpos.X + 1, windowpos.Y + (i - 0.01f) * windowsize.Y),
                topColor.ToColor().Lerp(bottomColor.ToColor(), i).ToUint(), 2);
            draw.AddLine(
                new Vector2(windowpos.X + windowsize.X - 1,
                    windowpos.Y + i * windowsize.Y)
                , new Vector2(windowpos.X + windowsize.X, windowpos.Y + (i - 0.01f) * windowsize.Y),
                topColor.ToColor().Lerp(bottomColor.ToColor(), i).ToUint(), 3);
        }

        draw.AddRect(new Vector2(windowpos.X, windowpos.Y - 1), new Vector2(windowpos.X + windowsize.X + 1, windowpos.Y)
            , topColor.ToColor().ToUint(), style.WindowRounding,
            ImDrawCornerFlags.TopLeft| ImDrawCornerFlags.TopRight, 1.5f);
    }

    public static void GradientRect(Vector2 pMin, Vector2 pMax, Vector4 topColor, Vector4 bottomColor,
        float cornerRadius)
    {
        var windowpos = new Vector2(0, 0);
        var style = ImGui.GetStyle();
        var windowsize = new Vector2(pMin.X - pMax.X, pMin.Y - pMax.Y);
        var draw = ImGui.GetForegroundDrawList();
        var stepper = windowsize.Y * 0.01f;
        style.WindowBorderSize = 0;
        for (var i = 0.01f; i < 1; i += 0.01f)
        {
            draw.AddLine(
                new Vector2(windowpos.X + pMin.X,
                    pMin.Y + windowpos.Y + i * windowsize.Y)
                , new Vector2(windowpos.X + 1 + pMin.X, pMin.Y + windowpos.Y + (i - 0.01f) * windowsize.Y),
                topColor.ToColor().Lerp(bottomColor.ToColor(), i).ToUint(), 2);
            draw.AddLine(
                new Vector2(windowpos.X + windowsize.X - 1 + pMin.X,
                    windowpos.Y + i * windowsize.Y + pMin.Y)
                , new Vector2(windowpos.X + windowsize.X + pMin.X, windowpos.Y + pMin.Y + (i - 0.01f) * windowsize.Y),
                topColor.ToColor().Lerp(bottomColor.ToColor(), i).ToUint(), 3);
        }

        draw.AddRect(new Vector2(windowpos.X + pMin.X, windowpos.Y + pMin.Y - 1)
            , new Vector2(windowpos.X + windowsize.X + 1 + pMin.X, pMin.Y + windowpos.Y)
            , topColor.ToColor().ToUint(), cornerRadius,
            ImDrawCornerFlags.TopLeft | ImDrawCornerFlags.TopRight, 1.5f);
        draw.AddRect(new Vector2(windowpos.X + pMin.X, windowpos.Y + pMin.Y - 1 + windowsize.Y)
            , new Vector2(windowpos.X + windowsize.X + 1 + pMin.X, pMin.Y + windowpos.Y + windowsize.Y)
            , bottomColor.ToColor().ToUint(), cornerRadius,
            ImDrawCornerFlags.TopLeft | ImDrawCornerFlags.TopRight, 1.5f);
    }

    public static void GradientGlowingInput(string label, ref string text, GlowingInputConfigurator cfg,
        Vector4 bottomColor, uint maxlength = 32)
    {
        label = "##" + label;
        object obj = cfg.BorderColor;
        var borderColorActive = cfg.BorderColorActive;
        var borderThicknessActive = cfg.BorderThickness;
        var flag = FrameData.TryGetValue(label, out obj);
        if (flag)
        {
            borderColorActive = ((GlowingInputFrame)obj).Color;
            borderThicknessActive = ((GlowingInputFrame)obj).BorderThickness;
        }
        else
        {
            FrameData.Add(label, new GlowingInputFrame
            {
                BorderThickness = borderThicknessActive,
                Color = borderColorActive
            });
        }

        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        var startDrawBg = new Vector2(windowpos.X + cursorPos.X, windowpos.Y + cursorPos.Y);
        var endDrawBg = new Vector2(windowpos.X + cursorPos.X + cfg.Size.X, windowpos.Y + cursorPos.Y + cfg.Size.Y);
        draw.AddRectFilled(startDrawBg, endDrawBg, cfg.Bgcolor, cfg.RoundCorners);
        var windowbgColor = style.Colors[ImGuiCol.WindowBg.ToInt()].ToColor();
        var temp = style.Colors[ImGuiCol.FrameBg.ToInt()];
        var temp2 = style.Colors[ImGuiCol.Text.ToInt()];
        var temp3 = ImGui.GetFontSize();
        style.Colors[ImGuiCol.FrameBg.ToInt()] = cfg.Bgcolor.ToColor().ToVec4();
        style.FrameBorderSize = 0;
        style.Colors[ImGuiCol.Text.ToInt()] = cfg.TextColor.ToColor().ToVec4();
        var currentCursor = ImGui.GetCursorPos();

        ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPosX() + style.FramePadding.X,
            ImGui.GetCursorPosY() + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(cfg.Prefix).Y / 2.0f
        ));
        if (ImGui.InputText(label, ref text, maxlength))
        {
        }

        if (text == string.Empty && !ImGui.IsItemActive())
            draw.AddText(
                new Vector2(startDrawBg.X + style.FramePadding.X,
                    windowpos.Y + cursorPos.Y + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(cfg.Prefix).Y / 2.0f)
                , cfg.TextColor.ToColor().Brightness(0.4f).ToUint(), cfg.Prefix);
        if (ImGui.IsItemActive())
        {
            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColorActive.ToColor(), 0.01f).ToUint();
            if (borderThicknessActive <= cfg.BorderThickness + 3)
                borderThicknessActive += 0.01f;
        }
        else
        {
            if (borderThicknessActive > cfg.BorderThickness)
                borderThicknessActive -= 0.01f;
            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColor.ToColor(), 0.01f).ToUint();
        }

        FrameData[label] = new GlowingInputFrame
        {
            BorderThickness = borderThicknessActive,
            Color = borderColorActive
        };

        GradientRect(new Vector2(startDrawBg.X + cfg.Size.X, startDrawBg.Y + +cfg.Size.Y),
            new Vector2(endDrawBg.X + cfg.Size.X, endDrawBg.Y + cfg.Size.Y), borderColorActive.ToColor().ToVec4(),
            bottomColor, cfg.RoundCorners);

        style.Colors[ImGuiCol.FrameBg.ToInt()] = temp;
        style.Colors[ImGuiCol.Text.ToInt()] = temp2;
    }

    public static bool DrawTextGradient(string label, Vector2 pos, string text, Color clrDefault, Color clrHover)
    {
        object obj = clrDefault;
        var exitColorActive = clrDefault;
        var flag = FrameData.TryGetValue(label, out obj);
        var results = false;
        if (flag)
            exitColorActive = (Color)obj;
        else
            FrameData.Add(label, exitColorActive);
        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var size = ImGui.CalcTextSize(text);
        var start = new Vector2(windowpos.X + pos.X, windowpos.Y + pos.Y);
        var end = new Vector2(start.X + size.X, start.Y + size.Y);
        draw.AddText(start, exitColorActive.ToUint(), text);
        if (ImGui.IsMouseHoveringRect(start, end))
        {
            exitColorActive = exitColorActive.Lerp(clrHover, 0.025);
            if (ImGui.IsMouseClicked((int)ImGuiMouseButton.Left)) results = true;
        }
        else
        {
            exitColorActive = exitColorActive.Lerp(clrDefault, 0.025);
        }

        FrameData[label] = exitColorActive;
        return results;
    }

    public static bool DrawTextButton(string text)
    {
        var clrDefault = TextbuttonTheme.Bgcolor.ToColor();
        object obj = clrDefault;
        var clrHover = TextbuttonTheme.ColorHover.ToColor();
        var exitColorActive = clrHover;
        var flag = FrameData.TryGetValue($"DrawTextButtonLabel{text}", out obj);
        if (flag)
            exitColorActive = (Color)obj;
        else
            FrameData.Add($"DrawTextButtonLabel{text}", exitColorActive);
        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        var start = cursorPos;
        ImGui.Text(text);
        draw.AddText(cursorPos, exitColorActive.ToUint(), text);
        var end = start + ImGui.CalcTextSize(text);
        if (ImGui.IsMouseHoveringRect(start, end))
        {
            exitColorActive = exitColorActive.Lerp(clrHover, 0.1);
            if (ImGui.IsMouseClicked((int)ImGuiMouseButton.Left)) return true;
        }
        else
        {
            exitColorActive = exitColorActive.Lerp(clrDefault, 0.1);
        }

        FrameData[$"DrawTextButtonLabel{text}"] = exitColorActive;
        return false;
    }

    public static void DrawExitButton(float size, Color clrDefault, Color clrHover)
    {
        object obj = clrDefault;
        var exitColorActive = clrDefault;
        var flag = FrameData.TryGetValue("DrawExitButtonLabel", out obj);
        if (flag)
            exitColorActive = (Color)obj;
        else
            FrameData.Add("DrawExitButtonLabel", exitColorActive);
        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        var start = new Vector2(windowpos.X + ImGui.GetContentRegionAvail().X - size - 5, windowpos.Y - size + 30);
        var end = new Vector2(start.X + size, start.Y + size);
        if (ImGui.IsMouseHoveringRect(start, end))
        {
            exitColorActive = exitColorActive.Lerp(clrHover, 0.1);
            if (ImGui.IsMouseClicked((int)ImGuiMouseButton.Left)) Environment.Exit(0);
        }
        else
        {
            exitColorActive = exitColorActive.Lerp(clrDefault, 0.1);
        }

        FrameData["DrawExitButtonLabel"] = exitColorActive;
        draw.AddLine(start, end, exitColorActive.ToUint(), 2);
        draw.AddLine(new Vector2(end.X, start.Y), new Vector2(start.X, end.Y), exitColorActive.ToUint(), 2);
    }

    public static bool Slider(string label, float min, float max, ref float value, SliderInputConfigurator cfg)
    {
        var scaled = (float)Imports.Scale(value, min, max, 0, 1);
        var results = Slider(label, ref scaled, cfg);
        value = (float)Imports.Scale(scaled, 0, 1, min, max);
        return results;
    }

    public static bool Slider(string label, ref float value, SliderInputConfigurator cfg)
    {
        label = "##" + label;
        var results = false;
        object obj = cfg.BorderColor;
        var borderColorActive = cfg.BorderColorActive;
        var borderThicknessActive = cfg.BorderThickness;
        var sliderColorActive = cfg.SliderColor;
        float lerpProgress = 0;
        var status = Sliderstatus.Idle;
        var flag = FrameData.TryGetValue(label, out obj);
        if (flag)
        {
            var frameData = (SliderInputFrame)obj;
            borderColorActive = frameData.Color;
            borderThicknessActive = frameData.BorderThickness;
            sliderColorActive = frameData.SliderColorActive;
            lerpProgress = frameData.LerpProgress;
            status = frameData.Status;
        }
        else
        {
            FrameData.Add(label, new SliderInputFrame
            {
                BorderThickness = borderThicknessActive,
                Color = borderColorActive,
                SliderColorActive = sliderColorActive,
                LerpProgress = lerpProgress,
                Status = status
            });
        }

        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        var mousePos = ImGui.GetCursorPos();
        var startDrawBg = new Vector2(windowpos.X + cursorPos.X, windowpos.Y + cursorPos.Y);
        var endDrawBg = new Vector2(windowpos.X + cursorPos.X + cfg.Size.X, windowpos.Y + cursorPos.Y + cfg.Size.Y);
        draw.AddRectFilled(startDrawBg, endDrawBg, cfg.Bgcolor, cfg.RoundCorners);
        draw.AddRectFilled(new Vector2(startDrawBg.X, startDrawBg.Y),
            new Vector2(startDrawBg.X + value * cfg.Size.X, endDrawBg.Y)
            , sliderColorActive, cfg.RoundCorners);
        var windowbgColor = style.Colors[ImGuiCol.WindowBg.ToInt()].ToColor();
        style.FrameBorderSize = 0;
        if (!ImGui.IsMouseDown((int)ImGuiMouseButton.Left) && status == Sliderstatus.Start) status = Sliderstatus.End;
        ImGui.InvisibleButton(label, cfg.Size); //stop dragging
        if (ImGui.IsMouseHoveringRect(startDrawBg, endDrawBg) || status == Sliderstatus.Start)
        {
            if (ImGui.IsMouseDown((int)ImGuiMouseButton.Left) || status == Sliderstatus.Start)
            {
                status = Sliderstatus.Start;
                var drawPos = mousePos.X - startDrawBg.X;
                if (drawPos < 0)
                    drawPos = 0;
                if (drawPos > cfg.Size.X)
                    drawPos = cfg.Size.X;
                value = drawPos / cfg.Size.X;
                results = true;
                draw.AddRectFilled(startDrawBg, endDrawBg, cfg.Bgcolor, cfg.RoundCorners);
                draw.AddRectFilled(new Vector2(startDrawBg.X, startDrawBg.Y),
                    new Vector2(startDrawBg.X + drawPos, endDrawBg.Y), sliderColorActive, cfg.RoundCorners);
                lerpProgress += 0.0025f;
            }
            else
            {
                lerpProgress -= 0.0025f;
            }

            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColorActive.ToColor(), 0.01f).ToUint();
            if (borderThicknessActive <= cfg.BorderThickness + 3)
                borderThicknessActive += 0.01f;
        }
        else
        {
            if (borderThicknessActive > cfg.BorderThickness)
                borderThicknessActive -= 0.01f;
            lerpProgress -= 0.0025f;
            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColor.ToColor(), 0.01f).ToUint();
        }

        if (lerpProgress < 0)
            lerpProgress = 0;
        else if (lerpProgress > 1)
            lerpProgress = 1;
        if (lerpProgress != 0)
        {
            var drawPos = mousePos.X - startDrawBg.X;
            if (drawPos < 0)
                drawPos = 0;
            if (drawPos > cfg.Size.X)
                drawPos = cfg.Size.X;
            var addZero = string.Empty;
            if ((value * 100).ToString().Count() < 4)
                addZero += ".0";
            var text = new string((value * 100).ToString().Take(4).ToArray()) + addZero + "%";
            var textSize = ImGui.CalcTextSize(text);
            var startRect = new Vector2(startDrawBg.X + drawPos - textSize.X / 2,
                startDrawBg.Y - textSize.Y + cfg.YoffsetLabel * lerpProgress);
            var endRect = new Vector2(startDrawBg.X + drawPos + textSize.X / 2,
                startDrawBg.Y + cfg.YoffsetLabel * lerpProgress);
            draw.AddRectFilled(startRect
                , endRect, sliderColorActive, 4);
            draw.AddTriangleFilled(new Vector2(startRect.X + textSize.X / 2 - 8, endRect.Y),
                new Vector2(startRect.X + textSize.X / 2 + 8, endRect.Y),
                new Vector2(startDrawBg.X + drawPos, startDrawBg.Y + cfg.YoffsetLabel * lerpProgress * 0.5f),
                sliderColorActive);
            draw.AddText(
                new Vector2(startDrawBg.X + drawPos - textSize.X / 2,
                    startDrawBg.Y - textSize.Y + cfg.YoffsetLabel * lerpProgress), Color.White.ToUint(), text);
        }

        sliderColorActive = cfg.SliderColor.ToColor().Lerp(cfg.SliderColorActive.ToColor(), lerpProgress).ToUint();
        FrameData[label] = new SliderInputFrame
        {
            BorderThickness = borderThicknessActive,
            Color = borderColorActive,
            SliderColorActive = sliderColorActive,
            Status = status,
            LerpProgress = lerpProgress
        };

        for (float i = 0; i < borderThicknessActive; i += 1f)
        {
            var lerpedToBackground = borderColorActive.ToColor().Lerp(windowbgColor, i / borderThicknessActive);
            draw.AddRect(new Vector2(startDrawBg.X - i, startDrawBg.Y - i),
                new Vector2(endDrawBg.X + i, endDrawBg.Y + i), lerpedToBackground.ToUint(), cfg.RoundCorners);
        }

        return results;
    }

    public static bool GlowingInput(string label, ref string text, GlowingInputConfigurator cfg, uint maxlength = 32)
    {
        label = "##" + label;
        object obj = cfg.BorderColor;
        var borderColorActive = cfg.BorderColorActive;
        var borderThicknessActive = cfg.BorderThickness;
        var flag = FrameData.TryGetValue(label, out obj);
        if (flag)
        {
            borderColorActive = ((GlowingInputFrame)obj).Color;
            borderThicknessActive = ((GlowingInputFrame)obj).BorderThickness;
        }
        else
        {
            FrameData.Add(label, new GlowingInputFrame
            {
                BorderThickness = borderThicknessActive,
                Color = borderColorActive
            });
        }

        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        var startDrawBg = new Vector2(windowpos.X + cursorPos.X, windowpos.Y + cursorPos.Y);
        var endDrawBg = new Vector2(windowpos.X + cursorPos.X + cfg.Size.X, windowpos.Y + cursorPos.Y + cfg.Size.Y);
        draw.AddRectFilled(startDrawBg, endDrawBg, cfg.Bgcolor, cfg.RoundCorners);
        var windowbgColor = style.Colors[ImGuiCol.WindowBg.ToInt()].ToColor();
        var temp = style.Colors[ImGuiCol.FrameBg.ToInt()];
        var temp2 = style.Colors[ImGuiCol.Text.ToInt()];
        var temp3 = ImGui.GetFontSize();
        style.Colors[ImGuiCol.FrameBg.ToInt()] = cfg.Bgcolor.ToColor().ToVec4();
        style.FrameBorderSize = 0;
        style.Colors[ImGuiCol.Text.ToInt()] = cfg.TextColor.ToColor().ToVec4();
        var currentCursor = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPosX() + style.FramePadding.X,
            ImGui.GetCursorPosY() + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(cfg.Prefix).Y / 2.0f
        ));
        ImGui.SetNextItemWidth(cfg.Size.X);
        var valuechanged = ImGui.InputText(label, ref text, maxlength);
        if (cfg.PasswordChar != '\0')
        {
            var hiddenText = string.Empty;
            for (var i = 0; i < text.Length; i++) hiddenText += cfg.PasswordChar;
            draw.AddRectFilled(startDrawBg, endDrawBg, cfg.Bgcolor, cfg.RoundCorners);
            draw.AddText(
                new Vector2(startDrawBg.X + style.FramePadding.X,
                    windowpos.Y + cursorPos.Y + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(hiddenText).Y / 2.0f)
                , cfg.TextColor, hiddenText);
        }

        if (text == string.Empty && !ImGui.IsItemActive())
            draw.AddText(
                new Vector2(startDrawBg.X + style.FramePadding.X,
                    windowpos.Y + cursorPos.Y + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(cfg.Prefix).Y / 2.0f)
                , cfg.TextColor.ToColor().Brightness(0.4f).ToUint(), cfg.Prefix);
        if (ImGui.IsItemActive())
        {
            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColorActive.ToColor(), 0.01f).ToUint();
            if (borderThicknessActive <= cfg.BorderThickness + 3)
                borderThicknessActive += 0.01f;
            
        }
        else
        {
            if (borderThicknessActive > cfg.BorderThickness)
                borderThicknessActive -= 0.01f;
            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColor.ToColor(), 0.01f).ToUint();
        }

        FrameData[label] = new GlowingInputFrame
        {
            BorderThickness = borderThicknessActive,
            Color = borderColorActive
        };

        for (float i = 0; i < borderThicknessActive; i += 1f)
        {
            var lerpedToBackground = borderColorActive.ToColor().Lerp(windowbgColor, i / borderThicknessActive);
            draw.AddRect(new Vector2(startDrawBg.X - i, startDrawBg.Y - i),
                new Vector2(endDrawBg.X + i, endDrawBg.Y + i), lerpedToBackground.ToUint(), cfg.RoundCorners);
        }

        style.Colors[ImGuiCol.FrameBg.ToInt()] = temp;
        style.Colors[ImGuiCol.Text.ToInt()] = temp2;
        return valuechanged;
    }

    public static bool GlowingCombo(string label, ref int selectedIndx, string[] items, GlowingInputConfigurator cfg,
        uint maxlength = 32)
    {
        label = "##" + label;
        object obj = cfg.BorderColor;
        var borderColorActive = cfg.BorderColorActive;
        var borderThicknessActive = cfg.BorderThickness;
        var flag = FrameData.TryGetValue(label, out obj);
        if (flag)
        {
            borderColorActive = ((GlowingInputFrame)obj).Color;
            borderThicknessActive = ((GlowingInputFrame)obj).BorderThickness;
        }
        else
        {
            FrameData.Add(label, new GlowingInputFrame
            {
                BorderThickness = borderThicknessActive,
                Color = borderColorActive
            });
        }

        var windowpos = ImGui.GetWindowPos();
        var style = ImGui.GetStyle();
        var draw = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorPos();
        var startDrawBg = new Vector2(windowpos.X + cursorPos.X, windowpos.Y + cursorPos.Y);
        var endDrawBg = new Vector2(windowpos.X + cursorPos.X + cfg.Size.X, windowpos.Y + cursorPos.Y + cfg.Size.Y);
        draw.AddRectFilled(startDrawBg, endDrawBg, cfg.Bgcolor, cfg.RoundCorners);
        var windowbgColor = style.Colors[ImGuiCol.WindowBg.ToInt()].ToColor();
        var temp = style.Colors[ImGuiCol.FrameBg.ToInt()];
        var temp2 = style.Colors[ImGuiCol.Text.ToInt()];
        var temp3 = ImGui.GetFontSize();
        style.Colors[ImGuiCol.FrameBg.ToInt()] = cfg.Bgcolor.ToColor().ToVec4();
        style.FrameBorderSize = 0;
        style.Colors[ImGuiCol.Text.ToInt()] = cfg.TextColor.ToColor().ToVec4();
        style.Colors[ImGuiCol.FrameBgHovered.ToInt()] = cfg.Bgcolor.ToColor().ToVec4();
        style.Colors[ImGuiCol.Button.ToInt()] = cfg.Bgcolor.ToColor().ToVec4();
        style.Colors[ImGuiCol.ButtonHovered.ToInt()] = cfg.Bgcolor.ToColor().ToVec4();
        var currentCursor = ImGui.GetCursorPos();
        ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPosX() + style.FramePadding.X,
            ImGui.GetCursorPosY() + cfg.Size.Y / 2.0f - ImGui.CalcTextSize(cfg.Prefix).Y / 2.0f
        ));
        ImGui.SetNextItemWidth(cfg.Size.X - 3 * _scaleSize.X);
        var results = ImGui.Combo(label, ref selectedIndx, items, items.Length);
        if (ImGui.IsItemActive())
        {
            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColorActive.ToColor(), 0.01f).ToUint();
            if (borderThicknessActive <= cfg.BorderThickness + 3)
                borderThicknessActive += 0.01f;
        }
        else
        {
            if (borderThicknessActive > cfg.BorderThickness)
                borderThicknessActive -= 0.01f;
            borderColorActive = borderColorActive.ToColor().Lerp(cfg.BorderColor.ToColor(), 0.01f).ToUint();
        }

        FrameData[label] = new GlowingInputFrame
        {
            BorderThickness = borderThicknessActive,
            Color = borderColorActive
        };

        for (float i = 0; i < borderThicknessActive; i += 1f)
        {
            var lerpedToBackground = borderColorActive.ToColor().Lerp(windowbgColor, i / borderThicknessActive);
            draw.AddRect(new Vector2(startDrawBg.X - i, startDrawBg.Y - i),
                new Vector2(endDrawBg.X + i, endDrawBg.Y + i), lerpedToBackground.ToUint(), cfg.RoundCorners);
        }

        style.Colors[ImGuiCol.FrameBg.ToInt()] = temp;
        style.Colors[ImGuiCol.Text.ToInt()] = temp2;
        return results;
    }

    public static void CircleProgressBarAnimated(string label, Vector2 pos, float radius, uint color, float tickness,
        float speed, float progress)
    {
        var windowpos = ImGui.GetWindowPos();
        pos = new Vector2(windowpos.X + pos.X, windowpos.Y + pos.Y);
        object obj = 0.0f;
        var flag = FrameData.TryGetValue(label, out obj);
        var draw = ImGui.GetWindowDrawList();
        float angle;
        if (!flag)
        {
            angle = 0;
            FrameData.Add(label, angle);
        }
        else
        {
            angle = (float)obj;
        }

        var style = ImGui.GetStyle();
        var bg = style.Colors[ImGuiCol.WindowBg.ToInt()].ToColor();

        angle += speed;
        var lim = 2.0f * (float)Math.PI * progress;
        for (float i = 0; i < lim; i += 0.01f)
        {
            var unDiscover = new Vector2(radius * (float)Math.Sin(angle + i), radius * (float)Math.Cos(angle + i));
            var steppedColor = bg.Lerp(color.ToColor(), i / lim);
            draw.AddLine(new Vector2(pos.X + unDiscover.X - 1, pos.Y + unDiscover.Y - 1)
                , new Vector2(pos.X + unDiscover.X, pos.Y + unDiscover.Y), steppedColor.ToUint(), tickness);
        }

        if (angle >= 2 * Math.PI) angle = 0;
        FrameData[label] = angle;
    }

    public static void NewLine()
    {
        ImGui.Dummy(new Vector2(0, 45.0f * _scaleSize.Y)); // Adds 20px vertical space
    }

    public static void SameLine()
    {
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 320 * _scaleSize.X);
    }

    public static GlowingInputConfigurator GetTextTheme()
    {
        var textboxTheme = new GlowingInputConfigurator();
        textboxTheme.Size = new Vector2(320 * _scaleSize.X, 45f * _scaleSize.Y);
        textboxTheme.RoundCorners = 5;
        textboxTheme.Prefix = "Username";
        textboxTheme.BorderThickness = 3f;
        textboxTheme.Bgcolor = Color.FromArgb(28, 28, 32).ToUint();
        textboxTheme.BorderColor = Color.FromArgb(88, 37, 227).ToUint();
        textboxTheme.BorderColorActive = Color.FromArgb(115, 70, 232).ToUint();
        textboxTheme.TextColor = Color.White.ToUint();
        textboxTheme.FontScale = 16f;
        return textboxTheme;
    }

    public static SliderInputConfigurator GetSliderTheme()
    {
        var textboxTheme = new SliderInputConfigurator();
        textboxTheme.Size = new Vector2(320 * _scaleSize.X, 45f * _scaleSize.Y);
        textboxTheme.RoundCorners = 2;
        textboxTheme.BorderThickness = 3f;
        textboxTheme.Bgcolor = Color.FromArgb(28, 28, 32).ToUint();
        textboxTheme.BorderColor = Color.FromArgb(88, 37, 227).ToUint();
        textboxTheme.BorderColorActive = Color.FromArgb(115, 70, 232).ToUint();
        textboxTheme.SliderColor = Color.FromArgb(82, 34, 204).ToUint();
        textboxTheme.SliderColorActive = Color.FromArgb(119, 73, 226).ToUint();
        textboxTheme.YoffsetLabel = -20;
        return textboxTheme;
    }

    public static ButtonConfigurator GetTextButtonTheme()
    {
        var textboxTheme = new ButtonConfigurator();
        textboxTheme.Size = new Vector2(320 * _scaleSize.X, 45f * _scaleSize.Y);
        textboxTheme.RoundCorners = 5;
        textboxTheme.Text = "NULL";
        textboxTheme.Bgcolor = Color.FromArgb(100, 100, 100).ToUint();
        textboxTheme.ColorHover = Color.FromArgb(222, 222, 222).ToUint();
        textboxTheme.TextColor = Color.White.ToUint();
        textboxTheme.WaitSpeed = 0.00025f;
        textboxTheme.SlideSpeed = 0.0025f;
        textboxTheme.CircleThickness = 4;
        textboxTheme.CircleRadius = 20;
        textboxTheme.CircleColor = Color.FromArgb(82, 34, 204).ToUint();
        textboxTheme.CircleSpeed = 0.005f;
        textboxTheme.CirclePositionY = -90;
        return textboxTheme;
    }

    public static ButtonConfigurator GetbuttonTheme()
    {
        var textboxTheme = new ButtonConfigurator();
        textboxTheme.Size = new Vector2(320 * _scaleSize.X, 45f * _scaleSize.Y);
        textboxTheme.RoundCorners = 5;
        textboxTheme.Text = "NULL";
        textboxTheme.Bgcolor = Color.FromArgb(91, 36, 221).ToUint();
        textboxTheme.ColorHover = Color.FromArgb(114, 71, 224).ToUint();
        textboxTheme.TextColor = Color.White.ToUint();
        textboxTheme.WaitSpeed = 0.00025f;
        textboxTheme.SlideSpeed = 0.0025f;
        textboxTheme.CircleThickness = 4;
        textboxTheme.CircleRadius = 20;
        textboxTheme.CircleColor = Color.FromArgb(82, 34, 204).ToUint();
        textboxTheme.CircleSpeed = 0.005f;
        textboxTheme.CirclePositionY = -90;
        return textboxTheme;
    }

    private struct ColFrame
    {
        public ImGuiCol Type;
        public Color Start;
        public Color End;
        public float Speed;
        public float Progress;
    }

    private struct GlowingInputFrame
    {
        public uint Color;
        public float BorderThickness;
    }

    private struct SliderInputFrame
    {
        public uint Color;
        public float BorderThickness;
        public float LerpProgress;
        public uint SliderColorActive;
        public Sliderstatus Status;
    }

    private struct CircleButtonFrame
    {
        public float Current;
        public float WaitFade;
        public float HoverFade;
        public uint Color;
        public CircleState Status;
    }

    public struct GlowingInputConfigurator
    {
        public uint Bgcolor;
        public uint BorderColorActive;
        public uint BorderColor;
        public uint TextColor;
        public float RoundCorners;
        public float BorderThickness;
        public Vector2 Size;
        public string Prefix;
        public float FontScale;
        public char PasswordChar;
    }

    public struct SliderInputConfigurator
    {
        public uint Bgcolor;
        public uint BorderColorActive;
        public uint BorderColor;
        public uint SliderColor;
        public float RoundCorners;
        public float BorderThickness;
        public Vector2 Size;
        public uint SliderColorActive;
        public float YoffsetLabel;
    }

    public struct ButtonConfigurator
    {
        public uint Bgcolor;
        public uint ColorHover;
        public uint TextColor;
        public float RoundCorners;
        public Vector2 Size;
        public string Text;
        public float CircleRadius;
        public float CircleSpeed;
        public uint CircleColor;
        public uint CircleThickness;
        public float SlideSpeed;
        public float WaitSpeed;
        public float CirclePositionY;
    }
}