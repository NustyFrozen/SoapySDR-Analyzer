using SoapySpectrum.UI;

namespace SoapySpectrum.View.tabs
{
    internal class tab_Trigger
    {
        struct trigger
        {
            public double freqstart, freqstop, mindB;
        }
        public static void renderTrigger()
        {
            var inputTheme = Theme.getTextTheme();
            var buttonTheme = Theme.getButtonTheme();
            Theme.button("Add");

        }
    }
}
