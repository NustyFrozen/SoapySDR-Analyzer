using FFTW.NET;
using MathNet.Numerics;
using MathNet.Numerics.Random;
using NLog;
using Pothosware.SoapySDR;
using SharpGen.Runtime;
using SoapySA.View;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Logger = NLog.Logger;

namespace SoapySA;

public class PerformFft
{
    private readonly ConcurrentQueue<Tuple<double, Complex[], double>> _fftQueue = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly double _ratio = 1e-05f;

    private double _avgReal, _avgImg, _hopSize;
    private int _fftSize = 4096;
    private readonly List<Task> _fftTasks = new();
    public bool IsRunning, ResetData;

    // Use the injected objects from the primary constructor
    private readonly Configuration Config;
    private readonly SdrDeviceCom Com;
    private readonly GraphPlotManager graphHandle;
    public PerformFft(IWidget widget,Configuration config, SdrDeviceCom com, GraphPlotManager graphHandle)
    {
        this.Config = config;
        this.Com = com;
        this.graphHandle = graphHandle;
        widget.OnWidgetEnter += (object? s, EventArgs e) => BeginFft();
        widget.OnWidgetExit += (object? s, EventArgs e) => StopFft();

        config.OnConfigLoadBegin += (object? s, EventArgs e) => StopFft();
        config.OnConfigLoadEnd += (object? s, EventArgs e) => BeginFft();
        config.OnConfigSaveBegin += (object? s, EventArgs e) => StopFft();
        config.OnConfigSaveEnd += (object? s, EventArgs e) => BeginFft();

    }

    private void stopFFTFromEvent(object? sender, EventArgs e) => StopFft();
    private void startFFTFromEvent(object? sender, EventArgs e) => BeginFft();

    public List<Tuple<float, float>>? calibrationData;
    public void BeginFft()
    {
        if (IsRunning) return;
        IsRunning = true;
        _fftTasks.Clear();
        _fftTasks.Add(Task.Run(() => FFT_POOL()));
        _fftTasks.Add(Task.Run(() => IqSampler()));
        _fftTasks.Add(Task.Run(() => sourceWriter()));
    }

    public void StopFft()
    {
        IsRunning = false;
        foreach (var task in _fftTasks)
            if (!task.IsCompleted)
                task.Wait();
    }

    private static float[] GenerateWhiteNoise(int count)
    {
        var rng = new MersenneTwister();
        var buffer = new Complex[count];

        for (var i = 0; i < count; i++)
        {
            var phase = rng.NextDouble() * 360.0;
            buffer[i] = new Complex(Math.Cos(phase), Math.Sin(phase));
        }

        var results = new float[2 * count];
        for (var i = 0; i < count; i++)
        {
            results[i * 2] = (float)buffer[i].Real;
            results[i * 2 + 1] = (float)buffer[i].Imaginary;
        }
        return results;
    }

    private float[][]? WelchPsd(Complex[] inputSignal, FftwArrayComplex bufferInput,
        FftwArrayComplex bufferOutput, FftwPlanC2C plan, int segmentLength, int overlap, float sampleRate,
        float center)
    {
        try
        {
            var signal = inputSignal.AsSpan();
            var numSegments = (signal.Length - overlap) / (segmentLength - overlap);
            var psd = new float[2][];
            psd[0] = new float[segmentLength];
            psd[1] = new float[segmentLength];

            var window = Window.FlatTop(segmentLength);
            double sumW2 = 0;
            for (var i = 0; i < segmentLength; i++) sumW2 += window[i] * window[i];

            var scale = segmentLength * sampleRate * sumW2;
            for (var seg = 0; seg < numSegments; seg++)
            {
                var start = seg * (segmentLength - overlap);
                if (start + segmentLength > signal.Length) break;
                var segment = signal.Slice(start, segmentLength);

                for (var i = 0; i < segmentLength; i++)
                    bufferInput[i] = segment[i] * window[i];

                plan.Execute();

                for (var k = 0; k < segment.Length; k++)
                    psd[0][k] += (float)(bufferOutput[k].MagnitudeSquared() / scale);
            }

            // Calibration access (assumes _parent is still needed for UI/External logic not in ctor)
            var calibration = 0.0f;
            // Note: If CalibrationView is also in Config/Com, move it there too.
            // Using placeholder _parent logic here as it wasn't specified for injection
             if (calibrationData is { } data) 
                calibration = data.OrderBy(x => Math.Abs(x.Item1 - center)).First().Item2;

            for (var k = 0; k < segmentLength; k++)
            {
                psd[0][k] /= numSegments;
                psd[0][k] = (float)(10 * Math.Log10(psd[0][k])) + calibration;

                float frequency = (k < segmentLength / 2.0)
                    ? k * sampleRate / segmentLength + center
                    : (k - segmentLength) * sampleRate / segmentLength + center;

                psd[1][k] = frequency;
            }

            if (Config.FreqInterleaving)
            {
                var newpsd = new float[psd[0].Length / 2];
                var newpsdFreq = new float[psd[1].Length / 2];
                for (var i = 0; i < psd[0].Length / 4; i++)
                {
                    newpsd[i] = psd[0][psd[0].Length / 8 + i];
                    newpsd[i + newpsd.Length / 2] = psd[0][psd[0].Length / 8 + i + psd[0].Length / 2];
                    newpsdFreq[i] = psd[1][psd[1].Length / 8 + i];
                    newpsdFreq[i + newpsdFreq.Length / 2] = psd[1][psd[1].Length / 8 + i + psd[1].Length / 2];
                }
                psd[0] = newpsd;
                psd[1] = newpsdFreq;
            }

            return psd;
        }
        catch (Exception ex)
        {
            _logger.Error($"FFT ERROR {ex.Message} {ex.StackTrace}");
            return null;
        }
    }

