using Pothosware.SoapySDR;
using System.Numerics;

namespace SoapySpectrum
{
    public static class PerformFFT
    {
        public static bool isRunning = false;
        static RxStream stream;
        public static void beginFFT()
        {
            var device = UI.UI.sdr_device;

        }
        static void IQSampler(Device sdr)
        {
            stream = sdr.SetupRxStream("CF32", new uint[] { 0 }, "wire=CF32"); ;
            stream.Activate(StreamFlags.None);
            var results = new StreamResult();
            ErrorCode received = ErrorCode.Overflow;
            float[] rawSamples = new float[stream.MTU * 2];
            while (true)
            {
                int numSamples = (int)Configuration.config["FFTSize"];
                Complex[] iqBuffer = new Complex[numSamples];

                while (received == ErrorCode.Overflow)
                    received = stream.Read<float>(ref rawSamples, timeoutUs: 1_000_000, out results);
                if (received == ErrorCode.Corruption ||
                    received == ErrorCode.Timeout ||
                    received == ErrorCode.StreamError) continue;
                for (int i = 0; i < results.NumSamples / 2; i++)
                {
                    iqBuffer[i] = new Complex(rawSamples[2 * i], rawSamples[2 * i + 1]);
                }

            }
        }
    }
}
