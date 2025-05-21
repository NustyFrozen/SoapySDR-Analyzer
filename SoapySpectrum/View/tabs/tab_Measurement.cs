using ImGuiNET;
using SoapyVNACommon;

namespace SoapySA.View.tabs
{
    public class tab_Measurement(MainWindow initiator)
    {
        private MainWindow parent = initiator;
        private static string[] _availableMeasurements = { "None", "Channel Power", "Filter Bandwidth", "Adjacent Channel Power" };
        public measurementMode s_selectedMeasurementMode = measurementMode.none;
        public int s_selectedPage = 0;

        public enum measurementMode
        {
            none,
            channelPower,
            filterBW,
            ACP,
        }

        public void renderMeasurements()
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
                ImGui.SetCursorPosY(parent.Configuration.optionSize.Y / 2.0f -
                           (_availableMeasurements.Length - 1) * Theme.buttonTheme.size.Y / 2.0f);
                for (var i = 0; i < _availableMeasurements.Length; i++)
                {
                    Theme.buttonTheme.text = $"{_availableMeasurements[i]}";
                    if (Theme.button(_availableMeasurements[i], Theme.buttonTheme))
                    {
                        s_selectedMeasurementMode = (measurementMode)i;
                        if (s_selectedMeasurementMode == measurementMode.channelPower)
                        {
                            var start = (double)parent.Configuration.config[Configuration.saVar.freqStart];
                            var stop = (double)parent.Configuration.config[Configuration.saVar.freqStop];
                            var center = (stop - start) / 2.0 + start;
                            start = center - parent.tab_Device.deviceCOM.rxSampleRate / 2.0;
                            stop = center + parent.tab_Device.deviceCOM.rxSampleRate / 2.0;
                            parent.Configuration.config[Configuration.saVar.freqStart] = start;
                            parent.Configuration.config[Configuration.saVar.freqStop] = stop;
                            parent.fftManager.resetIQFilter();
                        }
                        s_selectedPage = i;
                    }
                    Theme.newLine();
                }
            }
            Theme.newLine();
            switch (s_selectedPage)
            {
                case 1:
                    parent.channelPower.renderChannelPowerSettings();
                    break;
            }
        }
    }
}