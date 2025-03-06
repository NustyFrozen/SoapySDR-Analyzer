using MathNet.Numerics.IntegralTransforms;
using Pothosware.SoapySDR;
using System.Numerics;

namespace SoapySpectrum
{
    public static class PerformFFT
    {
        public static bool isRunning = false;
        static RxStream stream;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static void beginFFT()
        {
            var device = UI.UI.sdr_device;
            isRunning = true;
            Thread sampler = new Thread(() =>
            {

                IQSampler(device);
            })
            { Priority = ThreadPriority.Highest };
            sampler.Start();

        }
        static double[] getWindowFunction(int size)
        {
            return ((Func<int, double[]>)
                Configuration.config["FFTWINDOW"])(size);
        }
        static void simpleFFT(Tuple<double, Complex[]> next)
        {
            var fft_samples = next.Item2;
            Fourier.Forward(fft_samples, FourierOptions.Default);
            int fft_size = next.Item2.Length;
            double sample_rate = (double)Configuration.config["sampleRate"];
            //allowing the sdr to give more samples for that specific frequency

            double binWidth = sample_rate / fft_size;  // Frequency spacing per bin
            var windowMultiplier = getWindowFunction(fft_size);
            for (int i = 0; i < fft_size; i++)
            {
                fft_samples[i] *= windowMultiplier[i];
            }
            Dictionary<float, float> results = new Dictionary<float, float>();
            for (int i = 0; i < fft_size; i++)
            {
                double frequency = next.Item1 + (i - fft_size / 2) * binWidth;  // Frequency of the bin (in Hz)
                double magnitude = fft_samples[i].Magnitude;  // Get magnitude of FFT result
                double power_dBm = 10 * Math.Log10(magnitude * magnitude) - 50;
                results.Add((float)frequency, (float)power_dBm);

            }
            UI.UI.updateData(results);


        }
        static double CalculateFrequency(double index, double Fs, double N, double f_center)
        {
            if (index < N / 2)
            {
                // Positive frequencies with center frequency offset
                return ((index * Fs) / (double)N) + f_center;
            }
            else
            {
                // Negative frequencies with center frequency offset
                return (((index - N) * Fs) / (double)N) + f_center;
            }
        }
        static double[] WelchPSD(Complex[] signal, int segmentLength, int overlap, int fftSize)
        {
            int stepSize = segmentLength - overlap;
            int numSegments = (signal.Length - overlap) / stepSize;
            double[] psd = new double[fftSize];

            double[] window = getWindowFunction(segmentLength); // Hann window
            double normalizationFactor = 0.0;

            for (int i = 0; i < segmentLength; i++)
                normalizationFactor += window[i] * window[i];

            for (int seg = 0; seg < numSegments; seg++)
            {
                Complex[] segment = new Complex[fftSize];

                // Apply window to segment
                for (int i = 0; i < segmentLength; i++)
                    segment[i] = signal[seg * stepSize + i] * window[i];

                // Perform FFT
                Fourier.Forward(segment, FourierOptions.Default);

                // Compute periodogram (magnitude squared)
                for (int k = 0; k < fftSize; k++)
                    psd[k] += (segment[k].Magnitude * segment[k].Magnitude) / normalizationFactor;
            }

            // Average over segments
            for (int k = 0; k < fftSize; k++)
                psd[k] /= numSegments;

            return psd;
        }

        //https://github.com/ghostop14/gr-correctiq
        static double ratio = 1e-05f;
        static double avg_real = 0.0, avg_img = 0.0;
        static void correctIQ(Complex[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                avg_real = ratio * (samples[i].Real - avg_real) + avg_real;
                avg_img = ratio * (samples[i].Imaginary - avg_img) + avg_img;
                samples[i] = new Complex(samples[i].Real - avg_real, samples[i].Imaginary - avg_img);
            }
        }
        static void FFTCalculator(Tuple<double, Complex[]> next)
        {

            var fft_samples = next.Item2;
            int fft_size = next.Item2.Length;
            int segmentLength = Math.Max(1, fft_size / (int)Configuration.config["FFT_segments"]); // Segment length
            int overlap = (int)(segmentLength * (double)Configuration.config["FFT_overlap"]); // 50% overlap
            int stepSize = segmentLength - overlap; // Step size
            int numSegments = (fft_size - overlap) / stepSize; // Number of segments

            double sample_rate = (double)Configuration.config["sampleRate"];
            //allowing the sdr to give more samples for that specific frequency

            double[] psd = WelchPSD(fft_samples, segmentLength, overlap, fft_size);
            Dictionary<float, float> results = new Dictionary<float, float>();
            // Calculate and print the frequency for each PSD bin
            for (int i = 0; i < psd.Length; i++)
            {
                double frequency = CalculateFrequency(i, sample_rate, fft_size, next.Item1);
                double power_dBm = 10 * Math.Log10(psd[i]);
                //Logger.Debug($"{frequency}:{power_dBm}");
                if (frequency == next.Item1)
                {
                    continue;
                }
                results.Add((float)frequency, (float)power_dBm);
            }
            UI.UI.updateData(results);
        }
        static void IQSampler(Device sdr)
        {
            stream = sdr.SetupRxStream(Pothosware.SoapySDR.StreamFormat.ComplexFloat32, new uint[] { 0 }, ""); ;
            stream.Activate();
            var MTU = stream.MTU;
            var results = new StreamResult();
            while (isRunning)
            {

                var sample_rate = (double)Configuration.config["sampleRate"];
                sdr.SetSampleRate(Direction.Rx, 0, sample_rate);
                for (double f_center = (double)Configuration.config["freqStart"]; f_center <= (double)Configuration.config["freqStop"]; f_center += sample_rate / 2)
                {

                    double frequency = f_center;
                    sdr.SetFrequency(Direction.Rx, 0, frequency);
                    //fill up the iqbuffer to have enough samples for FFT

                    var FFTSIZE = (int)Configuration.config["FFT_Size"];
                    List<Complex> samples = new List<Complex>();
                    while (samples.Count < FFTSIZE)
                    {
                        //either overflow from the sdr itself or from waiting for the fft so waiting it out

                        var floatBuffer = new float[MTU * 2];
                        var errorCode = stream.Read(ref floatBuffer, 100000, out results);
                        int length = (int)Math.Min(MTU * 2, results.NumSamples);
                        length = length / 2;

                        for (int i = 0; i < length || samples.Count == FFTSIZE; i += 2)
                        {
                            samples.Add(new Complex(floatBuffer[i], floatBuffer[i + 1]));
                        }
                        //Logger.Debug($"Read returned {streamResult.NumSamples} elements, expected {numSamples} errorcode: {received}  flags {Convert.ToString(Convert.ToByte(streamResult.Flags), 2).PadLeft(8, '0')}");

                    }
                    var IQCorrectionSamples = samples.ToArray();
                    correctIQ(IQCorrectionSamples);
                    FFTCalculator(new Tuple<double, Complex[]>(frequency, IQCorrectionSamples));
                }
            }
            stream.Deactivate();
            stream.Close();
        }
    }
}
