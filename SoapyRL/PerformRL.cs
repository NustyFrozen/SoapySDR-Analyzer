using FFTW.NET;
using ManagedCuda.CudaFFT;
using MathNet.Numerics;
using MathNet.Numerics.Random;
using NLog;
using Pothosware.SoapySDR;
using SoapyRL.Extentions;
using SoapyRL.UI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

namespace SoapyRL
{
    public static class PerformRL
    {
        public static bool isRunning = false, resetData = false;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        //FFT Queue
        private static ConcurrentQueue<Tuple<double, Complex[], double>> FFTQueue = new ConcurrentQueue<Tuple<double, Complex[], double>>();

        private static Device device;
        private static int FFT_size = 4096;

        public static void beginRL()
        {
            device = tab_Device.s_sdrDevice;
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

        public static bool isFFTQueueEmpty() => FFTQueue.IsEmpty;

        private static float[] generateWhiteNoise(int count)
        {
            var rng = new MersenneTwister(); // Fast, high-quality PRNG
            var buffer = new Complex[count];

            for (int i = 0; i < count; i++)
            {
                double iSample = rng.NextDouble() * 2.0 - 1.0; // [-1.0, +1.0]
                double qSample = rng.NextDouble() * 2.0 - 1.0;
                buffer[i] = new Complex(iSample, qSample);
            }
            var results = new float[2 * count];
            for (int i = 0; i < count; i++)
            {
                results[i * 2] = (float)buffer[i].Real;
                results[i * 2 + 1] = (float)buffer[i].Imaginary;
            }
            return results;
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

        private static float[][] WelchPSD(Complex[] inputSignal, FftwArrayComplex bufferInput, FftwArrayComplex bufferOutput,FftwPlanC2C plan, int segmentLength, int overlap, float sampleRate, float center)
        {
            var signal = inputSignal.AsSpan();
            int numSegments = (signal.Length - overlap) / (segmentLength - overlap);
            float[][] psd = new float[2][];
            psd[0] = new float[segmentLength];
            psd[1] = new float[segmentLength];

            // Calculate normalization factor for window (Hanning, Hamming, etc.)
               var window = Window.Gauss(segmentLength,0.5);
            double windowPower = 0;
            for (int i = 0; i < window.Length; i++)
                windowPower += window[i] * window[i];
            for (int seg = 0; seg < numSegments; seg++)
            {
                int start = seg * (segmentLength - overlap);
                var segment = signal.Slice(start, segmentLength);
                for (int i = 0; i < segmentLength; i++)
                    bufferInput[i] = segment[i] * window[i];
                // Perform FFT
                plan.Execute();
                // Compute periodogram (magnitude squared)
                for (int k = 0; k < segmentLength; k++)
                {
                    psd[0][k] += (float)((bufferOutput[k].MagnitudeSquared())); // Normalize by window and segment length
                }
            }

            // Average over segments and convert to dBm if needed
            float scale = (float)(sampleRate * windowPower * numSegments);
            var freqStart = center - sampleRate / 2.0;
            var freqStop = center + sampleRate / 2.0;
            var frequencyBinScale = inputSignal.Length / segmentLength;
            // Convert to dBm
            for (int k = 0; k < segmentLength; k++)
            {
                // Convert the power to dBm (if applicable)
                psd[0][k] /= scale;
                psd[0][k] = (float)(10 * Math.Log10(psd[0][k]));

                // Calculate frequency for each bin
                float frequency = 0;
                if (k < (segmentLength / 2.0))
                {
                    // Positive frequencies with center frequency offset
                    frequency = ((k * sampleRate) / (float)segmentLength) + center;
                }
                else
                {
                    // Negative frequencies with center frequency offset
                    frequency = (((k - segmentLength) * sampleRate) / (float)segmentLength) + center;
                }

                psd[1][k] = frequency;
            }

            return psd;
        }

        //https://github.com/ghostop14/gr-correctiq
        private static double ratio = 1e-05f;

        private static double avg_real = 0.0, avg_img = 0.0;

        private static void calculateRBWVBW()
        {
            calculateAutoFFTSize();

            var sample_rate = (double)Configuration.config[Configuration.saVar.rxSampleRate];
            double overlap = (double)Configuration.config[Configuration.saVar.fftOverlap];
            var neff = FFT_size / (1 - overlap);
            int segmentLength = Math.Max(1, FFT_size / (int)Configuration.config[Configuration.saVar.fftSegment]);
            double stepSize = segmentLength - overlap;
            double numSegments = (FFT_size - overlap) / stepSize;
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

        private static void calculateAutoFFTSize()
        {
            var hops = ((double)Configuration.config[Configuration.saVar.freqStop] - (double)Configuration.config[Configuration.saVar.freqStart]) / ((double)Configuration.config[Configuration.saVar.rxSampleRate] / 2);
            FFT_size = Enumerable.Range(1, 15).Select(x => (int)Math.Pow(2, x)).OrderBy(i => i).First(x => x - (int)(((double)Configuration.config[Configuration.saVar.rxSampleRate] * (int)Configuration.config[Configuration.saVar.fftSegment]) / 1e6) >= 0);
            
        }
        static AlignedArrayComplex _fftwArray;
        
        private static void FFT_POOL()
        {
            
            int segmentLength = Math.Max(1, FFT_size / (int)Configuration.config[Configuration.saVar.fftSegment]);
            int overlap = (int)(segmentLength * (double)Configuration.config[Configuration.saVar.fftOverlap]);
            int stepSize = segmentLength - overlap; // Step size
            int numSegments = (FFT_size - overlap) / stepSize; // Number of segments
            var fftwArrayInput = new FftwArrayComplex(segmentLength);
            var fftwArrayOuput = new FftwArrayComplex(segmentLength);
            var _fftwPlanContext = FFTW.NET.FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput,DftDirection.Forwards,PlannerFlags.Default);
            while (isRunning || !FFTQueue.IsEmpty)
            {
                Tuple<double, Complex[], double> next;
                if (!FFTQueue.TryDequeue(out next))
                {
                    Thread.Sleep(1);
                    continue;
                }
                var fft_samples = next.Item2;

                float[][] psd = WelchPSD(fft_samples,fftwArrayInput,fftwArrayOuput,_fftwPlanContext, segmentLength, overlap, (float)next.Item3, (float)next.Item1);

                if (resetData)
                {
                    continue;
                }
                Graph.updateData(psd);
            }
            _fftwPlanContext.Dispose();
            fftwArrayInput.Dispose();
            fftwArrayOuput.Dispose();
        }

        //the noise is static so it will be used the same for every sweep both reference and actual RL measure
        private static float[]? _whiteNoise = null;

        private static unsafe void IQSampler(Device sdr)
        {
            calculateAutoFFTSize();
            var sample_rate = (double)Configuration.config[Configuration.saVar.rxSampleRate];
            sdr.SetSampleRate(Direction.Rx, 0, sample_rate);
            sdr.SetSampleRate(Direction.Tx, 0, sample_rate);
            sdr.SetGain(Direction.Rx, 0, 0);
            double frequency = (double)Configuration.config[Configuration.saVar.freqStart] + sample_rate / 2;
            sdr.SetFrequency(Direction.Rx, 0, frequency);
            var rxStream = sdr.SetupRxStream(Pothosware.SoapySDR.StreamFormat.ComplexFloat32, new uint[] { 0 }, "");
            var txStream = sdr.SetupTxStream(Pothosware.SoapySDR.StreamFormat.ComplexFloat32, new uint[] { 0 }, "");
            rxStream.Activate();
            txStream.Activate();
            var rxMTU = rxStream.MTU / 2;
            var txMTU = 4096;
            var results = new StreamResult();
            float[] rxFloatBuffer = new float[rxMTU * 2];
            if (_whiteNoise is null)
                _whiteNoise = generateWhiteNoise((int)txMTU);

            GCHandle rxBufferHandle = GCHandle.Alloc(rxFloatBuffer, GCHandleType.Pinned);
            GCHandle txBufferHandle = GCHandle.Alloc(_whiteNoise, GCHandleType.Pinned);
            Logger.Info($"Begining Stream MTU: {rxStream.MTU}");
            Stopwatch sw = new Stopwatch();
            bool keepTransmiting = true;
            Thread transmitThread = new Thread(() =>
            {
                fixed (float* bufferPtr = _whiteNoise)
                {
                    while (keepTransmiting)
                    {
                        var errorCode = txStream.Write((nint)bufferPtr, (uint)txMTU, StreamFlags.None, 0, 10_000_000, out results);
                        if (errorCode is not ErrorCode.None || results is null)
                        {
#if DEBUG_VERBOSE
                                    Logger.Error($"WriteStream Error Code {errorCode}");
#endif
                            continue;
                        }
                    }
                }
            });
            transmitThread.Start();

            bool skipBuffer = false, skipBufferAck = false;

            List<Complex> samples = new List<Complex>();
            int totalSamples = 0;

            Thread readingThread = new Thread(() =>
            {
                while (isRunning && keepTransmiting)
                {
                    unsafe
                    {
                        fixed (float* bufferPtr = rxFloatBuffer)
                        {
                            var errorCode = rxStream.Read((nint)bufferPtr, (uint)rxMTU, 10_000_000, out results);

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
                    if (skipBuffer)
                    {
                        skipBufferAck = true;
                        continue;
                    }
                    int length = (int)Math.Min(rxMTU * 2, results.NumSamples);
                    length = length / 2;
                    for (int i = 0; i < length; i += 2)
                    {
                        samples.Add(new Complex(rxFloatBuffer[i], rxFloatBuffer[i + 1]));
                        totalSamples++;
                    }
                }
            });
            readingThread.Start();
            for (double f_center = (double)Configuration.config[Configuration.saVar.freqStart] + (sample_rate / 2);
                f_center - (sample_rate / 2) < (double)Configuration.config[Configuration.saVar.freqStop]
                && !Imports.GetAsyncKeyState(Keys.End);
                f_center += sample_rate)
            {
                //some sdrs are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                if (frequency != f_center)
                {
                    frequency = f_center;
                    sdr.SetFrequency(Direction.Rx, 0, frequency);
                    sdr.SetFrequency(Direction.Tx, 0, frequency);
                    sw.Restart();
                    skipBufferAck = false;
                    skipBuffer = true;
                    while ((sw.ElapsedMilliseconds < (int)Configuration.config[Configuration.saVar.leakageSleep] || !skipBufferAck) && isRunning)
                    {
                        Thread.Sleep(1);
                    }
                    samples.Clear();
                    Array.Clear(rxFloatBuffer, 0, rxFloatBuffer.Length);
                    totalSamples = 0;
                    skipBuffer = false;
                }
                //fill up the iqbuffer to have enough samples for FFT

                while (totalSamples < FFT_size)
                {
                    Thread.Sleep(10); //waiting for samples to fill up;
                }
                var currentTotalSamples = 0;
                currentTotalSamples += totalSamples; // like this so its not a ref and actual copy
                while (currentTotalSamples > FFT_size)
                {
                    var IQCorrectionSamples = samples.Slice(currentTotalSamples - FFT_size, FFT_size).ToArray();
                    if ((bool)Configuration.config[Configuration.saVar.iqCorrection])
                        correctIQ(IQCorrectionSamples);
                    Complex mean = IQCorrectionSamples.Aggregate((a, b) => a + b) / IQCorrectionSamples.Length;
                    for (int i = 0; i < IQCorrectionSamples.Length; i++)
                        IQCorrectionSamples[i] -= mean;
                    FFTQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, IQCorrectionSamples, sample_rate));
                    currentTotalSamples -= FFT_size;
                }
            }

            Logger.Info($"Sweep Finished stopping transmission...");
            keepTransmiting = false;
            transmitThread.Join();
            readingThread.Join();
            Logger.Info($"Transmission stopped");
            txStream.Deactivate();
            txStream.Close();
            rxStream.Deactivate();
            rxStream.Close();
            Logger.Info($"Closed Streams");
            Logger.Info($"");
            rxBufferHandle.Free();
            txBufferHandle.Free();
        }
    }
}