using FFTW.NET;
using MathNet.Numerics;
using MathNet.Numerics.Random;
using NLog;
using Pothosware.SoapySDR;
using SharpGen.Runtime;
using SoapySA.View;
using SoapyVNACommon.Extentions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Logger = NLog.Logger;

namespace SoapySA;

public class PerformFft(MainWindowView initiator)
{
    //FFT Queue
    private readonly ConcurrentQueue<Tuple<double, Complex[], double>> _fftQueue = new();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    //https://github.com/ghostop14/gr-correctiq
    private readonly double _ratio = 1e-05f;

    private double _avgReal, _avgImg, _hopSize;

    private int _fftSize = 4096;
    private readonly List<Task> _fftTasks = new();
    public bool IsRunning, ResetData;
    private readonly MainWindowView _parent = initiator;

    public void BeginFft()
    {
        if (IsRunning) return;
        IsRunning = true;
        _fftTasks.Clear();
        _fftTasks.Add(Task.Run(() => { FFT_POOL(); }));
        _fftTasks.Add(Task.Run(() => { IqSampler(); }));
        _fftTasks.Add(Task.Run(() => { sourceWriter(); }));
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
    //CalculateFrequency(i, next.Item3, fft_size, next.Item1);
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

            // Calculate normalization factor for window (Hanning, Hamming, etc.)
            var window = Window.FlatTop(segmentLength);

            double sumW2 = 0;
            for (var i = 0; i < segmentLength; i++) sumW2 += window[i] * window[i];
            var scale = segmentLength * sampleRate * sumW2;
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
                    psd[0][k] +=
                        (float)(bufferOutput[k].MagnitudeSquared() / scale); // Normalize by window and segment length
            }

            // Average over segments and convert to dBm if needed
            var calibration = 0.0f;
            if (_parent.CalibrationView.calibrationData is {})
                 calibration = _parent.CalibrationView.calibrationData.OrderBy(x => Math.Abs(x.Item1 - center)).First().Item2;

