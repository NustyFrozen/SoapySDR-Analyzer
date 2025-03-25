using ClickableTransparentOverlay;
using FFTW.NET;
using MathNet.Numerics;
using Pothosware.SoapySDR;
using SoapySpectrum.UI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SoapySpectrum
{
    public static class PerformFFT
    {
        public static bool isRunning = false, resetData = false;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        //FFT Queue
        static ConcurrentQueue<Tuple<double, Complex[], double>> FFTQueue = new ConcurrentQueue<Tuple<double, Complex[], double>>();
        static Device device;
        static int FFT_size = 4096;
        public static void beginFFT()
        {
            device = tab_Device.sdr_device;
            isRunning = true;
            new Thread(() =>
            {

                FFT_POOL();
            })
            { Priority = ThreadPriority.Highest }.Start();

            new Thread(() =>
            {

                IQSampler(device);
            })
            { }.Start();



        }
        static double[] getWindowFunction(int size)
        {
            return ((Func<int, double[]>)
                Configuration.config[Configuration.saVar.fftWindow])(size);
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
        //CalculateFrequency(i, next.Item3, fft_size, next.Item1);
        static float[][] WelchPSD(Complex[] signal, int segmentLength, int overlap, float sample_rate, float center)
        {
            int numSegments = (signal.Length - overlap) / (segmentLength - overlap);
            float[][] psd = new float[2][];
            psd[0] = new float[signal.Length];
            psd[1] = new float[signal.Length];

            // Calculate normalization factor for window (Hanning, Hamming, etc.)
            double[] window = getWindowFunction(segmentLength);
            double normalizationFactor_regular = 0.0;
            for (int i = 0; i < segmentLength; i++)
                normalizationFactor_regular += window[i] * window[i];

            for (int seg = 0; seg < numSegments; seg++)
            {
                int start = seg * (segmentLength - overlap);
                var segment = signal.Skip(start).Take(segmentLength).ToArray();

                // Apply window to segment
                for (int i = 0; i < segmentLength; i++)
                    segment[i] *= window[i];

                var output = new Complex[signal.Length];
                var input = new Complex[signal.Length];
                Array.Copy(segment, input, segmentLength);

                // Perform FFT
                using (var pinIn = new PinnedArray<Complex>(input))
                using (var pinOut = new PinnedArray<Complex>(output))
                {
                    DFT.FFT(pinIn, pinOut);  // Perform FFT here
                }

                segment = output;

                // Compute periodogram (magnitude squared)
                for (int k = 0; k < signal.Length; k++)
                {
                    psd[0][k] += (float)((output[k].MagnitudeSquared()) / (normalizationFactor_regular * segmentLength)); // Normalize by window and segment length
                }
            }

            // Average over segments and convert to dBm if needed
            var calibration = 0.0f;
            if (tab_Cal.current_cal.Count > 0)
            {
                calibration = tab_Cal.current_cal.OrderBy(x => Math.Abs(x.frequency - center)).First().results;
            }

            // Convert to dBm
            for (int k = 0; k < signal.Length; k++)
            {
                // Convert the power to dBm (if applicable)
                psd[0][k] /= numSegments;
                psd[0][k] = (float)(10 * Math.Log10(psd[0][k])) + calibration;

                // Calculate frequency for each bin
                float frequency = 0;
                if (k < (signal.Length / 2.0))
                {
                    // Positive frequencies with center frequency offset
                    frequency = ((k * sample_rate) / (float)signal.Length) + center;
                }
                else
                {
                    // Negative frequencies with center frequency offset
                    frequency = (((k - signal.Length) * sample_rate) / (float)signal.Length) + center;
                }

                psd[1][k] = frequency;
            }

            return psd;
        }



        //https://github.com/ghostop14/gr-correctiq
        static double ratio = 1e-05f;
        static double avg_real = 0.0, avg_img = 0.0;
        public static double RBW, VBW, ENBW,EN;
        static void calculateRBWVBW()
        {
            double BW = 0;
            if (0 == (int)Configuration.config[Configuration.saVar.fftSize])
            {
                //auto
                calculateAutoFFTSize();
            } else
            FFT_size = (int)Configuration.config[Configuration.saVar.fftSize];
            double[] window = getWindowFunction(FFT_size);
            for (int j = 0; j < window.Length; j++)
            {
                EN += window[j] * window[j];
                BW += window[j];
            }
            BW *= BW;
            ENBW = (EN / BW) * (double)FFT_size;
            var sample_rate = (double)Configuration.config[Configuration.saVar.sampleRate];
            double overlap = (double)Configuration.config[Configuration.saVar.fftOverlap];
            var neff = FFT_size / (1 - overlap);
            RBW = ENBW * sample_rate / neff;
            int segmentLength = Math.Max(1, FFT_size / (int)Configuration.config[Configuration.saVar.fftSegment]);
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
            while (!((double)Configuration.config[Configuration.saVar.sampleRate]).Equals(device.GetSampleRate(Direction.Rx, 0)))
            {

                Thread.Sleep(20);
            }
            resetData = true;
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
        static void calculateAutoFFTSize()
        {
            var hops= ((double)Configuration.config[Configuration.saVar.freqStop] - (double)Configuration.config[Configuration.saVar.freqStart]) / ((double)Configuration.config[Configuration.saVar.sampleRate]/2);
            FFT_size = Array.ConvertAll(tab_Video.FFTLength.Skip(1).ToArray(),s=> int.Parse(s)).OrderBy(i=>i).First(x=>x / (float)(int)Configuration.config[Configuration.saVar.fftSegment] > Configuration.graphSize.X/hops);
        }
        static void FFT_POOL()
        {

            while (isRunning)
            {
                Tuple<double, Complex[], double> next;
                if (!FFTQueue.TryDequeue(out next))
                {
                    Thread.Sleep(1);
                    continue;
                }
                var fft_samples = next.Item2;
                int fft_size = next.Item2.Length;
                int segmentLength = Math.Max(1, fft_size / (int)Configuration.config[Configuration.saVar.fftSegment]);
                int overlap = (int)(segmentLength * (double)Configuration.config[Configuration.saVar.fftOverlap]);
                int stepSize = segmentLength - overlap; // Step size
                int numSegments = (fft_size - overlap) / stepSize; // Number of segments

                float[][] psd = WelchPSD(fft_samples, segmentLength, overlap, (float)next.Item3, (float)next.Item1);

                if (resetData)
                {

                    continue;
                }
                Graph.updateData(psd);
            }
        }
        static unsafe void IQSampler(Device sdr)
        {
            var sample_rate = (double)Configuration.config[Configuration.saVar.sampleRate];
            sdr.SetSampleRate(Direction.Rx, 0, sample_rate);
            sdr.SetGain(Direction.Rx, 0, 0);
            double frequency = (double)Configuration.config[Configuration.saVar.freqStart] + sample_rate / 2;
            sdr.SetFrequency(Direction.Rx, 0, frequency);
            var stream = sdr.SetupRxStream(Pothosware.SoapySDR.StreamFormat.ComplexFloat32, new uint[] { 0 }, ""); ;
            stream.Activate();
            var MTU = stream.MTU;
            var results = new StreamResult();
            var floatBuffer = new float[MTU * 2];
            GCHandle bufferHandle = GCHandle.Alloc(floatBuffer, GCHandleType.Pinned);
            Logger.Info($"Begining Stream MTU: {stream.MTU}");
            Stopwatch sw = new Stopwatch();
            while (isRunning)
            {
                if (sample_rate != (double)Configuration.config[Configuration.saVar.sampleRate])
                {
                    sample_rate = (double)Configuration.config[Configuration.saVar.sampleRate];
                    sdr.SetSampleRate(Direction.Rx, 0, sample_rate);
                }
                bool noHopping = (double)Configuration.config[Configuration.saVar.freqStop] - (double)Configuration.config[Configuration.saVar.freqStart] <= sample_rate;
                for (double f_center = (double)Configuration.config[Configuration.saVar.freqStart] + sample_rate / 2;
                    f_center - sample_rate / 2 < (double)Configuration.config[Configuration.saVar.freqStop] || noHopping;
                    f_center += sample_rate)
                {

                    //some sdrs are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                    if (frequency != f_center)
                    {
                        frequency = f_center;
                        sdr.SetFrequency(Direction.Rx, 0, frequency);
                        sw.Restart();
                        while (sw.ElapsedMilliseconds < (int)Configuration.config[Configuration.saVar.leakageSleep] && isRunning)
                        {
                            //reading while sleeping so no buffer overflow will happen
                            unsafe
                            {
                                fixed (float* bufferPtr = floatBuffer)
                                {
                                    Array.Clear(floatBuffer, 0, floatBuffer.Length);
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
                    //fill up the iqbuffer to have enough samples for FFT

                    var FFTSIZE = FFT_size;
                    Complex[] samples = new Complex[FFTSIZE];
                    int totalSamples = 0;

                    while (totalSamples < FFTSIZE && isRunning)
                    {
                        unsafe
                        {

                            fixed (float* bufferPtr = floatBuffer)
                            {

                                var errorCode = stream.Read((nint)bufferPtr, (uint)MTU, 10_000_000, out results);

                                if (errorCode is not ErrorCode.None || results is null)
                                {
#if DEBUG_VERBOSE
                                    Logger.Error($"Readstream Error Code {errorCode}");
#endif

                                    continue;
                                }
                            }
                        }

#if DEBUG_VERBOSE
                        Logger.Debug($"Readstream samples returned {results.NumSamples} time {results.TimeNs},Flags {results.Flags}");
#endif

                        int length = (int)Math.Min(MTU * 2, results.NumSamples);
                        length = length / 2;
                        for (int i = 0; i < length && totalSamples < FFTSIZE; i += 2)
                        {
                            samples[totalSamples] = new Complex(floatBuffer[i], floatBuffer[i + 1]);
                            totalSamples++;
                        }
                    }



                    //did it finish the same sampling yet?
                    if (FFTQueue.Any(x => x.Item1 == frequency))
                        continue;
                    var IQCorrectionSamples = samples.Take(FFTSIZE).ToArray();
                    if ((bool)Configuration.config[Configuration.saVar.iqCorrection])
                        correctIQ(IQCorrectionSamples);
                    FFTQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, IQCorrectionSamples, sample_rate));

                    if (noHopping)
                        break;
                }
                sw.Restart();
                while ((sw.ElapsedMilliseconds < (long)Configuration.config[Configuration.saVar.refreshRate] | resetData) && isRunning)
                {
                    //reading while sleeping so no buffer overflow will happen
                    unsafe
                    {
                        fixed (float* bufferPtr = floatBuffer)
                        {
                            var errorCode = stream.Read((nint)bufferPtr, (uint)MTU, 10_000_000, out results);
                            if (errorCode is not ErrorCode.None || results is null)
                            {
#if DEBUG_VERBOSE
                                Logger.Error($"Readstream Error Code {errorCode}");
#endif
                                continue;
                            }
                        }
                    }
                    if (resetData && FFTQueue.Count == 0)
                    {
                        resetData = false;
                        Graph.clearPlotData();
                        break;

                    }
                    Thread.Sleep(0);
                }
            }
            stream.Deactivate();
            stream.Close();
        }
    }
}
