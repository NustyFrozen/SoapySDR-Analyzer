using ImGuiNET;
using SoapySA.View.measurements;

namespace SoapySA.View.tabs
{
    internal class tab_Measurement
    {
        private static string[] _availableMeasurements = { "None", "Channel Power", "Filter Bandwidth", "Adjacent Channel Power" };
        public static measurementMode s_selectedMeasurementMode = measurementMode.none;
        public static int s_selectedPage = 0;

        public enum measurementMode
        {
            none,
            channelPower,
            filterBW,
            ACP,
        }

        public static void renderMeasurements()
        {
            if (s_selectedPage != 0)
            {
                Theme.buttonTheme.text = $"Return to Measure options";
                if (Theme.button("MeasurementReturn", Theme.buttonTheme))
                {
                    s_selectedPage = 0;
                }
            }
            else
            {
                ImGui.SetCursorPosY(Configuration.optionSize.Y / 2.0f -
                           (_availableMeasurements.Length - 1) * Theme.buttonTheme.size.Y / 2.0f);
                for (var i = 0; i < _availableMeasurements.Length; i++)
                {
                    Theme.buttonTheme.text = $"{_availableMeasurements[i]}";
                    if (Theme.button(_availableMeasurements[i], Theme.buttonTheme))
                    {
                        s_selectedMeasurementMode = (measurementMode)i;
                        s_selectedPage = i;
                    }
                    Theme.newLine();
                }
            }
            Theme.newLine();
            switch (s_selectedPage)
            {
                case 1:
                    ChannelPower.renderChannelPowerSettings();
                    break;
            }
        }
    }
}