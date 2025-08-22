using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using FFTW.NET;
using MathNet.Numerics;
using MathNet.Numerics.Random;
using NLog;
using Pothosware.SoapySDR;
using SoapyRL.Extentions;
using SoapyRL.View;
using SoapyRL.View.tabs;
using Logger = NLog.Logger;

namespace SoapyRL;

public class PerformRl(MainWindow initiator)
{
    //FFT Queue
    private readonly ConcurrentQueue<Tuple<double, Complex[], double>> _fftQueue = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    //https://github.com/ghostop14/gr-correctiq
    private readonly double _ratio = 1e-05f;

    //the noise is  so it will be used the same for every sweep both reference and actual RL measure
    private float[]? _whiteNoise;

    private double _avgReal, _avgImg;

    private int _fftSize = 4096;

    private readonly List<Task> _fftTasks = new();
    public bool IsRunning, ResetData, Continous = false;
    public MainWindow Parent = initiator;

    public void BeginRl()
    {
        if (IsRunning) return;
        IsRunning = true;
        _fftTasks.Clear();
        _fftTasks.Add(Task.Run(() => { FFT_POOL(); }));
        _fftTasks.Add(Task.Run(() => { RlSampler(); }));
    }

    public void StopRl()
    {
        IsRunning = false;
        foreach (var task in _fftTasks)
            if (!task.IsCompleted)
                task.Wait();
    }

    public bool IsFftQueueEmpty()
    {
        return _fftQueue.IsEmpty;
    }

    private float[] GenerateWhiteNoise(int count)
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

    private double CalculateFrequency(double index, double fs, double n, double fCenter)
    {
        if (index < n / 2)
            // Positive frequencies with center frequency offset
            return index * fs / n + fCenter;

        // Negative frequencies with center frequency offset
        return (index - n) * fs / n + fCenter;
    }

    private float[][] WelchPsd(Complex[] inputSignal, FftwArrayComplex bufferInput,
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

    private void CalculateRbwvbw()
    {
        var sampleRate = Parent.TabDevice.DeviceCom.RxSampleRate;
        var overlap = (double)Parent.Configuration.Config[Configuration.SaVar.FftOverlap];
        var neff = _fftSize / (1 - overlap);
        var segmentLength = Math.Max(1, _fftSize / (int)Parent.Configuration.Config[Configuration.SaVar.FftSegment]);
        var stepSize = segmentLength - overlap;
        var numSegments = (_fftSize - overlap) / stepSize;
    }

    public void ResetIqFilter()
    {
        _avgReal = 0;
        _avgImg = 0;
        if (Parent.TabDevice.DeviceCom.SdrDevice == null) return;
        CalculateRbwvbw();
        //anything that affects the bin width,frequencies,welching method,etc... can and will affect the dc bias position on the IQ chart therfore we need to reset it
        //in addition we might aswell reset the plot since the functions that calls it will also change the bin spacing and frequency positioning which might not be in our span
        //man i've been coding this spectrum for so long it hurts, but it is fun!
        ResetData = true;
    }

    private void CorrectIq(Complex[] samples)
    {
        // return;
        for (var i = 0; i < samples.Length; i++)
        {
            _avgReal = _ratio * (samples[i].Real - _avgReal) + _avgReal;
            _avgImg = _ratio * (samples[i].Imaginary - _avgImg) + _avgImg;
            samples[i] = new Complex(samples[i].Real - _avgReal, samples[i].Imaginary - _avgImg);
        }
    }

    private void CalculateAutoFftSize()
    {
        _fftSize = Enumerable.Range(1, 15).Select(x => (int)Math.Pow(2, x)).OrderBy(i => i).First(x =>
            x - (int)(Parent.TabDevice.DeviceCom.RxSampleRate *
                (int)Parent.Configuration.Config[Configuration.SaVar.FftSegment] / 1e6) >= 0);
    }

    private void FFT_POOL()
    {
        var segmentLength = Math.Max(1, _fftSize / (int)Parent.Configuration.Config[Configuration.SaVar.FftSegment]);
        var overlap = (int)(segmentLength * (double)Parent.Configuration.Config[Configuration.SaVar.FftOverlap]);
        var stepSize = segmentLength - overlap; // Step size
        var numSegments = (_fftSize - overlap) / stepSize; // Number of segments
        var fftwArrayInput = new FftwArrayComplex(segmentLength);
        var fftwArrayOuput = new FftwArrayComplex(segmentLength);
        var fftwPlanContext = FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards);
        while (IsRunning || !_fftQueue.IsEmpty)
        {
            Tuple<double, Complex[], double> next;
            if (!_fftQueue.TryDequeue(out next))
            {
                Thread.Sleep(1);
                continue;
            }

            var fftSamples = next.Item2;

            var psd = WelchPsd(fftSamples, fftwArrayInput, fftwArrayOuput, fftwPlanContext, segmentLength,
                overlap, (float)next.Item3, (float)next.Item1);

            if (ResetData) continue;

            Parent.Graph.UpdateData(psd);
        }

        fftwPlanContext.Dispose();
        fftwArrayInput.Dispose();
        fftwArrayOuput.Dispose();
    }

