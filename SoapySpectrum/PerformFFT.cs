using MathNet.Numerics.IntegralTransforms;
using Pothosware.SoapySDR;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SoapySpectrum
{
    public static class PerformFFT
    {
        public static bool isRunning = false;
        static RxStream stream;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        static Dictionary<float, float> FFT_RESULTS = new Dictionary<float, float>();
        static Device device;
        public static void beginFFT()
        {
            device = UI.UI.sdr_device;
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
                Configuration.config["FFT_WINDOW"])(size);
        }
        static double[] getPeriodicWindowFunction(int size)
        {
            return ((Func<int, double[]>)
                Configuration.config["FFT_WINDOW_PERIODIC"])(size);
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
        static double[] WelchPSD(Complex[] signal, int segmentLength, int overlap)
        {
            int stepSize = segmentLength - overlap;
            int numSegments = (signal.Length - overlap) / stepSize;
            double[] psd = new double[signal.Length];

            //calculating normalizationFactor for periodic all segments but last
            double[] window = getPeriodicWindowFunction(segmentLength);
            double normalizationFactor_Periodic = 0.0;
            for (int i = 0; i < segmentLength; i++)
                normalizationFactor_Periodic += window[i] * window[i];

            //calculating normalizationFactor for last segment (non periodic)
            window = getWindowFunction(segmentLength);
            double normalizationFactor_regular = 0.0;
            for (int i = 0; i < segmentLength; i++)
                normalizationFactor_regular += window[i] * window[i];

            for (int seg = 0; seg < numSegments; seg++)
            {
                double normalizationFactor = normalizationFactor_Periodic;
                Complex[] segment = new Complex[segmentLength];
                if (seg + 1 == numSegments) //last segment apply non periodic window
                {
                    normalizationFactor = normalizationFactor_regular;
                }
                // Apply window to segment
                for (int i = 0; i < segmentLength; i++)
                    segment[i] = signal[seg * stepSize + i] * window[i];

                //adds zero padding if fftsize > segment length
                var output = new Complex[signal.Length];
                Array.Copy(segment, output, segmentLength);

                Fourier.Forward(output);

                segment = output;

                // Compute periodogram (magnitude squared)
                for (int k = 0; k < signal.Length; k++)
                    psd[k] += (segment[k].Magnitude * segment[k].Magnitude) / normalizationFactor;
            }

            // Average over segments
            for (int k = 0; k < signal.Length; k++)
                psd[k] /= numSegments;

            return psd;
        }

        //https://github.com/ghostop14/gr-correctiq
        static double ratio = 1e-05f;
        static double avg_real = 0.0, avg_img = 0.0;
        public static double RBW, VBW, ENBW;
        static void calculateRBWVBW()
        {

            double EN = 0;
            double BW = 0;
            int FFT_size = (int)Configuration.config["FFT_Size"];
            double[] window = getPeriodicWindowFunction(FFT_size);
            for (int j = 0; j < window.Length; j++)
            {
                EN += window[j] * window[j];
                BW += window[j];
            }
            BW *= BW;
            ENBW = (EN / BW) * (double)FFT_size;
            var sample_rate = (double)Configuration.config["sampleRate"];
            double overlap = (double)Configuration.config["FFT_overlap"];
            var neff = FFT_size / (1 - overlap);
            RBW = ENBW * sample_rate / neff;
            int segmentLength = Math.Max(1, FFT_size / (int)Configuration.config["FFT_segments"]);
            double stepSize = segmentLength - overlap;
            double numSegments = (FFT_size - overlap) / stepSize;
            VBW = RBW / Math.Sqrt(numSegments);
            RBW = (double)(int)RBW;
            VBW = (double)(int)VBW;
        }
        public static void resetIQFilter()
        {
            avg_real = 0;
            avg_img = 0;
            if (device == null) return;
            calculateRBWVBW();
            //anything that affects the bin width,frequencies,welching method,etc... can and will affect the dc bias position on the IQ chart therfore we need to reset it
            //in addition we might aswell reset the plot since the functions that calls it will also change the bin spacing and frequency positioning which might not be in our span
            //man i've been coding this spectrum for so long it hurts, but it is fun!
            while (!((double)Configuration.config["sampleRate"]).Equals(device.GetSampleRate(Direction.Rx, 0)))
            {

                Thread.Sleep(20);
            }
            UI.UI.clearPlotData();
        }
        static void correctIQ(Complex[] samples)
        {
            // return;
            for (int i = 0; i < samples.Length; i++)
            {
                avg_real = ratio * (samples[i].Real - avg_real) + avg_real;
                avg_img = ratio * (samples[i].Imaginary - avg_img) + avg_img;
                samples[i] = new Complex(samples[i].Real - avg_real, samples[i].Imaginary - avg_img);
            }
        }
        static void FFTCalculator(Tuple<double, Complex[]> next, double sample_rate)
        {

            var fft_samples = next.Item2;
            int fft_size = next.Item2.Length;
            int segmentLength = Math.Max(1, fft_size / (int)Configuration.config["FFT_segments"]);
            int overlap = (int)(segmentLength * (double)Configuration.config["FFT_overlap"]);
            int stepSize = segmentLength - overlap; // Step size
            int numSegments = (fft_size - overlap) / stepSize; // Number of segments

            //allowing the sdr to give more samples for that specific frequency
            /*Logger.Debug($"welch FFT_SIZE{fft_size} signal {fft_samples.Length}" +
                $" Segment Length {segmentLength} Overlap {overlap} totalSegments {numSegments}");*/
            double[] psd = WelchPSD(fft_samples, segmentLength, overlap);
            // Calculate and print the frequency for each PSD bin
            for (int i = 0; i < psd.Length; i++)
            {
                double frequency = CalculateFrequency(i, sample_rate, fft_size, next.Item1);
                double power_dBm = 10 * Math.Log10(psd[i]);
                //Logger.Debug($"{frequency}:{power_dBm}");
                FFT_RESULTS[(float)frequency] = (float)power_dBm;
            }
        }
        static void IQSampler(Device sdr)
        {
            var sample_rate = (double)Configuration.config["sampleRate"];
            sdr.SetSampleRate(Direction.Rx, 0, sample_rate);
            sdr.SetGain(Direction.Rx, 0, 0);
            double frequency = (double)Configuration.config["freqStart"] + sample_rate / 2;
            sdr.SetFrequency(Direction.Rx, 0, frequency);
            stream = sdr.SetupRxStream(Pothosware.SoapySDR.StreamFormat.ComplexFloat32, new uint[] { 0 }, ""); ;
            stream.Activate();
            var MTU = stream.MTU;
            var results = new StreamResult();
            var floatBuffer = new float[MTU * 2];
            GCHandle bufferHandle = GCHandle.Alloc(floatBuffer, GCHandleType.Pinned);
            Logger.Info($"Begining Stream MTU: {stream.MTU}");
            Stopwatch sw = new Stopwatch();
            while (isRunning)
            {
                if (sample_rate != (double)Configuration.config["sampleRate"])
                {
                    sample_rate = (double)Configuration.config["sampleRate"];
                    sdr.SetSampleRate(Direction.Rx, 0, sample_rate);
                }
                bool noHopping = (double)Configuration.config["freqStop"] - (double)Configuration.config["freqStart"] <= sample_rate;
                for (double f_center = (double)Configuration.config["freqStart"] + sample_rate / 2;
                    f_center + sample_rate / 2 <= (double)Configuration.config["freqStop"] || noHopping;
                    f_center += sample_rate)
                {

                    //some sdrs are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                    if (frequency != f_center)
                    {
                        frequency = f_center;
                        sdr.SetFrequency(Direction.Rx, 0, frequency);
                        Thread.Sleep((int)Configuration.config["leakageSleep"]);
                    }
                    //fill up the iqbuffer to have enough samples for FFT

                    var FFTSIZE = (int)Configuration.config["FFT_Size"];
                    Complex[] samples = new Complex[FFTSIZE];
                    int totalSamples = 0;
                    while (totalSamples < FFTSIZE)
                    {
                        unsafe
                        {
                            fixed (float* bufferPtr = floatBuffer)
                            {
                                var errorCode = stream.Read((nint)bufferPtr, (uint)MTU, 10_000_000, out results);
                                if (errorCode is not ErrorCode.None || results is null)
                                {
                                    Logger.Error($"Readstream Error Code {errorCode}");
                                    continue;
                                }
                            }
                        }
                        Logger.Debug($"Readstream samples returned {results.NumSamples} time {results.TimeNs},Flags {results.Flags}");
                        int length = (int)Math.Min(MTU * 2, results.NumSamples);
                        length = length / 2;
                        for (int i = 0; i < length && totalSamples < FFTSIZE; i += 2)
                        {
                            samples[totalSamples] = new Complex(floatBuffer[i], floatBuffer[i + 1]);
                            totalSamples++;
                        }
                    }
                    var IQCorrectionSamples = samples.Take(FFTSIZE).ToArray();
                    if ((bool)Configuration.config["IQCorrection"])
                        correctIQ(IQCorrectionSamples);
                    FFTCalculator(new Tuple<double, Complex[]>(frequency, IQCorrectionSamples), sample_rate);
                    UI.UI.updateData(FFT_RESULTS);
                    FFT_RESULTS.Clear();
                    if (noHopping)
                        break;
                }
                sw.Restart();
                while (sw.ElapsedMilliseconds < (long)Configuration.config["refreshRate"])
                {
                    //reading while sleeping so no buffer overflow will happen
                    unsafe
                    {
                        fixed (float* bufferPtr = floatBuffer)
                        {
                            var errorCode = stream.Read((nint)bufferPtr, (uint)MTU, 10_000_000, out results);
                            if (errorCode is not ErrorCode.None || results is null)
                            {
                                Logger.Error($"Readstream Error Code {errorCode}");
                                continue;
                            }
                        }
                    }
                    Thread.Sleep(0);
                }
            }
            stream.Deactivate();
            stream.Close();
        }
    }
}
