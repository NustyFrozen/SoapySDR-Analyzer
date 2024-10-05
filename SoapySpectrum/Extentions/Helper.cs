
namespace SoapySpectrum.Extentions
{
    using ImGuiNET;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Numerics;
    using Color = System.Drawing.Color;

    namespace Design_imGUINET
    {
        public static class ColorExtention
        {
            public static int toInt(this ImGuiCol col)
            {
                return (int)col;

            }
            public static Vector4 toVec4(this System.Drawing.Color clr)
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
                a = (byte)((float)a);
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
            public static Color Brightness(this Color A, float t) //linear interpolation
            {


                return Color.FromArgb(Convert.ToInt32(A.R * t), Convert.ToInt32(A.G * t), Convert.ToInt32(A.B * t));
            }
            public static Color lerp(this Color A, Color B, double t) //linear interpolation
            {

                double R = (1 - t) * A.R + B.R * t;
                double G = (1 - t) * A.G + B.G * t;
                double BB = (1 - t) * A.B + B.B * t;
                return Color.FromArgb((int)(255.0f), Convert.ToInt32(R), Convert.ToInt32(G), Convert.ToInt32(BB));
            }
            public static Color Rainbow(this System.Drawing.Color clr, float progress)
            {
                float div = (System.Math.Abs(progress % 1) * 6);
                int ascending = (int)((div % 1) * 255);
                int descending = 255 - ascending;

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

            public static uint ToUint(this System.Drawing.Color c)
            {

                return (uint)(((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R) & 0xffffffffL);
            }
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

        public static decimal toDBm(this decimal mW)
        {
            var big = 10 * (decimal)DecimalMath.DecimalEx.Log10(mW);
            return big;
        }
        public static decimal toMW(this decimal dB)
        {
            var mehane =  (dB / (decimal)10.0);
            var value = (DecimalMath.DecimalEx.Pow(10,mehane));
            return value;
        }
    }
    public static class CollectionHelper
    {
        public static void AddRangeOverride<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd)
        {
            dicToAdd.ForEach(x => dic[x.Key] = x.Value);
        }

        public static void AddRangeNewOnly<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd)
        {
            dicToAdd.ForEach(x => { if (!dic.ContainsKey(x.Key)) dic.Add(x.Key, x.Value); });
        }

        public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd)
        {
            dicToAdd.ForEach(x => dic.Add(x.Key, x.Value));
        }

        public static bool ContainsKeys<TKey, TValue>(this IDictionary<TKey, TValue> dic, IEnumerable<TKey> keys)
        {
            bool result = false;
            keys.ForEachOrBreak((x) => { result = dic.ContainsKey(x); return result; });
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
                bool result = func(item);
                if (result) break;
            }
        }
    }
}