    //i used to know what is going on in here, now i dont, but it works so dont touch or try to optimize
    //(a joke)
    private unsafe void RlSampler()
    {
        var sampleRate = Parent.TabDevice.DeviceCom.RxSampleRate;
        Parent.TabDevice.DeviceCom.SdrDevice.SetSampleRate(Direction.Rx, Parent.TabDevice.DeviceCom.RxAntenna.Item1,
            sampleRate);
        Parent.TabDevice.DeviceCom.SdrDevice.SetSampleRate(Direction.Tx, Parent.TabDevice.DeviceCom.TxAntenna.Item1,
            sampleRate);
        var frequency = (double)Parent.Configuration.Config[Configuration.SaVar.FreqStart] + sampleRate / 2;
        Parent.TabDevice.DeviceCom.SdrDevice.SetFrequency(Direction.Rx, Parent.TabDevice.DeviceCom.RxAntenna.Item1,
            frequency);
        Parent.TabDevice.DeviceCom.SdrDevice.SetFrequency(Direction.Tx, Parent.TabDevice.DeviceCom.TxAntenna.Item1,
            frequency);

        var rxStream = Parent.TabDevice.DeviceCom.SdrDevice.SetupRxStream(StreamFormat.ComplexFloat32,
            new[] { Parent.TabDevice.DeviceCom.RxAntenna.Item1 }, "");
        var txStream = Parent.TabDevice.DeviceCom.SdrDevice.SetupTxStream(StreamFormat.ComplexFloat32,
            new[] { Parent.TabDevice.DeviceCom.TxAntenna.Item1 }, "");
        rxStream.Activate();
        txStream.Activate();
        var rxMtu = rxStream.MTU;
        var txMtu = 4096;
        var results = new StreamResult();
        var rxFloatBuffer = new float[rxMtu * 2];
        if (_whiteNoise is null)
            _whiteNoise = GenerateWhiteNoise(txMtu);

        var rxBufferHandle = GCHandle.Alloc(rxFloatBuffer, GCHandleType.Pinned);
        var txBufferHandle = GCHandle.Alloc(_whiteNoise, GCHandleType.Pinned);
        _logger.Info($"Begining Stream MTU: {rxStream.MTU}");
        var sw = new Stopwatch();
        var keepTransmission = true;
        var transmitThread = new Thread(() =>
        {
            fixed (float* bufferPtr = _whiteNoise)
            {
                while (keepTransmission)
                {
                    var errorCode = txStream.Write((nint)bufferPtr, (uint)txMtu, StreamFlags.None, 0, 10_000_000,
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
            while (IsRunning && keepTransmission)
            {
                fixed (float* bufferPtr = rxFloatBuffer)
                {
                    var errorCode = rxStream.Read((nint)bufferPtr, (uint)rxMtu, 10_000_000, out results);

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

                var length = (int)Math.Min(rxMtu * 2, results.NumSamples);
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
            for (var fCenter = (double)Parent.Configuration.Config[Configuration.SaVar.FreqStart] + sampleRate / 2;
                 fCenter - sampleRate / 2 < (double)Parent.Configuration.Config[Configuration.SaVar.FreqStop]
                 && !Imports.GetAsyncKeyState(Keys.End) && IsRunning;
                 fCenter += sampleRate)
            {
                //some parent.tab_Device.deviceCOM.sdrDevices are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                if (frequency != fCenter)
                {
                    frequency = fCenter;
                    Parent.TabDevice.DeviceCom.SdrDevice.SetFrequency(Direction.Rx,
                        Parent.TabDevice.DeviceCom.RxAntenna.Item1, frequency);
                    Parent.TabDevice.DeviceCom.SdrDevice.SetFrequency(Direction.Tx,
                        Parent.TabDevice.DeviceCom.TxAntenna.Item1, frequency);
                    sw.Restart();
                    skipBufferAck = false;
                    skipBuffer = true;
                    while ((sw.ElapsedMilliseconds <
                            (int)Parent.Configuration.Config[Configuration.SaVar.LeakageSleep] ||
                            !skipBufferAck) && IsRunning)
                        Thread.Sleep(1);

                    samples.Clear();
                    Array.Clear(rxFloatBuffer, 0, rxFloatBuffer.Length);
                    totalSamples = 0;
                    skipBuffer = false;
                }
                //fill up the iqbuffer to have enough samples for FFT

                while (totalSamples < _fftSize && IsRunning) Thread.Sleep(10); //waiting for samples to fill up;

                var currentTotalSamples = 0;
                currentTotalSamples += totalSamples; // like this so its not a ref and actual copy
                while (currentTotalSamples > _fftSize && IsRunning)
                {
                    var iqCorrectionSamples = samples.Slice(currentTotalSamples - _fftSize, _fftSize).ToArray();
                    if ((bool)Parent.Configuration.Config[Configuration.SaVar.IqCorrection])
                        CorrectIq(iqCorrectionSamples);
                    var mean = iqCorrectionSamples.Aggregate((a, b) => a + b) / iqCorrectionSamples.Length;
                    for (var i = 0; i < iqCorrectionSamples.Length; i++)
                        iqCorrectionSamples[i] -= mean;
                    _fftQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, iqCorrectionSamples, sampleRate));
                    currentTotalSamples -= _fftSize;
                }
            }
        } while (Continous && Parent.TabTrace.STraces[1].ViewStatus == TabTrace.TraceViewStatus.Active && IsRunning);

        _logger.Info("Sweep Finished stopping transmission...");
        keepTransmission = false;
        transmitThread.Join();
        readingThread.Join();
        _logger.Info("Transmission stopped");
        txStream.Deactivate();
        txStream.Close();
        rxStream.Deactivate();
        rxStream.Close();
        _logger.Info("Closed Streams");
        _logger.Info("");
        IsRunning = false;
        rxBufferHandle.Free();
        txBufferHandle.Free();
    }
}