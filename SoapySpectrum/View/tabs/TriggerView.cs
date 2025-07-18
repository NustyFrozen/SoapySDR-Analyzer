using SoapyVNACommon;

namespace SoapySA.View.tabs;

internal class TriggerView
{
    public static void RenderTrigger()
    {
        Theme.Button("Add");
    }

    private struct Trigger
    {
        public double Freqstart, Freqstop, MindB;
    }
}