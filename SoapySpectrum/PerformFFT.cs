using FFTW.NET;
using MathNet.Numerics;
using Pothosware.SoapySDR;
using SoapyRL.UI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SoapyRL
{
    public static class PerformFFT
    {
        public static bool isRunning = false, resetData = false;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        //FFT Queue
        private static ConcurrentQueue<Tuple<double, Complex[], double>> FFTQueue = new ConcurrentQueue<Tuple<double, Complex[], double>>();

        private static Device device;

        public static void beginFFT()
        {
            device = tab_Device.s_sdrDevice;
            isRunning = true;
            new Thread(() =>
            {
                IQSampler(device);
            })
            { }.Start();
            new Thread(() =>
            {
                FFT_POOL();
            })
            { Priority = ThreadPriority.Highest }.Start();
        }

        private static double[] getWindowFunction(int size)
        {
            return ((Func<int, double[]>)
                Configuration.config[Configuration.saVar.fftWindow])(size);
        }
        public static void applyVBWFilter(float[] psd)
        {
            // Determine the number of frequency bins to apply smoothing to
            int smoothingBins = (int)((((double)Configuration.config[Configuration.saVar.fftRBW] /
            (double)Configuration.config[Configuration.saVar.fftVBW])) * psd.Length); // VBW as a fraction of RBW

            if (smoothingBins < 1)
            {
                smoothingBins = 1; // Ensure at least one bin is averaged
            }

            // Apply a simple moving average for smoothing
            var smoothedPsd = psd.AsSpan();
            for (int i = 0; i < psd.Length; i++)
            {
                float sum = 0;
                int count = 0;

                // Apply smoothing over the neighboring bins
                for (int j = i - smoothingBins / 2; j <= i + smoothingBins / 2; j++)
                {
                    if (j >= 0 && j < psd.Length)
                    {
                        sum += psd[j];
                        count++;
                    }
                }

                smoothedPsd[i] = sum / count;
            }
        }
        private static double CalculateFrequency(double index, double Fs, double N, double f_center)
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
        private static float[][]? WelchPSD(Complex[] inputSignal, FftwArrayComplex bufferInput, FftwArrayComplex bufferOutput, FftwPlanC2C plan, int segmentLength, int overlap, float sample_rate, float center)
        {
            try
            {
                var signal = inputSignal.AsSpan();
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
                    var segment = signal.Slice(start, segmentLength);

                    // Apply window to segment
                    for (int i = 0; i < segmentLength; i++)
                        bufferInput[i] = segment[i] * window[i];

                    plan.Execute();

                    // Compute periodogram (magnitude squared)
                    for (int k = 0; k < signal.Length; k++)
                    {
                        psd[0][k] += (float)((bufferOutput[k].MagnitudeSquared()) / (normalizationFactor_regular * segmentLength)); // Normalize by window and segment length
                    }
                }
                applyVBWFilter(psd[0]);

                // Average over segments and convert to dBm if needed
                var calibration = 0.0f;
                if (tab_Cal.s_currentCalData.Count > 0)
                {
                    calibration = tab_Cal.s_currentCalData.OrderBy(x => Math.Abs(x.frequency - center)).First().results;
                }

                // Convert to dBm
                for (int k = 0; k < segmentLength; k++)
                {
                    // Convert the power to dBm (if applicable)
                    psd[0][k] /= numSegments;
                    psd[0][k] = (float)(10 * Math.Log10(psd[0][k])) + calibration;

                    // Calculate frequency for each bin
                    float frequency = 0;
                    if (k < (segmentLength / 2.0))
                    {
                        // Positive frequencies with center frequency offset
                        frequency = ((k * sample_rate) / (float)segmentLength) + center;
                    }
                    else
                    {
                        // Negative frequencies with center frequency offset
                        frequency = (((k - segmentLength) * sample_rate) / (float)segmentLength) + center;
                    }

                    psd[1][k] = frequency;
                }

                return psd;
            } catch
            {
                return null; 
            }
        }

        //https://github.com/ghostop14/gr-correctiq
        private static double ratio = 1e-05f;

        private static double avg_real = 0.0, avg_img = 0.0;
        private static int NextPowerOfTwo(int n)
        {
            if (n <= 1) return 1;
            int power = 1;
            while (power < n) power <<= 1;
            return power;
        }
        public static void calculateRBWVBW()
        {
            // Get parameters from the configuration
            double ENBW = 1.0,
       RBW = (double)Configuration.config[Configuration.saVar.fftRBW],
       VBW = (double)Configuration.config[Configuration.saVar.fftVBW],
       sampleRate = (double)Configuration.config[Configuration.saVar.sampleRate],
       overlap = 0.5;
            
            // Introduce span of what you are sampling (e.g., 10 MHz or 10000 kHz)
            double span = (double)Configuration.config[Configuration.saVar.freqStop] - (double)Configuration.config[Configuration.saVar.freqStart]; // span in Hz

            // Define a maximum fftSize limit (e.g., 8192 for your system's memory limit)
            int maxFftSize = 32768;
            if (span / RBW < 20e3)
            {
                RBW = 1e6; //RBW too low, probably gonna cause problemas
            }
            // Calculate fftSegmentLength from RBW and ENBW considering span
            int fftSegmentLength = (int) Math.Round(ENBW * sampleRate / RBW);


            // Estimate fftSize based on segment length and overlap
            int fftSize = (int)Math.Round(span / RBW);
            fftSize = NextPowerOfTwo(fftSize);
            // If the estimated fftSize exceeds the maximum allowed fftSize, we need to adjust RBW and VBW
            if (fftSize > maxFftSize)
            {
                Logger.Info($"FFTSIZE Clipped {fftSize} -> {maxFftSize}");
                fftSize = maxFftSize;
            }

            // Set the configuration values
            Configuration.config[Configuration.saVar.fftOverlap] = overlap;
            Configuration.config[Configuration.saVar.fftSegmentLength] = (int)fftSegmentLength;
            Configuration.config[Configuration.saVar.fftSize] = (int)fftSize;
        }

        public static void resetIQFilter()
        {
            avg_real = 0;
            avg_img = 0;
            if (device == null) return;
            
            //anything that affects the bin width,frequencies,welching method,etc... can and will affect the dc bias position on the IQ chart therfore we need to reset it
            //in addition we might aswell reset the plot since the functions that calls it will also change the bin spacing and frequency positioning which might not be in our span
            //man i've been coding this spectrum for so long it hurts, but it is fun!
            while (!((double)Configuration.config[Configuration.saVar.sampleRate]).Equals(device.GetSampleRate(Direction.Rx, 0)))
            {
                Thread.Sleep(20);
            }
            resetData = true;
        }

        private static void correctIQ(Complex[] samples)
        {
            // return;
            for (int i = 0; i < samples.Length; i++)
            {
                avg_real = ratio * (samples[i].Real - avg_real) + avg_real;
                avg_img = ratio * (samples[i].Imaginary - avg_img) + avg_img;
                samples[i] = new Complex(samples[i].Real - avg_real, samples[i].Imaginary - avg_img);
            }
        }

    

        private static void FFT_POOL()
        {
            var fftwArrayInput = new FftwArrayComplex(1024);
            var fftwArrayOuput = new FftwArrayComplex(1024);
            var fftwPlanContext = FFTW.NET.FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards, PlannerFlags.Default);
            while (isRunning)
            {
                Tuple<double, Complex[], double> next;
                
                
                int segmentLength = (int)Configuration.config[Configuration.saVar.fftSegmentLength];
                if(fftwArrayInput.Length != segmentLength)
                {
                    fftwPlanContext.Dispose();
                    fftwArrayInput.Dispose();
                    fftwArrayOuput.Dispose();
                     fftwArrayInput = new FftwArrayComplex(segmentLength);
                     fftwArrayOuput = new FftwArrayComplex(segmentLength);
                    fftwPlanContext = FFTW.NET.FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards, PlannerFlags.Default);
                }
                int overlap = (int)(segmentLength * (double)Configuration.config[Configuration.saVar.fftOverlap]);
                int stepSize = segmentLength - overlap; // Step size
               

                if (resetData)
                {
                    FFTQueue.TryDequeue(out next);
                    continue;
                }
                if (!FFTQueue.TryDequeue(out next))
                {
                    Thread.Sleep(1);
                    continue;
                }
                var fft_samples = next.Item2;
                int fft_size = next.Item2.Length;
                int numSegments = (fft_size - overlap) / stepSize; // Number of segments
                float[][]? psd = WelchPSD(fft_samples, fftwArrayInput, fftwArrayOuput, fftwPlanContext, segmentLength, overlap, (float)next.Item3, (float)next.Item1);
                    if (psd is null) continue;
                    Graph.updateData(psd);
                
            }
        }

        private static unsafe void IQSampler(Device sdr)
        {
            var fft_size = (int)Configuration.config[Configuration.saVar.fftSize];
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

                    Complex[] samples = new Complex[fft_size];
                    int totalSamples = 0;

                    while (totalSamples < fft_size && isRunning)
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
                        for (int i = 0; i < length && totalSamples < fft_size; i += 2)
                        {
                            samples[totalSamples] = new Complex(floatBuffer[i], floatBuffer[i + 1]);
                            totalSamples++;
                        }
                    }

                    //did it finish the same sampling yet?
                    if (FFTQueue.Any(x => x.Item1 == frequency))
                        continue;
                    var IQCorrectionSamples = samples.Take(fft_size).ToArray();
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
                        calculateRBWVBW();
                        fft_size = (int)Configuration.config[Configuration.saVar.fftSize];
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