using ImGuiNET;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using System.Drawing;
using System.Numerics;

namespace SoapySA.View;

public partial class GraphPlotManager
{
    /// <summary>Breathing room from the graph's corner, converted from the original 1920x1080 design.</summary>
    private const float WarningInsetPct = 5.0f / UserScreenConfiguration.DesignHeight;

    /// <summary>
    ///     Labels the top left of the graph when the trace runs off the display, so a trace pinned to an
    ///     edge reads as a wrong reference level rather than as a flat signal.
    /// </summary>
    public static void DrawRefLevelWarning(ImDrawListPtr draw, Vector2 topLeft, RefLevelFit fit)
    {
        if (fit == RefLevelFit.Inside)
            return;

        // bins above the top are the ones actually being clipped, so they win when both edges are hit
        var text = fit.HasFlag(RefLevelFit.AboveTop) ? "REF LEVEL TOO LOW" : "REF LEVEL TOO HIGH";
        var inset = UserScreenConfiguration.PercentUniform(WarningInsetPct);

        draw.AddText(new Vector2(topLeft.X + inset, topLeft.Y + inset), Color.Orange.ToUint(), text);
    }
}
