namespace SoapyVNACommon
{
    public interface Widget
    {
        void renderWidget();

        void releaseSDR();

        void handleSDR();

        void initWidget();
    }
}