            // Convert to dBm
            for (var k = 0; k < segmentLength; k++)
            {
                // Convert the power to dBm (if applicable)
                psd[0][k] /= numSegments;
                psd[0][k] = (float)(10 * Math.Log10(psd[0][k])) + calibration;

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

            if ((bool)_parent.Configuration.Config[Configuration.SaVar.FreqInterleaving])
            {
                var newpsd = new float[psd[0].Length / 2];
                var newpsdFreq = new float[psd[1].Length / 2];
                for (var i = 0; i < psd[0].Length / 4; i++)
                {
                    newpsd[i] = psd[0][psd[0].Length / 8 + i]; //first interleaved

                    newpsd[i + newpsd.Length / 2] =
                        psd[0][psd[0].Length / 8 + i + psd[0].Length / 2]; //second interleaved

                    newpsdFreq[i] = psd[1][psd[1].Length / 8 + i]; //first interleaved
                    newpsdFreq[i + newpsdFreq.Length / 2] =
                        psd[1][psd[1].Length / 8 + i + psd[1].Length / 2]; //second interleaved
                    //Console.WriteLine($"freq: {newpsdFreq[i]}, dBm: {newpsd[i]}");
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
        var rbw = (double)_parent.Configuration.Config[Configuration.SaVar.FftRbw];
        var numberOfSegments = (int)_parent.Configuration.Config[Configuration.SaVar.FftSegment];
        var desiredSegmentLength = _parent.DeviceView.DeviceCom.RxSampleRate / rbw;
        var desiredfftLength = desiredSegmentLength * numberOfSegments;
        _fftSize = (int)Math.Pow(2, (int)Math.Ceiling(Math.Log(desiredfftLength, 2)));

        _logger.Info($"RBW {rbw} FFTSIZE {_fftSize}");
    }

    public void ResetIqFilter()
    {
        _avgReal = 0;
        _avgImg = 0;
        if (_parent.DeviceView.DeviceCom.SdrDevice == null) return;

        CalculateRbwvbw();
        var sampleRate = _parent.DeviceView.DeviceCom.RxSampleRate;
        _hopSize = (bool)_parent.Configuration.Config[Configuration.SaVar.FreqInterleaving]
            ? sampleRate / 4.0
            : sampleRate;
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

    private void FFT_POOL()
    {
        var fftwArrayInput = new FftwArrayComplex(1024);
        var fftwArrayOuput = new FftwArrayComplex(1024);
        var fftwPlanContext = FftwPlanC2C.Create(fftwArrayInput, fftwArrayOuput, DftDirection.Forwards);

        while (IsRunning)
        {
            Tuple<double, Complex[], double>? next;
            if (!_fftQueue.TryDequeue(out next))
            {
                Thread.Sleep(1);
                continue;
            }

            var fftSamples = next.Item2;
            var fftSize = next.Item2.Length;
            var segmentLength =
                Math.Max(1, fftSize / (int)_parent.Configuration.Config[Configuration.SaVar.FftSegment]);
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

            var overlap = (int)(segmentLength * (double)_parent.Configuration.Config[Configuration.SaVar.FftOverlap]);
            var stepSize = segmentLength - overlap; // Step size
            var numSegments = (fftSize - overlap) / stepSize; // Number of segments

            var psd = WelchPsd(fftSamples, fftwArrayInput, fftwArrayOuput, fftwPlanContext, segmentLength, overlap,
                (float)next.Item3, (float)next.Item1);
            if (psd != null)
                _parent.GraphView.UpdateData(psd);
        }
    }
    private static  int transmissionRate = 4096;
    static  float[] _whiteNoise = GenerateWhiteNoise(transmissionRate);
    private static TxStream? transmissionStream;
    private unsafe void sourceWriter()
    {
        Task.Run(() =>
        {

            bool isTxEnabled = false;
            bool isTracking = false;
            var sourceMode = ((int)_parent.Configuration.Config[Configuration.SaVar.SourceMode]);
            var results = new StreamResult();
            while (IsRunning)
            {
                //the code here is very critical under sampling of tracking, therfore we put all in one big if statement to only enter this code
                //in the case of a change in the sourceMode
                if (sourceMode != ((int)_parent.Configuration.Config[Configuration.SaVar.SourceMode]))
                {
                    sourceMode = ((int)_parent.Configuration.Config[Configuration.SaVar.SourceMode]);
                    
                    if (sourceMode != 0 && !isTxEnabled) //changed to enabled
                    {
                        if (transmissionStream is null)
                        {
                            transmissionStream = _parent.DeviceView.DeviceCom.SdrDevice.SetupTxStream(StreamFormat.ComplexFloat32,
                            new[] { _parent.DeviceView.DeviceCom.TxAntenna.Item1 }, "");
                            _parent.DeviceView.DeviceCom.SdrDevice.SetSampleRate(Direction.Tx,
                           _parent.DeviceView.DeviceCom.TxAntenna.Item1, _parent.DeviceView.DeviceCom.RxSampleRate);

                        }
                        transmissionStream.Activate();
                        
                        _logger.Info(
                         $"Begining transmitting {{MTU: {transmissionStream?.MTU}");
                    }
                    else if (sourceMode == 0 && isTxEnabled) //changed to disabled
                    {
                        transmissionStream?.Deactivate();
                        _logger.Info(
                    $"stopped transmitting");
                    }
                    if (sourceMode == 2) //changed to CW
                    {
                        transmissionRate = 4096;
                        _whiteNoise = Enumerable.Range(0, 4096 * 2).Select(x => x % 2 == 0 ? 1.0f : 0.0f).ToArray(); //I = 1, Q = 0 ==> DC constant CW
                        _parent.DeviceView.DeviceCom.SdrDevice.SetFrequency(Direction.Tx,
                         _parent.DeviceView.DeviceCom.TxAntenna.Item1, (double)_parent.Configuration.Config[Configuration.SaVar.sourceFreq]);
                    }
                    else //changed to tracking
                    {
                        //if it changed to tracking, but the BW is so low that there is no requirement for hopping, i must change lo here
                        //as there is no hopping call
                        _parent.DeviceView.DeviceCom.SdrDevice.SetFrequency(Direction.Tx,
                         _parent.DeviceView.DeviceCom.TxAntenna.Item1, _parent.DeviceView.DeviceCom.SdrDevice.GetFrequency(Direction.Rx,
                          _parent.DeviceView.DeviceCom.RxAntenna.Item1));

                        transmissionRate = (int)transmissionStream.MTU;
                        _whiteNoise = GenerateWhiteNoise(transmissionRate);
                        
                    }
                    isTxEnabled = sourceMode != 0;
                    isTracking = sourceMode == 1;
                }
                if(isTxEnabled)
                fixed (float* bufferPtr = _whiteNoise)
                {
                    
                        var errorCode = transmissionStream?.Write((nint)bufferPtr, (uint)transmissionRate, StreamFlags.None, 0, 10_000_000,
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
    }
    private unsafe void IqSampler()
    {
        var dcBlock = new IqdcBlocker();
        double sampleRate = 0.0, frequency = 0.0;
        _parent.DeviceView.DeviceCom.SdrDevice.SetAntenna(Direction.Rx, _parent.DeviceView.DeviceCom.RxAntenna.Item1,
            _parent.DeviceView.DeviceCom.RxAntenna.Item2);
        var rxstream = _parent.DeviceView.DeviceCom.SdrDevice.SetupRxStream(StreamFormat.ComplexFloat32,
            new[] { _parent.DeviceView.DeviceCom.RxAntenna.Item1 }, "");
        rxstream.Activate();
        var mtu = rxstream.MTU;
        var results = new StreamResult();
        var floatBuffer = new float[mtu * 2];
        var bufferHandle = GCHandle.Alloc(floatBuffer, GCHandleType.Pinned);
        _logger.Info(
            $"Begining Sampling {{MTU: {rxstream.MTU}, FFT length: {_fftSize}, SPS: {_parent.DeviceView.DeviceCom.RxSampleRate}}}");
        var sw = new Stopwatch();

        while (IsRunning)
        {
            if (sampleRate != _parent.DeviceView.DeviceCom.RxSampleRate)
            {
                sampleRate = _parent.DeviceView.DeviceCom.RxSampleRate;
                _parent.DeviceView.DeviceCom.SdrDevice.SetSampleRate(Direction.Rx,
                    _parent.DeviceView.DeviceCom.RxAntenna.Item1, sampleRate);
                ResetIqFilter();
            }

            var noHopping =
                (double)_parent.Configuration.Config[Configuration.SaVar.FreqStop] -
                (double)_parent.Configuration.Config[Configuration.SaVar.FreqStart] <= sampleRate;
            var fCenter = (bool)_parent.Configuration.Config[Configuration.SaVar.FreqInterleaving]
                ? (double)_parent.Configuration.Config[Configuration.SaVar.FreqStart] + sampleRate / 2 - sampleRate
                : (double)_parent.Configuration.Config[Configuration.SaVar.FreqStart] + sampleRate / 2;
            var stillHopping = new Func<bool>(() =>
            {
                if (!IsRunning)
                    return false;
                if (ResetData)
                {
                    if (_fftQueue.Count == 0)
                    {
                        ResetData = false;
                        _parent.GraphView.ClearPlotData();
                    }

                    return false;
                }

                if ((bool)_parent.Configuration.Config[Configuration.SaVar.FreqInterleaving])
                    return fCenter - sampleRate / 2 <
                           (double)_parent.Configuration.Config[Configuration.SaVar.FreqStop] + sampleRate;

                return fCenter - sampleRate / 2 < (double)_parent.Configuration.Config[Configuration.SaVar.FreqStop];
            });
            for (;
                 stillHopping();
                 fCenter += _hopSize)
            {
                //some devices are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                if (frequency != fCenter)
                {
                    frequency = fCenter;
                    _parent.DeviceView.DeviceCom.SdrDevice.SetFrequency(Direction.Rx,
                        _parent.DeviceView.DeviceCom.RxAntenna.Item1, frequency);
                    if(((int)_parent.Configuration.Config[Configuration.SaVar.SourceMode]) == 1)
                    {
                        _parent.DeviceView.DeviceCom.SdrDevice.SetFrequency(Direction.Tx,
                        _parent.DeviceView.DeviceCom.TxAntenna.Item1, frequency);
                    }
                        sw.Restart();
                    while (sw.ElapsedMilliseconds <
                           (int)_parent.Configuration.Config[Configuration.SaVar.LeakageSleep] &&
                           IsRunning)
                    {
                        //reading while sleeping so no buffer overflow will happen
                        fixed (float* bufferPtr = floatBuffer)
                        {
                            Array.Clear(floatBuffer, 0, floatBuffer.Length);
                            var errorCode = rxstream.Read((nint)bufferPtr, (uint)mtu, 10_000_000, out results);
                            if (errorCode is not ErrorCode.None || results is null)
                            {
                                _logger.Error($"Readstream Error Code {errorCode}");
                                continue;
                            }
                        }

                        Thread.Sleep(0);
                    }
                }
                //fill up the iqbuffer to have enough samples for FFT

                var fftsize = _fftSize;
                var samples = new Complex[fftsize];
                var totalSamples = 0;

                while (totalSamples < fftsize && IsRunning)
                {
                    fixed (float* bufferPtr = floatBuffer)
                    {
                        var errorCode = rxstream.Read((nint)bufferPtr, (uint)mtu, 10_000_000, out results);

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

                    var length = (int)Math.Min(mtu * 2, results.NumSamples);
                    length = length / 2;
                    for (var i = 0; i < length && totalSamples < fftsize; i += 2)
                    {
                        samples[totalSamples] = new Complex(floatBuffer[i], floatBuffer[i + 1]);
                        totalSamples++;
                    }
                }

                //did it finish the same sampling yet?
                if (_fftQueue.Any(x => x.Item1 == frequency))
                    continue;
                var iqCorrectionSamples = samples.Take(fftsize).ToArray();
                if ((bool)_parent.Configuration.Config[Configuration.SaVar.IqCorrection])
                {
                    CorrectIq(iqCorrectionSamples);
                    dcBlock.ProcessSignal(iqCorrectionSamples);
                }

                _fftQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, iqCorrectionSamples, sampleRate));
            }

            sw.Restart();
            while ((sw.ElapsedMilliseconds < (int)_parent.Configuration.Config[Configuration.SaVar.RefreshRate]) |
                   ResetData &&
                   IsRunning)
            {
                //reading while sleeping so no buffer overflow will happen
                fixed (float* bufferPtr = floatBuffer)
                {
                    var errorCode = rxstream.Read((nint)bufferPtr, (uint)mtu, 10_000_000, out results);
                    if (errorCode is not ErrorCode.None || results is null)
                    {
#if DEBUG_VERBOSE
                                Logger.Error($"Readstream Error Code {errorCode}");
#endif
                        continue;
                    }
                }

                if (ResetData && _fftQueue.Count == 0)
                {
                    ResetData = false;
                    _parent.GraphView.ClearPlotData();
                    break;
                }

                Thread.Sleep(0);
            }
        }

        rxstream.Deactivate();
        rxstream.Close();
    }
}