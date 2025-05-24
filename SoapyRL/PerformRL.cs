using FFTW.NET;
using MathNet.Numerics;
using MathNet.Numerics.Random;
using NLog;
using Pothosware.SoapySDR;
using SoapyRL.Extentions;
using SoapyRL.View;
using SoapyRL.View.tabs;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Logger = NLog.Logger;

namespace SoapyRL;

public class PerformRL(MainWindow initiator)
{
    public MainWindow parent = initiator;
    public bool isRunning, resetData, continous = false;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    //FFT Queue
    private readonly ConcurrentQueue<Tuple<double, Complex[], double>> FFTQueue = new();

    private int FFT_size = 4096;

    //https://github.com/ghostop14/gr-correctiq
    private readonly double ratio = 1e-05f;

    private double avg_real, avg_img;

    //the noise is  so it will be used the same for every sweep both reference and actual RL measure
    private float[]? _whiteNoise;

    private List<Task> fftTasks = new List<Task>();

    public void beginRL()
    {
        if (isRunning) return;
        isRunning = true;
        fftTasks.Clear();
        fftTasks.Add(Task.Run(() => { FFT_POOL(); }));
        fftTasks.Add(Task.Run(() => { RLSampler(); }));
    }

    public void stopRL()
    {
        isRunning = false;
        foreach (var task in fftTasks)
            if (!task.IsCompleted)
                task.Wait();
    }

    public bool isFFTQueueEmpty()
    {
        return FFTQueue.IsEmpty;
    }

    private float[] generateWhiteNoise(int count)
    {
        var rng = new MersenneTwister(); // Fast, high-quality PRNG
        var buffer = new Complex[count];

        for (var i = 0; i < count; i++)
        {
            var phase = rng.NextDouble() * 360.0;
            var iSample = Math.Cos(phase);
            var qSample = Math.Sin(phase);
            buffer[i] = new Complex(iSample, qSample);
        }

        var results = new float[2 * count];
        for (var i = 0; i < count; i++)
        {
            results[i * 2] = (float)buffer[i].Real;
            results[i * 2 + 1] = (float)buffer[i].Imaginary;
        }

        return results;
    }

    private double CalculateFrequency(double index, double Fs, double N, double f_center)
    {
        if (index < N / 2)
            // Positive frequencies with center frequency offset
            return index * Fs / N + f_center;

        // Negative frequencies with center frequency offset
        return (index - N) * Fs / N + f_center;
    }

    private float[][] WelchPSD(Complex[] inputSignal, FftwArrayComplex bufferInput,
        FftwArrayComplex bufferOutput, FftwPlanC2C plan, int segmentLength, int overlap, float sampleRate,
        float center)
    {
        var signal = inputSignal.AsSpan();
        var numSegments = (signal.Length - overlap) / (segmentLength - overlap);
        var psd = new float[2][];
        psd[0] = new float[segmentLength];
        psd[1] = new float[segmentLength];

        // Calculate normalization factor for window (Hanning, Hamming, etc.)
        var window = Window.BlackmanHarris(segmentLength);
        double windowPower = 0;
        for (var i = 0; i < window.Length; i++)
            windowPower += window[i] * window[i];
        for (var seg = 0; seg < numSegments; seg++)
        {
            var start = seg * (segmentLength - overlap);
            var segment = signal.Slice(start, segmentLength);
            for (var i = 0; i < segmentLength; i++)
                bufferInput[i] = segment[i] * window[i];
            // Perform FFT
            plan.Execute();
            // Compute periodogram (magnitude squared)
            for (var k = 0; k < segmentLength; k++)
                psd[0][k] +=
                    (float)bufferOutput[k].MagnitudeSquared(); // Normalize by window and segment length
        }

        // Average over segments and convert to dBm if needed
        var scale = (float)(sampleRate * windowPower * numSegments);
        var freqStart = center - sampleRate / 2.0;
        var freqStop = center + sampleRate / 2.0;
        var frequencyBinScale = inputSignal.Length / segmentLength;
        // Convert to dBm
        for (var k = 0; k < segmentLength; k++)
        {
            // Convert the power to dBm (if applicable)
            psd[0][k] /= scale;
            psd[0][k] = (float)(10 * Math.Log10(psd[0][k]));

            // Calculate frequency for each bin
            float frequency = 0;
            if (k < segmentLength / 2.0)
                // Positive frequencies with center frequency offset
                frequency = k * sampleRate / segmentLength + center;
            else
                // Negative frequencies with center frequency offset
                frequency = (k - segmentLength) * sampleRate / segmentLength + center;

            psd[1][k] = frequency;
        }

        return psd;
    }

