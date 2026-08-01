using System.Diagnostics;
using NLog;
using Pothosware.SoapySDR;
using SoapyRTSA.Extentions;
using SoapyRTSA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using Logger = NLog.Logger;

namespace SoapyRTSA;

/// <summary>
///     The sweep. Two threads, each pinned to a core of its own: one drains the radio into a ring buffer,
///     the other slides overlapping transforms along that ring and stamps every result into the
///     persistence grid. Nothing in either loop allocates - all working memory is taken once, up front,
///     and re-taken only when the fft size changes - so the display never stutters on a collection.
/// </summary>
public sealed class PerformRtsa
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Grid geometry. Fixed, so the ui and the sweep never argue about dimensions.</summary>
    public const int Columns = 640;
    public const int DensityRows = 256;
    public const int WaterfallRows = 160;

    /// <summary>1M samples of slack between the radio and the transforms, about 50 ms at 20 MS/s.</summary>
    private const int RingSampleCount = 1 << 20;
    private const int RingMask = RingSampleCount - 1;

    private const int ReadTimeoutUs = 200_000;

    private readonly Configuration _config;
    private readonly SdrDeviceCom _com;

    #region shared buffers

    /// <summary>Interleaved IQ, written by the sampler and read by the transform thread.</summary>
    private readonly float[] _ring = new float[RingSampleCount * 2];

    private long _written; //samples handed over by the radio
    private long _read; //samples already transformed

    public DensityGrid Grid { get; } = new(Columns, DensityRows);
    public WaterfallBuffer Waterfall { get; } = new(Columns, WaterfallRows);

    #endregion shared buffers

    #region transform state, owned by the fft thread

    private FftTransform _transform;
    private float[] _window;
    private float[] _db;

    /// <summary>Display column each bin lands in, or -1 when the bin falls outside the span.</summary>
    private int[] _binToColumn = new int[1024];

    /// <summary>Mask level per bin, +inf where the mask does not reach so it can never trigger.</summary>
    private float[] _maskDb = new float[1024];

    /// <summary>Peak dB per column since the last waterfall row was pushed.</summary>
    private readonly float[] _waterfallPending = new float[Columns];

    private int _size = 1024;
    private int _hop = 256;
    private float _windowScale = 1.0f;
    private int _maskRevision = -1;

    private int _binsPerColumn = 1;
    private int _activeColumns = Columns;
    private int _firstShiftedBin;

    /// <summary>
    ///     Columns actually fed by the transform. Fewer than <see cref="Columns" /> whenever the bins do not
    ///     divide evenly; the renderer stretches these across the full width.
    /// </summary>
    public int ActiveColumns => _activeColumns;

    /// <summary>Exact frequency the leftmost column starts at, which is what the axis is labelled from.</summary>
    public double DisplayStartHz { get; private set; }

    public double DisplayStopHz { get; private set; }

    #endregion transform state, owned by the fft thread

    #region lifecycle

    private volatile bool _running;
    private volatile bool _reconfigure = true;
    private volatile bool _retune = true;

    private Thread? _sampler;
    private Thread? _transformer;
    private RxStream? _stream;

    #endregion lifecycle

    #region statistics

    public long Frames;
    public long Displayed;
    public long Dropped;
    public long Overruns;
    public double FramesPerSecond;
    public double SweepMegasamplesPerSecond;

    #endregion statistics

    private static readonly double SecondsPerTick = 1.0 / Stopwatch.Frequency;
    private static readonly double MicrosecondsPerTick = 1e6 / Stopwatch.Frequency;

    public PerformRtsa(IWidget widget, Configuration config, SdrDeviceCom com)
    {
        _config = config;
        _com = com;

        //plan for the size the preset asks for, so entering the widget does not pay for a plan twice
        _size = ClampSize(config.FftSize);
        _transform = new FftTransform(_size);
        _window = new float[_size];
        _db = new float[_size];
        _binToColumn = new int[_size];
        _maskDb = new float[_size];
        Logger.Info($"RTSA transform engine: {_transform.EngineName}");

        widget.OnWidgetEnter += (_, _) => Begin();
        widget.OnWidgetExit += (_, _) => Stop();

        config.OnConfigLoadBegin += (_, _) => Stop();
        config.OnConfigLoadEnd += (_, _) => Begin();
        config.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(Configuration.FftSize):
                case nameof(Configuration.OverlapPercent):
                case nameof(Configuration.Window):
                case nameof(Configuration.Span):
                case nameof(Configuration.CenterFrequency):
                case nameof(Configuration.RefLevelDb):
                case nameof(Configuration.RangeDb):
                    _reconfigure = true;
                    break;

                case nameof(Configuration.SampleRate):
                    _retune = true;
                    _reconfigure = true;
                    break;
            }

            if (e.PropertyName == nameof(Configuration.CenterFrequency))
                _retune = true;
        };
    }

    /// <summary>Effective rate: the configured one, or whatever the device came up at.</summary>
    private double SampleRate => _config.SampleRate > 0 ? _config.SampleRate : _com.RxSampleRate;

    /// <summary>Span actually displayed, never wider than the instantaneous bandwidth.</summary>
    public double EffectiveSpan
    {
        get
        {
            var rate = SampleRate;
            var span = _config.Span;
            return span <= 0.0 || span > rate ? rate : span;
        }
    }

    public bool IsRunning => _running;

    /// <summary>
    ///     Counter value a cell that is occupied by <b>every</b> frame settles at.
    ///     <para>
    ///         Hits land at the frame rate and decay with a time constant of FadeSeconds/ln(100), so the
    ///         steady state is rate * tau. Dividing the grid by it turns a raw counter into the fraction of
    ///         recent frames that cell was occupied, which is the whole point: normalising against the
    ///         busiest cell instead would peg the noise floor at full red and leave a rare burst - one hit
    ///         against tens of thousands - mathematically invisible. Against a fixed reference the burst is a
    ///         faint blue ghost that fades out over the fade time, and a red cell that stops being refreshed
    ///         cools through yellow and green on its way out rather than staying red until it vanishes.
    ///     </para>
    /// </summary>
    public float DensityReference
    {
        get
        {
            var frames = FramesPerSecond < 1.0 ? 1.0 : FramesPerSecond;
            var tau = _config.FadeSeconds / 4.60517; //ln(100), matching DensityGrid.DecayFactor
            var reference = frames * tau;

            return reference < 1.0 ? 1.0f : (float)reference;
        }
    }

    public void Begin()
    {
        if (_running)
            return;

        if (SampleRate <= 0.0)
        {
            Logger.Error("RTSA cannot start: the device has no sample rate");
            return;
        }

        _running = true;
        _reconfigure = true;
        _retune = true;

        _sampler = new Thread(SamplerLoop)
        {
            IsBackground = true,
            Name = "rtsa-sampler",
            Priority = ThreadPriority.Highest
        };
        _transformer = new Thread(TransformLoop)
        {
            IsBackground = true,
            Name = "rtsa-fft",
            Priority = ThreadPriority.Highest
        };

        _sampler.Start();
        _transformer.Start();
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;

        //a read in flight holds on for at most the timeout above
        if (_sampler is { } sampler && !sampler.Join(ReadTimeoutUs / 1000 + 1500))
            Logger.Warn("RTSA sampler did not stop in time, its stream will be dropped");

        _transformer?.Join(2000);
        _sampler = null;
        _transformer = null;
    }

    #region sampler

    private unsafe void SamplerLoop()
    {
        if (_config.PinCores)
            ThreadTuning.Pin("rtsa-sampler", ThreadTuning.CoreFromTop(0));
        ThreadTuning.Prioritise("rtsa-sampler");

        var channel = _com.RxAntenna.Item1;

        try
        {
            _com.SdrDevice.SetAntenna(Direction.Rx, channel, _com.RxAntenna.Item2);
            var streamArgs = _config.StreamArgs ?? string.Empty;
            Logger.Info($"RTSA opening RX stream (args \"{streamArgs}\")");
            _stream = _com.SdrDevice.SetupRxStream(StreamFormat.ComplexFloat32, new[] { channel }, streamArgs);
            _stream.Activate();
        }
        catch (Exception exception)
        {
            Logger.Error($"RTSA could not open the RX stream -> {exception.Message}");
            _running = false;
            return;
        }

        var mtu = (int)_stream.MTU;
        if (mtu <= 0)
            mtu = 4096;

        //taken once: the read loop below must not allocate
        var staging = new float[mtu * 2];
        var appliedRate = 0.0;
        var appliedFrequency = 0.0;

        fixed (float* buffer = staging)
        {
            while (_running)
            {
                if (_retune)
                {
                    _retune = false;
                    appliedRate = Retune(channel, appliedRate, ref appliedFrequency);
                }

                //one StreamResult per read is the binding's doing, and the only garbage in the sweep
                var code = _stream.Read((nint)buffer, (uint)mtu, ReadTimeoutUs, out var result);
                if (code != ErrorCode.None || result is null)
                {
                    if (code == ErrorCode.Overflow)
                        Interlocked.Increment(ref Overruns);
                    continue;
                }

                var samples = (int)result.NumSamples;
                if (samples <= 0)
                    continue;

                if (samples > mtu)
                    samples = mtu;

                Publish(staging, samples);
            }
        }

        try
        {
            _stream.Deactivate();
            _stream.Close();
        }
        catch (Exception exception)
        {
            Logger.Warn($"RTSA stream close -> {exception.Message}");
        }

        _stream = null;
    }

    /// <summary>Applies rate and centre frequency, which only the sampler thread is allowed to touch.</summary>
    private double Retune(uint channel, double appliedRate, ref double appliedFrequency)
    {
        var rate = SampleRate;

        try
        {
            if (rate > 0.0 && Math.Abs(rate - appliedRate) > 0.5)
            {
                _com.SdrDevice.SetSampleRate(Direction.Rx, channel, rate);
                appliedRate = rate;
            }
        }
        catch (Exception exception)
        {
            Logger.Error($"device refused sample rate {rate} -> {exception.Message}");
            appliedRate = rate; //do not retry every read
        }

        try
        {
            var frequency = _config.CenterFrequency;
            if (frequency > 0.0 && Math.Abs(frequency - appliedFrequency) > 0.5)
            {
                _com.SdrDevice.SetFrequency(Direction.Rx, channel, frequency);
                appliedFrequency = frequency;
            }
        }
        catch (Exception exception)
        {
            Logger.Error($"device refused frequency {_config.CenterFrequency} -> {exception.Message}");
        }

        return appliedRate;
    }

    /// <summary>Copies a read into the ring, splitting it when it straddles the end.</summary>
    private void Publish(float[] staging, int samples)
    {
        var start = (int)(_written & RingMask);
        var firstRun = Math.Min(samples, RingSampleCount - start);

        staging.AsSpan(0, firstRun * 2).CopyTo(_ring.AsSpan(start * 2));
        if (firstRun < samples)
            staging.AsSpan(firstRun * 2, (samples - firstRun) * 2).CopyTo(_ring.AsSpan(0));

        Volatile.Write(ref _written, _written + samples);
    }

    #endregion sampler

    #region transform

    private void TransformLoop()
    {
        if (_config.PinCores)
            ThreadTuning.Pin("rtsa-fft", ThreadTuning.CoreFromTop(1));
        ThreadTuning.Prioritise("rtsa-fft");

        var lastFade = Stopwatch.GetTimestamp();
        var lastWaterfall = lastFade;
        var lastStats = lastFade;
        var framesAtLastStats = 0L;
        var starved = 0;

        Array.Fill(_waterfallPending, float.NegativeInfinity);

        while (_running)
        {
            if (_reconfigure)
                ApplyReconfigure();

            if (_maskRevision != _config.MaskRevision)
                RebuildMask();

            var written = Volatile.Read(ref _written);
            var available = written - _read;

            if (available < _size)
            {
                //spin while the radio is merely a window behind, but stop burning the core if it went quiet
                if (++starved < 2000)
                    Thread.SpinWait(40);
                else
                    Thread.Sleep(1);

                continue;
            }

            starved = 0;

            //if the transforms fell a ring behind, skip ahead instead of reading samples being overwritten
            if (available > RingSampleCount - _size)
            {
                _read = written - RingSampleCount / 2;
                Interlocked.Increment(ref Dropped);
            }

            var now = Stopwatch.GetTimestamp();

            LoadWindow(_read);
            _transform.Forward();
            SimdOps.PowerToDb(_transform.OutputReal, _transform.OutputImaginary, _db, _windowScale);

            if (Accepted(now))
            {
                Displayed++;
                if (!_config.Paused)
                    Accumulate();
            }

            _read += _hop;
            Frames++;

            if (!_config.Paused)
            {
                var fadeElapsed = (now - lastFade) * SecondsPerTick;
                if (fadeElapsed >= 0.016)
                {
                    Grid.Fade(DensityGrid.DecayFactor(fadeElapsed, _config.FadeSeconds));
                    lastFade = now;
                }

                var waterfallElapsed = (now - lastWaterfall) * SecondsPerTick;
                if (waterfallElapsed * 1000.0 >= _config.WaterfallIntervalMs)
                {
                    Waterfall.Push(_waterfallPending.AsSpan(0, _activeColumns),
                        (float)_config.DisplayBottomDb, (float)_config.DisplayTopDb);
                    Array.Fill(_waterfallPending, float.NegativeInfinity);
                    lastWaterfall = now;
                }
            }

            var statsElapsed = (now - lastStats) * SecondsPerTick;
            if (statsElapsed >= 0.5)
            {
                var frames = Frames - framesAtLastStats;
                FramesPerSecond = frames / statsElapsed;
                SweepMegasamplesPerSecond = frames * _hop / statsElapsed / 1e6;
                framesAtLastStats = Frames;
                lastStats = now;
            }
        }
    }

    /// <summary>
    ///     Rebuilds everything that depends on the fft size, the window or the span. Runs on the transform
    ///     thread because it owns these buffers, so no lock is needed anywhere in the hot loop.
    /// </summary>
    private void ApplyReconfigure()
    {
        _reconfigure = false;

        var size = ClampSize(_config.FftSize);

        if (size != _size || _transform.Size != size)
        {
            _size = size;

            //planning is the only slow part and it happens here, never in the loop
            _transform.Dispose();
            _transform = new FftTransform(size);

            _window = new float[size];
            _db = new float[size];
            _binToColumn = new int[size];
            _maskDb = new float[size];
        }

        BuildWindow();
        BuildBinMap();
        RebuildMask();

        var overlap = Math.Clamp(_config.OverlapPercent, 0, 95);
        _hop = Math.Max(1, _size * (100 - overlap) / 100);

        _read = Volatile.Read(ref _written);
        Grid.Clear();
        Waterfall.Clear();
        Array.Fill(_waterfallPending, float.NegativeInfinity);

        Logger.Info(
            $"RTSA sweep: {_size} point {_config.Window} on {_transform.EngineName}, {overlap}% overlap "
                + $"(hop {_hop}), {_activeColumns} columns x {_binsPerColumn} bins, "
                + $"{EffectiveSpan / 1e6:0.###} MHz span at {SampleRate / 1e6:0.###} MS/s"
        );
    }

    /// <summary>Nearest power of two the transform will accept.</summary>
    private static int ClampSize(int size) =>
        1 << (int)Math.Round(Math.Log2(Math.Clamp(size, 256, 8192)));

    /// <summary>Which transform is doing the work, for the display tab.</summary>
    public string EngineName => _transform.EngineName;

    private void BuildWindow()
    {
        var n = _size;
        double sumSquares = 0.0;

        for (var i = 0; i < n; i++)
        {
            var x = 2.0 * Math.PI * i / (n - 1);
            var value = _config.Window switch
            {
                FftWindowType.Rectangular => 1.0,
                FftWindowType.Hann => 0.5 - 0.5 * Math.Cos(x),
                FftWindowType.Hamming => 0.54 - 0.46 * Math.Cos(x),
                FftWindowType.FlatTop => 0.21557895 - 0.41663158 * Math.Cos(x) + 0.277263158 * Math.Cos(2 * x)
                    - 0.083578947 * Math.Cos(3 * x) + 0.006947368 * Math.Cos(4 * x),
                _ => 0.35875 - 0.48829 * Math.Cos(x) + 0.14128 * Math.Cos(2 * x) - 0.01168 * Math.Cos(3 * x)
            };

            _window[i] = (float)value;
            sumSquares += value * value;
        }

        //the same power spectral density scaling the spectrum widget uses: |X|^2 / (N * fs * sum(w^2)).
        //dividing by the window's power gain rather than its coherent gain is what keeps the noise floor
        //reading the same level whichever window is picked, and keeps both widgets agreeing on a signal
        var rate = SampleRate;
        var denominator = _size * (rate <= 0.0 ? 1.0 : rate) * (sumSquares <= 0.0 ? 1.0 : sumSquares);
        _windowScale = (float)(1.0 / denominator);
    }

    /// <summary>
    ///     Maps every fft bin onto a display column, undoing the transform's wrap so negative frequencies
    ///     sit left of centre.
    /// </summary>
    private void BuildBinMap()
    {
        PlanColumns(_size, SampleRate, EffectiveSpan, out _firstShiftedBin, out _binsPerColumn, out _activeColumns);

        var covered = _activeColumns * _binsPerColumn;
        var binHz = SampleRate / _size;

        DisplayStartHz = _config.CenterFrequency + (_firstShiftedBin - _size / 2) * binHz;
        DisplayStopHz = DisplayStartHz + covered * binHz;

        for (var bin = 0; bin < _size; bin++)
            _binToColumn[bin] = ColumnForBin(bin, _size, _firstShiftedBin, _binsPerColumn, covered);
    }

    /// <summary>
    ///     Works out how the in-span bins divide across the display.
    ///     <para>
    ///         Every column has to be fed the <b>same</b> number of bins. Spreading, say, 1024 bins over 640
    ///         columns hands some columns two bins and some one, and that alone paints a periodic comb over
    ///         the whole span: the two-bin columns collect twice the hits and the higher of two noise
    ///         samples. So the bins per column are rounded up to a whole number and only as many columns as
    ///         that evenly fills are used, stretched across the full width by the renderer.
    ///     </para>
    /// </summary>
    public static void PlanColumns(
        int size,
        double rate,
        double span,
        out int firstShiftedBin,
        out int binsPerColumn,
        out int activeColumns
    )
    {
        firstShiftedBin = 0;
        binsPerColumn = 1;
        activeColumns = 1;

        if (size <= 0 || rate <= 0.0 || span <= 0.0)
            return;

        var half = size / 2;
        var binHz = rate / size;

        //shifted index 0 is the most negative frequency, so the span maps to a contiguous run
        var first = (int)Math.Ceiling(half - span / 2.0 / binHz);
        var last = (int)Math.Floor(half + span / 2.0 / binHz);

        if (first < 0)
            first = 0;
        if (last > size - 1)
            last = size - 1;

        var inSpan = last - first + 1;
        if (inSpan < 1)
            inSpan = 1;

        firstShiftedBin = first;
        binsPerColumn = (inSpan + Columns - 1) / Columns; //round up so no column is short changed
        activeColumns = Math.Clamp(inSpan / binsPerColumn, 1, Columns);
    }

    /// <summary>
    ///     Column a single bin lands in, or -1 when it falls outside what the columns cover. Bins past the
    ///     halfway point carry negative frequencies, which is what puts them left of centre.
    /// </summary>
    public static int ColumnForBin(int bin, int size, int firstShiftedBin, int binsPerColumn, int coveredBins)
    {
        if (size <= 0 || binsPerColumn <= 0)
            return -1;

        var half = size / 2;
        var shifted = bin < half ? bin + half : bin - half;
        var offset = shifted - firstShiftedBin;

        return (uint)offset < (uint)coveredBins ? offset / binsPerColumn : -1;
    }

    private void RebuildMask()
    {
        _maskRevision = _config.MaskRevision;

        var rate = SampleRate;
        for (var bin = 0; bin < _size; bin++)
        {
            var offset = (bin < _size / 2 ? bin : bin - _size) * rate / _size;
            var level = _config.MaskDbAt(_config.CenterFrequency + offset);
            _maskDb[bin] = double.IsNaN(level) ? float.PositiveInfinity : (float)level;
        }
    }

    /// <summary>Deinterleaves one window out of the ring and applies the window function.</summary>
    private void LoadWindow(long readSample)
    {
        var start = (int)(readSample & RingMask);
        var ring = _ring;
        var real = _transform.InputReal;
        var imaginary = _transform.InputImaginary;

        for (var j = 0; j < _size; j++)
        {
            var sample = (start + j) & RingMask;
            real[j] = ring[sample * 2];
            imaginary[j] = ring[sample * 2 + 1];
        }

        SimdOps.Multiply(real, _window);
        SimdOps.Multiply(imaginary, _window);
    }

    /// <summary>
    ///     Stamps the frame into the persistence grid and folds it into the pending waterfall row. One
    ///     counter bump per bin: this is what makes rare events visible next to a constant carrier.
    /// </summary>
    private void Accumulate()
    {
        var cells = Grid.Cells;
        var top = (float)_config.DisplayTopDb;
        var bottom = (float)_config.DisplayBottomDb;
        var range = top - bottom;
        if (range <= 0.0f)
            return;

        var rowScale = DensityRows / range;

        for (var bin = 0; bin < _size; bin++)
        {
            var column = _binToColumn[bin];
            if (column < 0)
                continue;

            var db = _db[bin];

            if (db > _waterfallPending[column])
                _waterfallPending[column] = db;

            var row = (int)((top - db) * rowScale);
            if ((uint)row >= (uint)DensityRows)
                continue;

            cells[row * Columns + column] += 1.0f;
        }
    }

    /// <summary>
    ///     Runs the armed trigger slots over the frame. Nothing armed means free run; otherwise any slot
    ///     accepting is enough, so the three can be used to catch three different things at once.
    /// </summary>
    private bool Accepted(long timestamp)
    {
        var triggers = _config.Triggers;
        var armed = false;

        for (var i = 0; i < triggers.Length; i++)
        {
            var trigger = triggers[i];
            if (!trigger.Enabled)
                continue;

            armed = true;

            var passed = trigger.Mode switch
            {
                TriggerMode.Magnitude => CrossesLevel((float)trigger.MagnitudeDb),
                TriggerMode.Mask => CrossesMask(),
                _ => InTimeWindow(trigger, timestamp)
            };

            if (!passed)
                continue;

            trigger.Hits++;
            return true;
        }

        return !armed;
    }

    private bool CrossesLevel(float level)
    {
        for (var bin = 0; bin < _size; bin++)
            if (_binToColumn[bin] >= 0 && _db[bin] > level)
                return true;

        return false;
    }

    private bool CrossesMask()
    {
        for (var bin = 0; bin < _size; bin++)
            if (_binToColumn[bin] >= 0 && _db[bin] > _maskDb[bin])
                return true;

        return false;
    }

    private static bool InTimeWindow(RtsaTrigger trigger, long timestamp) =>
        InTimeWindow(timestamp * MicrosecondsPerTick, trigger.PeriodUs, trigger.OffsetPercent, trigger.DurationUs);

    /// <summary>
    ///     Time qualified gate: the frame is shown only while the clock sits inside a window that repeats
    ///     every period, opening at a percentage of the way through it.
    /// </summary>
    public static bool InTimeWindow(double microseconds, double period, double offsetPercent, double durationUs)
    {
        if (period <= 0.0)
            return true;

        var phase = microseconds % period;
        var opens = period * offsetPercent / 100.0;
        var closes = opens + durationUs;

        //a window that runs off the end of the period wraps into the start of the next one
        return closes <= period
            ? phase >= opens && phase < closes
            : phase >= opens || phase < closes - period;
    }

    #endregion transform

    /// <summary>Wipes the persistence and history, which is what an operator expects from a restart.</summary>
    public void ClearDisplay()
    {
        Grid.Clear();
        Waterfall.Clear();
        Displayed = 0;
        Dropped = 0;

        foreach (var trigger in _config.Triggers)
            trigger.Hits = 0;
    }
}
