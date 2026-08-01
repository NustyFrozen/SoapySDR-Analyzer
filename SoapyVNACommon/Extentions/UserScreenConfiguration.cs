using System;
using System.Numerics;
using ImGuiNET;
using SoapyVNACommon;

namespace SoapySA.Extentions
{
    /// <summary>
    /// Central, resolution independent layout helper.
    ///
    /// Every dimension in the GUI is expressed as a <b>percentage of the screen</b> instead of an
    /// absolute pixel count. The percentages were converted from the original 1920x1080 design
    /// (pixels / design resolution), so a 1080p screen renders exactly like before while every
    /// other resolution scales proportionally.
    ///
    /// Use <see cref="Percent"/>/<see cref="PercentX"/>/<see cref="PercentY"/> when a value is
    /// naturally a fraction of the screen, and <see cref="ScaleX"/>/<see cref="ScaleY"/>/
    /// <see cref="ScaleUniform"/> to convert a legacy 1920x1080 pixel value.
    /// </summary>
    public static class UserScreenConfiguration
    {
        /// <summary>Resolution the original pixel values were authored against.</summary>
        public const float DesignWidth = 1920.0f;

        /// <summary>Resolution the original pixel values were authored against.</summary>
        public const float DesignHeight = 1080.0f;

        #region Layout percentages (0..1 fraction of the screen)

        /// <summary>Plot canvas: 80% x 90% of the screen.</summary>
        public const float GraphWidthPct = 0.80f, GraphHeightPct = 0.90f;

        /// <summary>Side options panel: 20% x 100% of the screen.</summary>
        public const float OptionWidthPct = 0.20f, OptionHeightPct = 1.00f;

        /// <summary>Padding around the plot canvas (was 50px x 10px @1080p).</summary>
        public const float MarginXPct = 50.0f / DesignWidth, MarginYPct = 10.0f / DesignHeight;

        /// <summary>Gap between the plot canvas and the options panel (was 60px x 30px @1080p).</summary>
        public const float OptionGapXPct = 60.0f / DesignWidth, OptionGapYPct = 30.0f / DesignHeight;

        /// <summary>Generic small padding used between drawn text/elements (was 5px @1080p).</summary>
        public const float PaddingPct = 5.0f / DesignHeight;

        #endregion Layout percentages (0..1 fraction of the screen)

        #region Font

        /// <summary>Font size the atlas was authored at, in design pixels.</summary>
        public const float BaseFontSizePx = 16.0f;

        /// <summary>Design time global font multiplier (used to be ImGui's FontGlobalScale).</summary>
        public const float FontBoost = 1.4f;

        private static float _fontAtlasUniformScale = 1.0f;

        /// <summary>Pixel size the font atlas should be rasterised at for the current resolution.</summary>
        public static float FontSizePx => MathF.Max(8.0f, MathF.Round(BaseFontSizePx * FontBoost * UniformScale));

        /// <summary>
        /// Residual multiplier ImGui has to apply on top of the rasterised atlas. It is 1.0 while the
        /// window matches the resolution the atlas was built for, and compensates proportionally if the
        /// window is resized afterwards (the atlas itself cannot be rebuilt mid-flight).
        /// </summary>
        public static float FontGlobalScale =>
            _fontAtlasUniformScale <= 0.0001f ? 1.0f : UniformScale / _fontAtlasUniformScale;

        /// <summary>Call right after the font atlas has been rasterised at <see cref="FontSizePx"/>.</summary>
        public static void OnFontAtlasBuilt()
        {
            _fontAtlasUniformScale = UniformScale;
            ApplyFontScale();
        }

        #endregion Font

        public static ImGuiWindowFlags MainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar |
                                                         ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;

        /// <summary>Physical resolution of the monitor the application is running on.</summary>
        public static Vector2 ScreenSize = new(DesignWidth, DesignHeight);

        /// <summary>
        /// Size of the application window. All percentages resolve against this, because it is the
        /// coordinate space ImGui draws in. The window is opened at the monitor resolution, so it
        /// normally equals <see cref="ScreenSize"/>.
        /// </summary>
        public static Vector2 windowSize = new(DesignWidth, DesignHeight);

        /// <summary>Per axis scale relative to the 1920x1080 design.</summary>
        public static Vector2 ScaleSize = Vector2.One;

