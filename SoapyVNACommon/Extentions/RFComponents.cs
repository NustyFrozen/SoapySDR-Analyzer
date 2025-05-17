using System.Numerics;

namespace SoapyVNACommon.Extentions
{
    public class IQDCBlocker
    {
        private double alpha;
        private double prevI = 0.0f;
        private double prevQ = 0.0f;
        private double prevOutI = 0.0f;
        private double prevOutQ = 0.0f;

        public IQDCBlocker(double alpha = 0.995f)
        {
            this.alpha = alpha;
        }

        public void ProcessSignal(Complex[] input)
        {
            var data = input.AsSpan();
            for (int pos = 0; pos < data.Length; pos++)
            {
                var sample = data[pos];

                input[pos] = new Complex(sample.Real - prevI + alpha * prevOutI
                    , sample.Imaginary - prevQ + alpha * prevOutQ);
                prevI = sample.Real;
                prevQ = sample.Imaginary;
                prevOutI = input[pos].Real;
                prevOutQ = input[pos].Imaginary;
            }
        }
    }
}