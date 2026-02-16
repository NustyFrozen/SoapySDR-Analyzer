using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SoapySA.Extentions
{
    public static class UserScreenConfiguration
    {
        public static ImGuiWindowFlags MainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar |
                                                    ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;
        public static Vector2 windowSize;
        public static Vector2 GetDefaultScaleSize()
        {
            return windowSize / new Vector2(1920.0f, 1080.0f);
        }
        public static Vector2
            ScaleSize,
            PositionOffset,
            GraphSize,
            OptionSize;
        public static void UpdateWindowSize(Vector2 newSize)
        {
            windowSize = newSize;
            ScaleSize = GetDefaultScaleSize();
            PositionOffset = new Vector2(50 * ScaleSize.X, 10 * ScaleSize.Y);
            GraphSize = new Vector2(Convert.ToInt16(windowSize.X * .8), Convert.ToInt16(windowSize.Y * .9));
            OptionSize = new Vector2(Convert.ToInt16(windowSize.X * .2), Convert.ToInt16(windowSize.Y));
        }
        
    }
}
