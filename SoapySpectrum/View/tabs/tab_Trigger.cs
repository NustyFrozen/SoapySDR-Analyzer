namespace SoapySA.View.tabs;

internal class tab_Trigger
{
    public static void renderTrigger()
    {
        Theme.button("Add");
    }

    private struct trigger
    {
        public double freqstart, freqstop, mindB;
    }
}