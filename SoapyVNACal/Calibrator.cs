using FFTW.NET;
using MathNet.Numerics;
using Newtonsoft.Json;
using NLog;
using Pothosware.SoapySDR;
using SoapyVNACommon.Extentions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
namespace SoapyVNACal;

public class Calibrator
    {
        public bool isRunning, resetData;
        private IqdcBlocker dcBlock = new IqdcBlocker();
        private SdrDeviceCom device;
        private readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
        //FFT Queue
        private readonly ConcurrentQueue<Tuple<double, Complex[], double>> FFTQueue = new();

        private int FFT_size = 4096, fft_segments = 20;

        private double RBW = 1e6, overlap = 0.5, frequency = 0.0;
        //https://github.com/ghostop14/gr-correctiq
        private readonly double ratio = 1e-05f;
        private double peakFreq, peakdB;

        private double avg_real, avg_img;
        private List<Task> fftTasks = new List<Task>();

        public void beginFFT()
        {
            if (isRunning) return;
            isRunning = true;
            fftTasks.Clear();
            fftTasks.Add(Task.Run(() => { FFT_POOL(); }));
            fftTasks.Add(Task.Run(() => { IQSampler(); }));
        }

        public void stopFFT()
        {
            isRunning = false;
            foreach (var task in fftTasks)
                if (!task.IsCompleted)
                    task.Wait();
        }

        //CalculateFrequency(i, next.Item3, fft_size, next.Item1);
        private float[][] WelchPSD(Complex[] inputSignal, FftwArrayComplex bufferInput,
            FftwArrayComplex bufferOutput, FftwPlanC2C plan, int segmentLength, int overlap, float sample_rate,
            float center)
        {
            try
            {
                var signal = inputSignal.AsSpan();
                var numSegments = (signal.Length - overlap) / (segmentLength - overlap);
                var psd = new float[2][];
                psd[0] = new float[segmentLength];
                psd[1] = new float[segmentLength];

                // Calculate normalization factor for window (Hanning, Hamming, etc.)
                var window = Window.FlatTop(segmentLength);

                double sum_w2 = 0;
                for (int i = 0; i < segmentLength; i++)
                {
                    sum_w2 += window[i] * window[i];
                }
                double scale = segmentLength * sample_rate * sum_w2;
                for (var seg = 0; seg < numSegments; seg++)
                {
                    var start = seg * (segmentLength - overlap);
                    if (start + segmentLength > signal.Length) break; //out of boundaries
                    var segment = signal.Slice(start, segmentLength);

                    // Apply window to segment
                    for (var i = 0; i < segmentLength; i++)
                        bufferInput[i] = segment[i] * window[i];

                    plan.Execute();

                    // Compute periodogram (magnitude squared)
                    for (var k = 0; k < segment.Length; k++)
                        psd[0][k] += (float)(bufferOutput[k].MagnitudeSquared() / scale); // Normalize by window and segment length
                }
                // Convert to dBm
                for (var k = 0; k < segmentLength; k++)
                {
                    // Convert the power to dBm (if applicable)
                    psd[0][k] /= numSegments;
                    psd[0][k] = (float)(10 * Math.Log10(psd[0][k]));

                    // Calculate frequency for each bin
                    float frequency = 0;
                    if (k < segmentLength / 2.0)
                        // Positive frequencies with center frequency offset
                        frequency = k * sample_rate / segmentLength + center;
                    else
                        // Negative frequencies with center frequency offset
                        frequency = (k - segmentLength) * sample_rate / segmentLength + center;

                    psd[1][k] = frequency;
                }

                return psd;
            }
            catch (Exception ex)
            {

                return null;
            }
        }

        private void calculateRBWVBW()
        {
            var rbw = (double)RBW;
            var numberOfSegments = (int)fft_segments;
            var desiredSegmentLength = device.RxSampleRate / rbw;
            var desiredfftLength = desiredSegmentLength * numberOfSegments;
            FFT_size = (int)Math.Pow(2, (int)Math.Ceiling(Math.Log(desiredfftLength, 2)));
            dcBlock = new IqdcBlocker();
        }

        public void resetIQFilter()
        {
            avg_real = 0;
            avg_img = 0;
            calculateRBWVBW();
            var sampleRate = (double)device.RxSampleRate;
            resetData = true;
        }

        private void correctIQ(Complex[] samples)
        {
            // return;
            for (var i = 0; i < samples.Length; i++)
            {
                avg_real = ratio * (samples[i].Real - avg_real) + avg_real;
                avg_img = ratio * (samples[i].Imaginary - avg_img) + avg_img;
                samples[i] = new Complex(samples[i].Real - avg_real, samples[i].Imaginary - avg_img);
            }
        }

        private void FFT_POOL()
        {
            var fftwArrayInput = new FftwArrayComplex(1024);
            var fftwArrayOuput = new FftwArrayComplex(1024);
            var fftwPlanContext = FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards);
            while (isRunning)
            {
                Tuple<double, Complex[], double> next;
                if (!FFTQueue.TryDequeue(out next))
                {
                    Thread.Sleep(1);
                    continue;
                }

                var fft_samples = next.Item2;
                var fft_size = next.Item2.Length;
                var segmentLength = Math.Max(1, fft_size / (int)fft_segments);
                if (fftwArrayInput.Length != segmentLength || resetData)
                {
                    fftwPlanContext.Dispose();
                    fftwArrayInput.Dispose();
                    fftwArrayOuput.Dispose();
                    fftwArrayInput = new FftwArrayComplex(segmentLength);
                    fftwArrayOuput = new FftwArrayComplex(segmentLength);
                    fftwPlanContext = FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards);
                    continue;
                }

                var overlapLength = (int)(segmentLength * overlap);
                var stepSize = segmentLength - overlapLength; // Step size
                var numSegments = (fft_size - overlapLength) / stepSize; // Number of segments

                var psd = WelchPSD(fft_samples, fftwArrayInput, fftwArrayOuput, fftwPlanContext, segmentLength, overlapLength,
                    (float)next.Item3, (float)next.Item1);
                if (psd != null)
                {
                    peakdB = psd[0].Max();
                    peakFreq = psd[1][psd[0].ToList().FindIndex(x => x == peakdB)];
                }
            }
        }

        private unsafe void IQSampler()
        {

            double sample_rate = 0.0, f_center = 0.0;
            device.SdrDevice.SetAntenna(Direction.Rx, device.RxAntenna.Item1, device.RxAntenna.Item2);

            var stream = device.SdrDevice.SetupRxStream(StreamFormat.ComplexFloat32, new uint[] { device.RxAntenna.Item1 }, "");
            stream.Activate();
            var MTU = stream.MTU;
            var results = new StreamResult();
            var floatBuffer = new float[MTU * 2];
            var bufferHandle = GCHandle.Alloc(floatBuffer, GCHandleType.Pinned);
            var sw = new Stopwatch();
            Logger.Info($"Begining Sampling {{MTU: {stream.MTU}, FFT length: {FFT_size}, SPS: {device.RxSampleRate}");
            while (isRunning)
            {
                if (sample_rate != (double)device.RxSampleRate)
                {
                    sample_rate = (double)device.RxSampleRate;
                    device.SdrDevice.SetSampleRate(Direction.Rx, device.RxAntenna.Item1, sample_rate);
                    resetIQFilter();
                }
                //some devices are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                if (frequency != f_center)
                {
                    f_center = frequency;
                    device.SdrDevice.SetFrequency(Direction.Rx, device.RxAntenna.Item1, frequency);
                    sw.Restart();
                    while (sw.ElapsedMilliseconds < (int)50 &&
                           isRunning)
                    {
                        //reading while sleeping so no buffer overflow will happen
                        fixed (float* bufferPtr = floatBuffer)
                        {
                            Array.Clear(floatBuffer, 0, floatBuffer.Length);
                            var errorCode = stream.Read((nint)bufferPtr, (uint)MTU, 10_000_000, out results);
                            if (errorCode is not ErrorCode.None || results is null)
                            {

                                continue;
                            }
                        }

                        Thread.Sleep(0);
                    }
                }
                //fill up the iqbuffer to have enough samples for FFT

                var FFTSIZE = FFT_size;
                var samples = new Complex[FFTSIZE];
                var totalSamples = 0;

                while (totalSamples < FFTSIZE && isRunning)
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

#if DEBUG_VERBOSE
                        Logger.Debug($"Readstream samples returned {results.NumSamples} time {results.TimeNs},Flags {results.Flags}");
#endif

                    var length = (int)Math.Min(MTU * 2, results.NumSamples);
                    length = length / 2;
                    for (var i = 0; i < length && totalSamples < FFTSIZE; i += 2)
                    {
                        samples[totalSamples] = new Complex(floatBuffer[i], floatBuffer[i + 1]);
                        totalSamples++;
                    }
                }

                //did it finish the same sampling yet?
                if (FFTQueue.Any(x => x.Item1 == frequency))
                    continue;
                var IQCorrectionSamples = samples.Take(FFTSIZE).ToArray();
                correctIQ(IQCorrectionSamples);
                dcBlock.ProcessSignal(IQCorrectionSamples);
                FFTQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, IQCorrectionSamples, sample_rate));
                sw.Restart();
                while (resetData &&
                       isRunning)
                {
                    //reading while sleeping so no buffer overflow will happen
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

                    if (resetData && FFTQueue.Count == 0)
                    {
                        resetData = false;
                        break;
                    }

                    Thread.Sleep(0);
                }
            }

            stream.Deactivate();
            stream.Close();
        }
        public static int selectOption(string message, string[] options)
        {
            Console.Clear();
            Console.WriteLine(message);
            int i = 1;
            foreach (var option in options)
            {
                Console.WriteLine($"{i++} {option}");
            }

            int selectedResult;
            while (true)
            {
                Console.WriteLine("Option: ");
                int.TryParse(Console.ReadLine(), out selectedResult);
                selectedResult--;
                if (selectedResult >= 0 && selectedResult < options.Length)
                    return selectedResult;
                Console.WriteLine($"{selectedResult} is not a valid option");
            }
        }
        public static void calibrateRX(SdrDeviceCom device, uint channel, string antenna, double freqStart, double freqStop, double hop, double expectedValue)
        {
            Calibrator x = new Calibrator();
            x.device = device;
            x.device.RxAntenna = new Tuple<uint, string>(channel, antenna);
            x.device.RxSampleRate = 16e6;//x.device.deviceRxSampleRates[(int)channel].MaxBy(x => x.Maximum).Maximum;
            x.RBW = x.device.RxSampleRate / 10000.0;
            x.frequency = freqStart;
            x.beginFFT();
            //freq,dB correction
            List<Tuple<float, float>> results = new();
            Console.WriteLine($"Steps: Transmit CW from a signal generator {expectedValue} dBM at {x.frequency} Hz");
            while (x.frequency <= freqStop)
            {
                Thread.Sleep(100);

                Console.Title = $"F1: Apply Correction|" +
                                $"END: force end calibration|" +
                                $"PROGRESS {((freqStart - x.frequency) / (freqStop - freqStart)) * 100}%";

                Console.WriteLine($"Tuned Frequency {x.frequency} Detected Peak at {x.peakFreq} Hz {x.peakdB} dBm");


               
            }

            char[] removeCharacters = Path.GetInvalidFileNameChars();
            string calibrationName = $"{DateTime.Now.ToString("T").Replace(":", "-")}-{freqStart}-{freqStop}-{hop}";
            foreach (var removeCharacter in removeCharacters)
                calibrationName = calibrationName.Replace(removeCharacter, '\0');
            calibrationName = calibrationName.Replace(" ", "");
            File.WriteAllText(Path.Combine(Global.CalibrationPath, $"{calibrationName}.json"), JsonConvert.SerializeObject(results));
            Console.WriteLine($"Calibration Saved --> {calibrationName}");
        }
    }