        /// <summary>
        /// Smallest of the two axis scales. Use it for anything that must stay square or keep its
        /// aspect (circle radii, line thickness, font size) so a wide screen does not stretch it.
        /// </summary>
        public static float UniformScale = 1.0f;

        public static Vector2 PositionOffset = new(MarginXPct * DesignWidth, MarginYPct * DesignHeight),
            GraphSize = new(GraphWidthPct * DesignWidth, GraphHeightPct * DesignHeight),
            OptionSize = new(OptionWidthPct * DesignWidth, OptionHeightPct * DesignHeight);

        static UserScreenConfiguration()
        {
            Recalculate();
        }

        public static Vector2 GetDefaultScaleSize()
        {
            return windowSize / new Vector2(DesignWidth, DesignHeight);
        }

        #region Percentage helpers

        /// <summary>Horizontal size as a fraction (0..1) of the screen width.</summary>
        public static float PercentX(float percent)
        {
            return windowSize.X * percent;
        }

        /// <summary>Vertical size as a fraction (0..1) of the screen height.</summary>
        public static float PercentY(float percent)
        {
            return windowSize.Y * percent;
        }

        /// <summary>Size as a fraction (0..1) of the screen on both axes.</summary>
        public static Vector2 Percent(float percentX, float percentY)
        {
            return new Vector2(PercentX(percentX), PercentY(percentY));
        }

        /// <summary>
        /// Aspect preserving size as a fraction (0..1) of the screen's shortest side, for radii,
        /// thicknesses and other values that must not be stretched.
        /// </summary>
        public static float PercentUniform(float percent)
        {
            return MathF.Min(windowSize.X, windowSize.Y) * percent;
        }

        #endregion Percentage helpers

        #region Legacy pixel -> percentage conversion

        /// <summary>Converts a horizontal 1920x1080 design pixel value into the current resolution.</summary>
        public static float ScaleX(float designPixels)
        {
            return PercentX(designPixels / DesignWidth);
        }

        /// <summary>Converts a vertical 1920x1080 design pixel value into the current resolution.</summary>
        public static float ScaleY(float designPixels)
        {
            return PercentY(designPixels / DesignHeight);
        }

        /// <summary>Converts a 1920x1080 design pixel size into the current resolution.</summary>
        public static Vector2 Scale(float designPixelsX, float designPixelsY)
        {
            return new Vector2(ScaleX(designPixelsX), ScaleY(designPixelsY));
        }

        /// <summary>
        /// Converts a 1920x1080 design pixel value into the current resolution without stretching it,
        /// for circle radii, line thickness, rounding and similar.
        /// </summary>
        public static float ScaleUniform(float designPixels)
        {
            return PercentUniform(designPixels / DesignHeight);
        }

        #endregion Legacy pixel -> percentage conversion

        /// <summary>Records the physical monitor resolution the window is being opened on.</summary>
        public static void UpdateScreenSize(Vector2 newScreenSize)
        {
            if (newScreenSize.X < 1 || newScreenSize.Y < 1)
                return;
            ScreenSize = newScreenSize;
        }

        /// <summary>
        /// Re-derives every cached GUI dimension from the new window size. Safe to call before the
        /// ImGui context exists (font/style updates are skipped until then).
        /// </summary>
        public static void UpdateWindowSize(Vector2 newSize)
        {
            //a minimised window reports 0 and would collapse the whole layout
            if (newSize.X < 1 || newSize.Y < 1)
                return;

            windowSize = newSize;
            Recalculate();

            Theme.Refresh();
            ApplyFontScale();
        }

        private static void Recalculate()
        {
            ScaleSize = GetDefaultScaleSize();
            UniformScale = MathF.Min(ScaleSize.X, ScaleSize.Y);
            PositionOffset = Percent(MarginXPct, MarginYPct);
            GraphSize = Round(Percent(GraphWidthPct, GraphHeightPct));
            OptionSize = Round(Percent(OptionWidthPct, OptionHeightPct));
        }

        /// <summary>Child window sizes are kept on whole pixels so plot lines stay crisp.</summary>
        private static Vector2 Round(Vector2 value)
        {
            return new Vector2(MathF.Round(value.X), MathF.Round(value.Y));
        }

        private static void ApplyFontScale()
        {
            if (ImGui.GetCurrentContext() == IntPtr.Zero)
                return;
            ImGui.GetIO().FontGlobalScale = FontGlobalScale;
        }
    }
}