    private void calculateRBWVBW()
    {
        var sample_rate = parent.tab_Device.deviceCOM.rxSampleRate;
        var overlap = (double)parent.Configuration.config[Configuration.saVar.fftOverlap];
        var neff = FFT_size / (1 - overlap);
        var segmentLength = Math.Max(1, FFT_size / (int)parent.Configuration.config[Configuration.saVar.fftSegment]);
        var stepSize = segmentLength - overlap;
        var numSegments = (FFT_size - overlap) / stepSize;
    }

    public void resetIQFilter()
    {
        avg_real = 0;
        avg_img = 0;
        if (parent.tab_Device.deviceCOM.sdrDevice == null) return;
        calculateRBWVBW();
        //anything that affects the bin width,frequencies,welching method,etc... can and will affect the dc bias position on the IQ chart therfore we need to reset it
        //in addition we might aswell reset the plot since the functions that calls it will also change the bin spacing and frequency positioning which might not be in our span
        //man i've been coding this spectrum for so long it hurts, but it is fun!
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

    private void calculateAutoFFTSize()
    {
        FFT_size = Enumerable.Range(1, 15).Select(x => (int)Math.Pow(2, x)).OrderBy(i => i).First(x =>
            x - (int)((double)parent.tab_Device.deviceCOM.rxSampleRate *
                (int)parent.Configuration.config[Configuration.saVar.fftSegment] / 1e6) >= 0);
    }

    private void FFT_POOL()
    {
        var segmentLength = Math.Max(1, FFT_size / (int)parent.Configuration.config[Configuration.saVar.fftSegment]);
        var overlap = (int)(segmentLength * (double)parent.Configuration.config[Configuration.saVar.fftOverlap]);
        var stepSize = segmentLength - overlap; // Step size
        var numSegments = (FFT_size - overlap) / stepSize; // Number of segments
        var fftwArrayInput = new FftwArrayComplex(segmentLength);
        var fftwArrayOuput = new FftwArrayComplex(segmentLength);
        var _fftwPlanContext = FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards);
        while (isRunning || !FFTQueue.IsEmpty)
        {
            Tuple<double, Complex[], double> next;
            if (!FFTQueue.TryDequeue(out next))
            {
                Thread.Sleep(1);
                continue;
            }

            var fft_samples = next.Item2;

            var psd = WelchPSD(fft_samples, fftwArrayInput, fftwArrayOuput, _fftwPlanContext, segmentLength,
                overlap, (float)next.Item3, (float)next.Item1);

            if (resetData) continue;

            parent.Graph.updateData(psd);
        }

        _fftwPlanContext.Dispose();
        fftwArrayInput.Dispose();
        fftwArrayOuput.Dispose();
    }

