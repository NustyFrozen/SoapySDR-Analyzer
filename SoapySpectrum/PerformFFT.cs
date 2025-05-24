using FFTW.NET;
using MathNet.Numerics;
using NLog;
using Pothosware.SoapySDR;
using SoapySA.View;
using SoapyVNACommon.Extentions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Logger = NLog.Logger;

namespace SoapySA;

public class PerformFFT(MainWindow initiator)
{
    private MainWindow parent = initiator;
    public bool isRunning, resetData;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    //FFT Queue
    private readonly ConcurrentQueue<Tuple<double, Complex[], double>> FFTQueue = new();

    private int FFT_size = 4096;

    //https://github.com/ghostop14/gr-correctiq
    private readonly double ratio = 1e-05f;

    private double avg_real, avg_img, hopSize;
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

            // Average over segments and convert to dBm if needed
            var calibration = 0.0f;
            //if (tab_Cal.s_currentCalData.Count > 0)
            //     calibration = tab_Cal.s_currentCalData.OrderBy(x => Math.Abs(x.frequency - center)).First().results;

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
                    frequency = k * sample_rate / segmentLength + center;
                else
                    // Negative frequencies with center frequency offset
                    frequency = (k - segmentLength) * sample_rate / segmentLength + center;

                psd[1][k] = frequency;
            }
            if ((bool)parent.Configuration.config[Configuration.saVar.freqInterleaving])
            {
                var newpsd = new float[psd[0].Length / 2];
                var newpsdFreq = new float[psd[1].Length / 2];
                for (int i = 0; i < psd[0].Length / 4; i++)
                {
                    newpsd[i] = psd[0][psd[0].Length / 8 + i]; //first interleaved

                    newpsd[i + newpsd.Length / 2] = psd[0][psd[0].Length / 8 + i + psd[0].Length / 2]; //second interleaved

                    newpsdFreq[i] = psd[1][psd[1].Length / 8 + i]; //first interleaved
                    newpsdFreq[i + newpsdFreq.Length / 2] = psd[1][psd[1].Length / 8 + i + psd[1].Length / 2]; //second interleaved
                    //Console.WriteLine($"freq: {newpsdFreq[i]}, dBm: {newpsd[i]}");
                }
                psd[0] = newpsd;
                psd[1] = newpsdFreq;
            }
            return psd;
        }
        catch (Exception ex)
        {
            Logger.Error($"FFT ERROR {ex.Message} {ex.StackTrace}");
            return null;
        }
    }

    private void calculateRBWVBW()
    {
        var rbw = (double)parent.Configuration.config[Configuration.saVar.fftRBW];
        var numberOfSegments = (int)parent.Configuration.config[Configuration.saVar.fftSegment];
        var desiredSegmentLength = parent.tab_Device.deviceCOM.rxSampleRate / rbw;
        var desiredfftLength = desiredSegmentLength * numberOfSegments;
        FFT_size = (int)Math.Pow(2, (int)Math.Ceiling(Math.Log(desiredfftLength, 2)));

        Logger.Info($"RBW {rbw} FFTSIZE {FFT_size}");
    }

    public void resetIQFilter()
    {
        avg_real = 0;
        avg_img = 0;
        if (parent.tab_Device.deviceCOM.sdrDevice == null) return;

        calculateRBWVBW();
        var sampleRate = (double)parent.tab_Device.deviceCOM.rxSampleRate;
        hopSize = ((bool)parent.Configuration.config[Configuration.saVar.freqInterleaving]) ? sampleRate / 4.0 : sampleRate;
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
            var segmentLength = Math.Max(1, fft_size / (int)parent.Configuration.config[Configuration.saVar.fftSegment]);
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

            var overlap = (int)(segmentLength * (double)parent.Configuration.config[Configuration.saVar.fftOverlap]);
            var stepSize = segmentLength - overlap; // Step size
            var numSegments = (fft_size - overlap) / stepSize; // Number of segments

            var psd = WelchPSD(fft_samples, fftwArrayInput, fftwArrayOuput, fftwPlanContext, segmentLength, overlap,
                (float)next.Item3, (float)next.Item1);
            if (psd != null)
                parent.Graph.updateData(psd);
        }
    }

    private unsafe void IQSampler()
    {
        IQDCBlocker dcBlock = new IQDCBlocker();
        double sample_rate = 0.0, frequency = 0.0;
        parent.tab_Device.deviceCOM.sdrDevice.SetAntenna(Direction.Rx, parent.tab_Device.deviceCOM.rxAntenna.Item1, parent.tab_Device.deviceCOM.rxAntenna.Item2);

        var stream = parent.tab_Device.deviceCOM.sdrDevice.SetupRxStream(StreamFormat.ComplexFloat32, new uint[] { parent.tab_Device.deviceCOM.rxAntenna.Item1 }, "");
        stream.Activate();
        var MTU = stream.MTU;
        var results = new StreamResult();
        var floatBuffer = new float[MTU * 2];
        var bufferHandle = GCHandle.Alloc(floatBuffer, GCHandleType.Pinned);
        Logger.Info($"Begining Sampling {{MTU: {stream.MTU}, FFT length: {FFT_size}, SPS: {parent.tab_Device.deviceCOM.rxSampleRate}}}");
        var sw = new Stopwatch();

        while (isRunning)
        {
            if (sample_rate != (double)parent.tab_Device.deviceCOM.rxSampleRate)
            {
                sample_rate = (double)parent.tab_Device.deviceCOM.rxSampleRate;
                parent.tab_Device.deviceCOM.sdrDevice.SetSampleRate(Direction.Rx, parent.tab_Device.deviceCOM.rxAntenna.Item1, sample_rate);
                resetIQFilter();
            }

            var noHopping =
                (double)parent.Configuration.config[Configuration.saVar.freqStop] -
                (double)parent.Configuration.config[Configuration.saVar.freqStart] <= sample_rate;
            var f_center = ((bool)parent.Configuration.config[Configuration.saVar.freqInterleaving]) ?
                (double)parent.Configuration.config[Configuration.saVar.freqStart] + sample_rate / 2 - sample_rate
                :
                (double)parent.Configuration.config[Configuration.saVar.freqStart] + sample_rate / 2;
            Func<bool> stillHopping = new Func<bool>(() =>
            {
                if (!isRunning)
                    return false;
                if (resetData)
                {
                    if (FFTQueue.Count == 0)
                    {
                        resetData = false;
                        parent.Graph.clearPlotData();
                    }
                    return false;
                }
                if ((bool)parent.Configuration.config[Configuration.saVar.freqInterleaving])
                {
                    return f_center - sample_rate / 2 < (double)parent.Configuration.config[Configuration.saVar.freqStop] + sample_rate;
                }
                else
                    return f_center - sample_rate / 2 < (double)parent.Configuration.config[Configuration.saVar.freqStop];
            });
            for (;
                 stillHopping();
                 f_center += hopSize)
            {
                //some devices are slow with hopping so it is preferable if we sample without hopping (just the span of the sample rate) we wont call setFrequency as it will slow the algorithm
                if (frequency != f_center)
                {
                    frequency = f_center;
                    parent.tab_Device.deviceCOM.sdrDevice.SetFrequency(Direction.Rx, parent.tab_Device.deviceCOM.rxAntenna.Item1, frequency);
                    sw.Restart();
                    while (sw.ElapsedMilliseconds < (int)parent.Configuration.config[Configuration.saVar.leakageSleep] &&
                           isRunning)
                    {
                        //reading while sleeping so no buffer overflow will happen
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
                if ((bool)parent.Configuration.config[Configuration.saVar.iqCorrection])
                {
                    correctIQ(IQCorrectionSamples);
                    dcBlock.ProcessSignal(IQCorrectionSamples);
                }

                FFTQueue.Enqueue(new Tuple<double, Complex[], double>(frequency, IQCorrectionSamples, sample_rate));
            }

            sw.Restart();
            while ((sw.ElapsedMilliseconds < (Int32)parent.Configuration.config[Configuration.saVar.refreshRate]) | resetData &&
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
                    parent.Graph.clearPlotData();
                    break;
                }

                Thread.Sleep(0);
            }
        }

        stream.Deactivate();
        stream.Close();
    }
}