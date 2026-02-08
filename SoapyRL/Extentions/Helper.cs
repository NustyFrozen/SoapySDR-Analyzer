using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace SoapyRL.Extentions;



public static class ColorExtention
{
    public static int ToInt(this ImGuiCol col)
    {
        return (int)col;
    }

    public static Vector4 ToVec4(this Color clr)
    {
        return new Vector4(clr.R / 255.0f, clr.G / 255.0f, clr.B / 255.0f, clr.A / 255.0f);
    }

    public static Color ToColor(this Vector4 vec)
    {
        return Color.FromArgb((int)(vec.W * 255.0f), (int)(vec.X * 255.0f), (int)(vec.Y * 255.0), (int)(vec.Z * 255.0));
    }

    public static Color ToColor(this uint col)
    {
        var a = (byte)(col >> 24);
        var r = (byte)(col >> 16);
        var g = (byte)(col >> 8);
        var b = (byte)(col >> 0);
        a = (byte)(float)a;
        return Color.FromArgb(a, b, g, r);
    }

    public static int Lerp(this int x, int final, double progress)
    {
        return Convert.ToInt16((1 - progress) * x + final * progress);
    }

    public static float Lerp(this float x, float final, double progress)
    {
        return (float)((1 - progress) * x + final * progress);
    }

    public static Color Brightness(this Color a, float t) //linear interpolation
    {
        return Color.FromArgb(Convert.ToInt32(a.R * t), Convert.ToInt32(a.G * t), Convert.ToInt32(a.B * t));
    }

    public static Color Lerp(this Color a, Color b, double t) //linear interpolation
    {
        var r = (1 - t) * a.R + b.R * t;
        var g = (1 - t) * a.G + b.G * t;
        var bb = (1 - t) * a.B + b.B * t;
        return Color.FromArgb((int)255.0f, Convert.ToInt32(r), Convert.ToInt32(g), Convert.ToInt32(bb));
    }

    public static Color Rainbow(this Color clr, float progress)
    {
        var div = Math.Abs(progress % 1) * 6;
        var ascending = (int)(div % 1 * 255);
        var descending = 255 - ascending;

        switch ((int)div)
        {
            case 0:
                return Color.FromArgb(255, 255, ascending, 0);

            case 1:
                return Color.FromArgb(255, descending, 255, 0);

            case 2:
                return Color.FromArgb(255, 0, 255, ascending);

            case 3:
                return Color.FromArgb(255, 0, descending, 255);

            case 4:
                return Color.FromArgb(255, ascending, 0, 255);

            default: // case 5:
                return Color.FromArgb(255, 255, 0, descending);
        }
    }

    public static uint ToUint(this Color c)
    {
        return (uint)(((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R) & 0xffffffffL);
    }
}

public static class StringExtention
{
    public static string TruncateLongString(this string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return str.Substring(0, Math.Min(str.Length, maxLength));
    }
}

public static class DbConverterHelper
{
    public static double ToDBm(this double mW)
    {
        var big = 10.0 * Math.Log10(mW);
        return big;
    }

    public static double ToMw(this double dB)
    {
        var mehane = dB / 10.0;
        var value = Math.Pow(10, mehane);
        return value;
    }
}

public static class CollectionHelper
{
    public static void AddRangeOverride<TKey, TValue>(this IDictionary<TKey, TValue> dic,
        IDictionary<TKey, TValue> dicToAdd)
    {
        try
        {
            if (dicToAdd is null) return;
            dicToAdd.ForEach(x => dic[x.Key] = x.Value);
        }
        catch (Exception ex)
        {
            //something the given dict is null when changing some values with samples,fft,etc...
        }
    }

    public static void AddRangeNewOnly<TKey, TValue>(this IDictionary<TKey, TValue> dic,
        IDictionary<TKey, TValue> dicToAdd)
    {
        dicToAdd.ForEach(x =>
        {
            if (!dic.ContainsKey(x.Key)) dic.Add(x.Key, x.Value);
        });
    }

    public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd)
    {
        dicToAdd.ForEach(x => dic.Add(x.Key, x.Value));
    }

    public static bool ContainsKeys<TKey, TValue>(this IDictionary<TKey, TValue> dic, IEnumerable<TKey> keys)
    {
        var result = false;
        keys.ForEachOrBreak(x =>
        {
            result = dic.ContainsKey(x);
            return result;
        });
        return result;
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
            action(item);
    }

    public static void ForEachOrBreak<T>(this IEnumerable<T> source, Func<T, bool> func)
    {
        foreach (var item in source)
        {
            var result = func(item);
            if (result) break;
        }
    }
}