    //i used to know what is going on in here, now i dont, but it works so dont touch or try to optimize
    //(a joke)
    private unsafe void RLSampler()
    {
        var sample_rate = parent.tab_Device.deviceCOM.rxSampleRate;
        parent.tab_Device.deviceCOM.sdrDevice.SetSampleRate(Direction.Rx, parent.tab_Device.deviceCOM.rxAntenna.Item1, sample_rate);
        parent.tab_Device.deviceCOM.sdrDevice.SetSampleRate(Direction.Tx, parent.tab_Device.deviceCOM.txAntenna.Item1, sample_rate);
        var frequency = (double)parent.Configuration.config[Configuration.saVar.freqStart] + sample_rate / 2;
        parent.tab_Device.deviceCOM.sdrDevice.SetFrequency(Direction.Rx, parent.tab_Device.deviceCOM.rxAntenna.Item1, frequency);
        parent.tab_Device.deviceCOM.sdrDevice.SetFrequency(Direction.Tx, parent.tab_Device.deviceCOM.txAntenna.Item1, frequency);

        var rxStream = parent.tab_Device.deviceCOM.sdrDevice.SetupRxStream(StreamFormat.ComplexFloat32, new uint[] { parent.tab_Device.deviceCOM.rxAntenna.Item1 }, "");
        var txStream = parent.tab_Device.deviceCOM.sdrDevice.SetupTxStream(StreamFormat.ComplexFloat32, new uint[] { parent.tab_Device.deviceCOM.txAntenna.Item1 }, "");
        rxStream.Activate();
        txStream.Activate();
        var rxMTU = rxStream.MTU;
        var txMTU = 4096;
        var results = new StreamResult();
        var rxFloatBuffer = new float[rxMTU * 2];
        if (_whiteNoise is null)
            _whiteNoise = generateWhiteNoise((int)txMTU);

        var rxBufferHandle = GCHandle.Alloc(rxFloatBuffer, GCHandleType.Pinned);
        var txBufferHandle = GCHandle.Alloc(_whiteNoise, GCHandleType.Pinned);
        Logger.Info($"Begining Stream MTU: {rxStream.MTU}");
        var sw = new Stopwatch();
        var keepTransmission = true;
        var transmitThread = new Thread(() =>
        {
            fixed (float* bufferPtr = _whiteNoise)
            {
                while (keepTransmission)
                {
                    var errorCode = txStream.Write((nint)bufferPtr, (uint)txMTU, StreamFlags.None, 0, 10_000_000,
                        out results);
                    if (errorCode is not ErrorCode.None || results is null)
                    {
#if DEBUG_VERBOSE
                                    Logger.Error($"WriteStream Error Code {errorCode}");
#endif
                    }
                }
            }
        });
        transmitThread.Start();

        bool skipBuffer = false, skipBufferAck = false;

        var samples = new List<Complex>();
        var totalSamples = 0;

        var readingThread = new Thread(() =>
        {
            while (isRunning && keepTransmission)
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

#if DEBUG_VERBOSE
                        Logger.Debug($"Readstream samples returned {results.NumSamples} time {results.TimeNs},Flags {results.Flags}");
#endif
                if (skipBuffer)
                {
                    skipBufferAck = true;
                    continue;
                }

                var length = (int)Math.Min(rxMTU * 2, results.NumSamples);
                length = length / 2;
                for (var i = 0; i < length; i += 2)
                {
                    samples.Add(new Complex(rxFloatBuffer[i], rxFloatBuffer[i + 1]));
                    totalSamples++;
                }
            }
        });
        readingThread.Start();
        do
        {
            for (var f_center = (double)parent.Configuration.config[Configuration.saVar.freqStart] + sample_rate / 2;
                 f_center - sample_rate / 2 < (double)parent.Configuration.config[Configuration.saVar.freqStop]
                 && !Imports.GetAsyncKeyState(Keys.End) && isRunning;
                 f_center += sample_rate)
            {
                //some parent.tab_Device.deviceCOM.sdrDevices are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                if (frequency != f_center)
                {
                    frequency = f_center;
                    parent.tab_Device.deviceCOM.sdrDevice.SetFrequency(Direction.Rx, parent.tab_Device.deviceCOM.rxAntenna.Item1, frequency);
                    parent.tab_Device.deviceCOM.sdrDevice.SetFrequency(Direction.Tx, parent.tab_Device.deviceCOM.txAntenna.Item1, frequency);
                    sw.Restart();
                    skipBufferAck = false;
                    skipBuffer = true;
                    while ((sw.ElapsedMilliseconds < (int)parent.Configuration.config[Configuration.saVar.leakageSleep] ||
                            !skipBufferAck) && isRunning)
                        Thread.Sleep(1);

                    samples.Clear();
                    Array.Clear(rxFloatBuffer, 0, rxFloatBuffer.Length);
                    totalSamples = 0;
                    skipBuffer = false;
                }
                //fill up the iqbuffer to have enough samples for FFT

                while (totalSamples < FFT_size && isRunning) Thread.Sleep(10); //waiting for samples to fill up;

                var currentTotalSamples = 0;
                currentTotalSamples += totalSamples; // like this so its not a ref and actual copy
                while (currentTotalSamples > FFT_size && isRunning)
                {
                    var IQCorrectionSamples = samples.Slice(currentTotalSamples - FFT_size, FFT_size).ToArray();
                    if ((bool)parent.Configuration.config[Configuration.saVar.iqCorrection])
                        correctIQ(IQCorrectionSamples);
                    var mean = IQCorrectionSamples.Aggregate((a, b) => a + b) / IQCorrectionSamples.Length;
                    for (var i = 0; i < IQCorrectionSamples.Length; i++)
                        IQCorrectionSamples[i] -= mean;
                    FFTQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, IQCorrectionSamples, sample_rate));
                    currentTotalSamples -= FFT_size;
                }
            }
        } while (continous && parent.tab_Trace.s_traces[1].viewStatus == tab_Trace.traceViewStatus.active && isRunning);
        Logger.Info("Sweep Finished stopping transmission...");
        keepTransmission = false;
        transmitThread.Join();
        readingThread.Join();
        Logger.Info("Transmission stopped");
        txStream.Deactivate();
        txStream.Close();
        rxStream.Deactivate();
        rxStream.Close();
        Logger.Info("Closed Streams");
        Logger.Info("");
        isRunning = false;
        rxBufferHandle.Free();
        txBufferHandle.Free();
    }
}