    private void CalculateRbwvbw()
    {
        var rbw = Config.FftRbw;
        var numberOfSegments = Config.FftSegment;
        var desiredSegmentLength = Com.RxSampleRate / rbw;
        var desiredfftLength = desiredSegmentLength * numberOfSegments;
        _fftSize = (int)Math.Pow(2, (int)Math.Ceiling(Math.Log(desiredfftLength, 2)));

        _logger.Info($"RBW {rbw} FFTSIZE {_fftSize}");
    }

    public void ResetIqFilter()
    {
        _avgReal = 0;
        _avgImg = 0;
        if (Com.SdrDevice == null) return;

        CalculateRbwvbw();
        var sampleRate = Com.RxSampleRate;
        _hopSize = Config.FreqInterleaving ? sampleRate / 4.0 : sampleRate;

        graphHandle.ClearPlotData();
        ResetData = true;
    }

    private void CorrectIq(Complex[] samples)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            _avgReal = _ratio * (samples[i].Real - _avgReal) + _avgReal;
            _avgImg = _ratio * (samples[i].Imaginary - _avgImg) + _avgImg;
            samples[i] = new Complex(samples[i].Real - _avgReal, samples[i].Imaginary - _avgImg);
        }
    }

    private void FFT_POOL()
    {
        var fftwArrayInput = new FftwArrayComplex(1024);
        var fftwArrayOuput = new FftwArrayComplex(1024);
        var fftwPlanContext = FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards);

        while (IsRunning)
        {
            if (!_fftQueue.TryDequeue(out var next))
            {
                Thread.Sleep(1);
                continue;
            }

            var fftSamples = next.Item2;
            var fftSize = next.Item2.Length;
            var segmentLength = Math.Max(1, fftSize / Config.FftSegment);

            if (fftwArrayInput.Length != segmentLength || ResetData)
            {
                fftwPlanContext.Dispose();
                fftwArrayInput.Dispose();
                fftwArrayOuput.Dispose();
                fftwArrayInput = new FftwArrayComplex(segmentLength);
                fftwArrayOuput = new FftwArrayComplex(segmentLength);
                fftwPlanContext = FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards);
                continue;
            }

            var overlap = (int)(segmentLength * Config.FftOverlap);
            var psd = WelchPsd(fftSamples, fftwArrayInput, fftwArrayOuput, fftwPlanContext, segmentLength, overlap,
                (float)next.Item3, (float)next.Item1);

            // Still uses _parent for the Graph View update
             if (psd != null) graphHandle.UpdateData(psd);
        }
    }

    private static int transmissionRate = 4096;
    static float[] _whiteNoise = GenerateWhiteNoise(transmissionRate);
    private static TxStream? transmissionStream;

    private unsafe void sourceWriter()
    {
        Task.Run(() =>
        {
            bool isTxEnabled = false;
            bool isTracking = false;
            var sourceMode = Config.SourceMode;
            var results = new StreamResult();

            while (IsRunning)
            {
                if (sourceMode != Config.SourceMode)
                {
                    sourceMode = Config.SourceMode;

                    if (sourceMode != 0 && !isTxEnabled)
                    {
                        if (transmissionStream is null)
                        {
                            transmissionStream = Com.SdrDevice.SetupTxStream(StreamFormat.ComplexFloat32, new[] { Com.TxAntenna.Item1 }, "");
                            Com.SdrDevice.SetSampleRate(Direction.Tx, Com.TxAntenna.Item1, Com.RxSampleRate);
                        }
                        transmissionStream.Activate();
                    }
                    else if (sourceMode == 0 && isTxEnabled)
                    {
                        transmissionStream?.Deactivate();
                    }

                    if (sourceMode == 2) // CW
                    {
                        transmissionRate = 4096;
                        _whiteNoise = Enumerable.Range(0, 4096 * 2).Select(x => x % 2 == 0 ? 1.0f : 0.0f).ToArray();
                        Com.SdrDevice.SetFrequency(Direction.Tx, Com.TxAntenna.Item1, Config.SourceFreq);
                    }
                    else // Tracking
                    {
                        Com.SdrDevice.SetFrequency(Direction.Tx, Com.TxAntenna.Item1, Com.SdrDevice.GetFrequency(Direction.Rx, Com.RxAntenna.Item1));
                        _whiteNoise = GenerateWhiteNoise((int)Com.RxSampleRate);
                    }
                    isTxEnabled = sourceMode != 0;
                    isTracking = sourceMode == 1;
                }

                if (isTxEnabled)
                {
                    fixed (float* bufferPtr = _whiteNoise)
                    {
                        transmissionStream?.Write((nint)bufferPtr, (uint)transmissionRate, StreamFlags.None, 0, 10_000_000, out results);
                    }
                }
            }
        });
    }

    private unsafe void IqSampler()
    {
        var dcBlock = new IqdcBlocker();
        double sampleRate = 0.0, frequency = 0.0;

        Com.SdrDevice.SetAntenna(Direction.Rx, Com.RxAntenna.Item1, Com.RxAntenna.Item2);
        var rxstream = Com.SdrDevice.SetupRxStream(StreamFormat.ComplexFloat32, new[] { Com.RxAntenna.Item1 }, "");
        rxstream.Activate();

        var mtu = rxstream.MTU;
        var results = new StreamResult();
        var floatBuffer = new float[mtu * 2];
        var sw = new Stopwatch();

        while (IsRunning)
        {
            if (sampleRate != Com.RxSampleRate)
            {
                sampleRate = Com.RxSampleRate;
                Com.SdrDevice.SetSampleRate(Direction.Rx, Com.RxAntenna.Item1, sampleRate);
                ResetIqFilter();
            }

            var noHopping = Config.FreqStop - Config.FreqStart <= sampleRate;
            var fCenter = Config.FreqInterleaving
                ? Config.FreqStart + sampleRate / 2 - sampleRate
                : Config.FreqStart + sampleRate / 2;

            var stillHopping = new Func<bool>(() =>
            {
                if (!IsRunning) return false;
                if (ResetData)
                {
                    if (_fftQueue.Count == 0) ResetData = false;
                    return false;
                }
                return Config.FreqInterleaving
                    ? fCenter - sampleRate / 2 < Config.FreqStop + sampleRate
                    : fCenter - sampleRate / 2 < Config.FreqStop;
            });

            for (; stillHopping(); fCenter += _hopSize)
            {
                if (frequency != fCenter)
                {
                    frequency = fCenter;
                    Com.SdrDevice.SetFrequency(Direction.Rx, Com.RxAntenna.Item1, frequency);
                    if (Config.SourceMode == 1)
                    {
                        Com.SdrDevice.SetFrequency(Direction.Tx, Com.TxAntenna.Item1, frequency);
                    }

                    sw.Restart();
                    while (sw.ElapsedMilliseconds < Config.LeakageSleep && IsRunning)
                    {
                        fixed (float* bufferPtr = floatBuffer)
                        {
                            rxstream.Read((nint)bufferPtr, (uint)mtu, 10_000_000, out results);
                        }
                        Thread.Sleep(0);
                    }
                }

                var fftsize = _fftSize;
                var samples = new Complex[fftsize];
                var totalSamples = 0;

                while (totalSamples < fftsize && IsRunning)
                {
                    fixed (float* bufferPtr = floatBuffer)
                    {
                        var errorCode = rxstream.Read((nint)bufferPtr, (uint)mtu, 10_000_000, out results);
                        if (errorCode is not ErrorCode.None || results is null) continue;
                    }

                    var length = (int)Math.Min(mtu, results.NumSamples);
                    for (var i = 0; i < length * 2 && totalSamples < fftsize; i += 2)
                    {
                        samples[totalSamples] = new Complex(floatBuffer[i], floatBuffer[i + 1]);
                        totalSamples++;
                    }
                }

                if (_fftQueue.Any(x => x.Item1 == frequency)) continue;

                if (Config.IqCorrection)
                {
                    CorrectIq(samples);
                    dcBlock.ProcessSignal(samples);
                }

                _fftQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, samples, sampleRate));
            }

            sw.Restart();
            while ((sw.ElapsedMilliseconds < Config.RefreshRate || ResetData) && IsRunning)
            {
                fixed (float* bufferPtr = floatBuffer)
                {
                    rxstream.Read((nint)bufferPtr, (uint)mtu, 10_000_000, out results);
                }
                if (ResetData && _fftQueue.Count == 0)
                {
                    graphHandle.ClearPlotData();
                    ResetData = false;
                    break;
                }
                Thread.Sleep(0);
            }
        }
        rxstream.Deactivate();
        rxstream.Close();
    }
}