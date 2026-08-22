using System.Drawing;
using SoapyVNACommon.Extentions;

namespace SoapyRTSA.Extentions;

/// <summary>
///     The density colour ramp, built once. Rare magnitudes come out blue and the most frequent ones red,
///     with alpha rising alongside so a hit that stops being refreshed fades out as its count decays
///     rather than snapping off the screen.
/// </summary>
public static class Palette
{
    public const int Steps = 256;

    /// <summary>Stops of the ramp, walked with a straight lerp between neighbours.</summary>
    private static readonly Color[] Stops =
    {
        Color.FromArgb(0, 0, 24), //empty
        Color.FromArgb(0, 0, 180), //rare
        Color.FromArgb(0, 200, 220),
        Color.FromArgb(0, 220, 0),
        Color.FromArgb(255, 235, 0),
        Color.FromArgb(255, 40, 0) //most frequent
    };

    private static readonly uint[] Density = Build(true);
    private static readonly uint[] Solid = Build(false);

    /// <summary>Colour for a density in 0..1, transparent at the bottom of the ramp.</summary>
    public static uint Fade(int step) => Density[step < 0 ? 0 : step >= Steps ? Steps - 1 : step];

    /// <summary>Colour for a density in 0..1 at full alpha, used by the waterfall.</summary>
    public static uint Opaque(int step) => Solid[step < 0 ? 0 : step >= Steps ? Steps - 1 : step];

    /// <summary>Quantises a 0..1 intensity onto the ramp.</summary>
    public static int Step(float intensity) =>
        intensity <= 0.0f ? 0 : intensity >= 1.0f ? Steps - 1 : (int)(intensity * (Steps - 1));

    private static uint[] Build(bool withAlpha)
    {
        var ramp = new uint[Steps];
        var segments = Stops.Length - 1;

        for (var i = 0; i < Steps; i++)
        {
            var position = i / (float)(Steps - 1) * segments;
            var index = (int)position;
            if (index >= segments)
                index = segments - 1;

            var t = position - index;
            var from = Stops[index];
            var to = Stops[index + 1];

            var red = (int)(from.R + (to.R - from.R) * t);
            var green = (int)(from.G + (to.G - from.G) * t);
            var blue = (int)(from.B + (to.B - from.B) * t);

            //alpha tracks the whole ramp, not just its bottom: a hit that is no longer being refreshed
            //has to cool through red and yellow and keep getting more transparent the whole way out
            var intensity = i / (float)(Steps - 1);
            var alpha = withAlpha ? (int)Math.Min(255.0f, 25.0f + intensity * 230.0f) : 255;

            ramp[i] = Color.FromArgb(alpha, red, green, blue).ToUint();
        }

        return ramp;
    }
}
