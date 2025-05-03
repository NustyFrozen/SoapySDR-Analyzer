using ImGuiNET;
using System.Numerics;

namespace SoapySA.Extentions;

using Color = Color;

public static class ColorExtention
{
    public static int toInt(this ImGuiCol col)
    {
        return (int)col;
    }

    public static Vector4 toVec4(this Color clr)
    {
        return new Vector4(clr.R / 255.0f, clr.G / 255.0f, clr.B / 255.0f, clr.A / 255.0f);
    }

    public static Color toColor(this Vector4 vec)
    {
        return Color.FromArgb((int)(vec.W * 255.0f), (int)(vec.X * 255.0f), (int)(vec.Y * 255.0), (int)(vec.Z * 255.0));
    }

    public static Color toColor(this uint col)
    {
        var a = (byte)(col >> 24);
        var r = (byte)(col >> 16);
        var g = (byte)(col >> 8);
        var b = (byte)(col >> 0);
        a = (byte)(float)a;
        return Color.FromArgb(a, b, g, r);
    }

    public static int lerp(this int x, int final, double progress)
    {
        return Convert.ToInt16((1 - progress) * x + final * progress);
    }

    public static float lerp(this float x, float final, double progress)
    {
        return (float)((1 - progress) * x + final * progress);
    }

    public static Color brightness(this Color A, float t) //linear interpolation
    {
        return Color.FromArgb(Convert.ToInt32(A.R * t), Convert.ToInt32(A.G * t), Convert.ToInt32(A.B * t));
    }

    public static Color lerp(this Color A, Color B, double t) //linear interpolation
    {
        var R = (1 - t) * A.R + B.R * t;
        var G = (1 - t) * A.G + B.G * t;
        var BB = (1 - t) * A.B + B.B * t;
        return Color.FromArgb((int)255.0f, Convert.ToInt32(R), Convert.ToInt32(G), Convert.ToInt32(BB));
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

public static class dbConverterHelper
{
    public static double toDBm(this double mW)
    {
        var big = 10.0 * Math.Log10(mW);
        return big;
    }

    public static double toMW(this double dB)
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