using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using NLog;
using SoapyRTSA.Model;
using SoapyVNACommon.Extentions;
using Logger = NLog.Logger;

namespace SoapyRTSA;

/// <summary>
///     Everything the analyser is told to do, in one observable object. The sweep threads read it live,
///     the tabs write it, and it is what a preset is made of.
/// </summary>
public class Configuration : INotifyPropertyChanged
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? OnConfigLoadBegin;
    public event EventHandler? OnConfigLoadEnd;
    public event EventHandler? OnConfigSaveBegin;
    public event EventHandler? OnConfigSaveEnd;

    //derived from the widget name and specific to this machine, so never part of a preset
    [JsonIgnore]
    public readonly string PresetPath;

    public Configuration(string widgetName)
    {
        PresetPath = Path.Combine(Global.ConfigPath, widgetName, "rtsa.json");
    }

    /// <summary>
    ///     For the deserializer only: what comes back is a carrier that <see cref="CopyFrom" /> drains into
    ///     the live instance, so every component keeps referencing the same object.
    /// </summary>
    [JsonConstructor]
    private Configuration()
    {
        PresetPath = string.Empty;
    }

    #region device

    private double _sampleRate;
    public double SampleRate
    {
        get => _sampleRate;
        set => SetField(ref _sampleRate, value);
    }

    private string _clockSource = string.Empty;
    public string ClockSource
    {
        get => _clockSource;
        set => SetField(ref _clockSource, value ?? string.Empty);
    }

    private double _masterClockRate;
    public double MasterClockRate
    {
        get => _masterClockRate;
        set => SetField(ref _masterClockRate, value);
    }

    private string _streamArgs = string.Empty;
    public string StreamArgs
    {
        get => _streamArgs;
        set => SetField(ref _streamArgs, value ?? string.Empty);
    }

    /// <summary>Gain per element keyed "channel:element", the same shape the spectrum widget stores.</summary>
    private Dictionary<string, double> _rxGains = new();
    public Dictionary<string, double> RxGains
    {
        get => _rxGains;
        set => SetField(ref _rxGains, value ?? new Dictionary<string, double>());
    }

    public static string GainKey(uint channel, string element) => $"{channel}:{element}";

    public void SetRxGain(uint channel, string element, double value)
    {
        var key = GainKey(channel, element);
        if (RxGains.TryGetValue(key, out var current) && current.Equals(value))
            return;

        RxGains[key] = value;
        OnPropertyChanged(nameof(RxGains));
    }

    #endregion device

    #region frequency

    private double _centerFrequency;
    public double CenterFrequency
    {
        get => _centerFrequency;
        set
        {
            if (SetField(ref _centerFrequency, value))
                OnPropertyChanged(nameof(SpanStart));
        }
    }

    /// <summary>
    ///     Displayed span. There is no hopping in a real time analyser, so this only ever zooms into the
    ///     instantaneous bandwidth the sample rate already gives us.
    /// </summary>
    private double _span;
    public double Span
    {
        get => _span;
        set
        {
            if (SetField(ref _span, value))
                OnPropertyChanged(nameof(SpanStart));
        }
    }

    [JsonIgnore]
    public double SpanStart => CenterFrequency - Span / 2.0;

    [JsonIgnore]
    public double SpanStop => CenterFrequency + Span / 2.0;

    #endregion frequency

    #region fft

    private int _fftSize = 1024;
    public int FftSize
    {
        get => _fftSize;
        set => SetField(ref _fftSize, value);
    }

    /// <summary>
    ///     How much each transform reuses the previous one. This is the knob that buys probability of
    ///     intercept: at 75% a burst a quarter of a window long still lands whole inside some transform.
    /// </summary>
    private int _overlapPercent = 75;
    public int OverlapPercent
    {
        get => _overlapPercent;
        set => SetField(ref _overlapPercent, value);
    }

    private FftWindowType _window = FftWindowType.BlackmanHarris;
    public FftWindowType Window
    {
        get => _window;
        set => SetField(ref _window, value);
    }

    #endregion fft

    #region amplitude

    /// <summary>Top of the display, dB.</summary>
    private double _refLevelDb = -20.0;
    public double RefLevelDb
    {
        get => _refLevelDb;
        set => SetField(ref _refLevelDb, value);
    }

    /// <summary>How far down the display reaches from the reference level, dB.</summary>
    private double _rangeDb = 100.0;
    public double RangeDb
    {
        get => _rangeDb;
        set => SetField(ref _rangeDb, value);
    }

    private double _offsetDb;
    public double OffsetDb
    {
        get => _offsetDb;
        set => SetField(ref _offsetDb, value);
    }

    private int _scalePerDivision = 10;
    public int ScalePerDivision
    {
        get => _scalePerDivision;
        set => SetField(ref _scalePerDivision, value);
    }

    [JsonIgnore]
    public double DisplayTopDb => RefLevelDb;

    [JsonIgnore]
    public double DisplayBottomDb => RefLevelDb - RangeDb;

    #endregion amplitude

    #region display

    /// <summary>How long a hit takes to fade out of the grid.</summary>
    private double _fadeSeconds = 1.0;
    public double FadeSeconds
    {
        get => _fadeSeconds;
        set => SetField(ref _fadeSeconds, value);
    }

    /// <summary>
    ///     Curve applied to the occupancy before it is coloured. Occupancy covers five decades - a carrier
    ///     is in every frame, a burst might be in one frame of a hundred thousand - and a straight linear
    ///     ramp would show the top decade and nothing else. 3 puts a 1-in-1000 burst in the blues while an
    ///     always present signal still reaches red.
    /// </summary>
    private double _densityGamma = 3.0;
    public double DensityGamma
    {
        get => _densityGamma;
        set => SetField(ref _densityGamma, value);
    }

    /// <summary>Clamped for use: a preset written before this existed would otherwise divide by zero.</summary>
    [JsonIgnore]
    public float DensityExponent => 1.0f / (float)Math.Clamp(DensityGamma <= 0.0 ? 3.0 : DensityGamma, 1.0, 12.0);

    private bool _showWaterfall = true;
    public bool ShowWaterfall
    {
        get => _showWaterfall;
        set => SetField(ref _showWaterfall, value);
    }

    private int _waterfallIntervalMs = 30;
    public int WaterfallIntervalMs
    {
        get => _waterfallIntervalMs;
        set => SetField(ref _waterfallIntervalMs, value);
    }

    private bool _pinCores = true;
    public bool PinCores
    {
        get => _pinCores;
        set => SetField(ref _pinCores, value);
    }

    /// <summary>Hold freezes the display so markers can be read. Never persisted.</summary>
    [JsonIgnore]
    public bool Paused { get; set; }

    #endregion display

    #region triggers

    public RtsaTrigger[] Triggers { get; set; } =
    {
        new(), new(), new()
    };

    /// <summary>Mask drawn on the graph, ordered by frequency.</summary>
    public List<MaskPoint> Mask { get; set; } = new();

    [JsonIgnore]
    public bool MaskEditing { get; set; }

    /// <summary>Bumped whenever the mask changes so the sweep knows to rebuild its per bin thresholds.</summary>
    [JsonIgnore]
    public int MaskRevision { get; private set; }

    public void AddMaskPoint(double frequency, double db)
    {
        Mask.Add(new MaskPoint { Frequency = frequency, Db = db });
        Mask.Sort(static (left, right) => left.Frequency.CompareTo(right.Frequency));
        MaskRevision++;
    }

    public void ClearMask()
    {
        Mask.Clear();
        MaskRevision++;
    }

    /// <summary>Mask level at a frequency, or NaN where the mask does not reach.</summary>
    public double MaskDbAt(double frequency)
    {
        if (Mask.Count == 0)
            return double.NaN;

        if (Mask.Count == 1)
            return Mask[0].Db;

        if (frequency <= Mask[0].Frequency || frequency >= Mask[^1].Frequency)
            return double.NaN;

        for (var i = 1; i < Mask.Count; i++)
        {
            var right = Mask[i];
            if (right.Frequency < frequency)
                continue;

            var left = Mask[i - 1];
            var width = right.Frequency - left.Frequency;
            if (width <= 0.0)
                return right.Db;

            var t = (frequency - left.Frequency) / width;
            return left.Db + (right.Db - left.Db) * t;
        }

        return double.NaN;
    }

    #endregion triggers

    #region markers

    public RtsaMarker[] Markers { get; set; } = BuildMarkers();

    private static RtsaMarker[] BuildMarkers()
    {
        var markers = new RtsaMarker[4];
        for (var i = 0; i < markers.Length; i++)
            markers[i] = new RtsaMarker { Id = i };

        return markers;
    }

    [JsonIgnore]
    public int SelectedMarker { get; set; }

    #endregion markers

    public void InitConfiguration()
    {
        CenterFrequency = 100e6;
        Span = 0; //resolved from the sample rate once the device is known

        FftSize = 1024;
        OverlapPercent = 75;
        Window = FftWindowType.BlackmanHarris;

        RefLevelDb = -20;
        RangeDb = 100;
        OffsetDb = 0;
        ScalePerDivision = 10;

        FadeSeconds = 1.0;
        DensityGamma = 3.0;
        ShowWaterfall = true;
        WaterfallIntervalMs = 30;
        PinCores = true;

        SampleRate = 0;
        ClockSource = string.Empty;
        MasterClockRate = 0;
        StreamArgs = string.Empty;
        RxGains = new Dictionary<string, double>();

        Triggers = new[] { new RtsaTrigger(), new RtsaTrigger(), new RtsaTrigger() };
        Mask = new List<MaskPoint>();
        Markers = BuildMarkers();

        if (File.Exists(PresetPath))
            LoadConfig();

        foreach (var trigger in Triggers)
            trigger.SyncEditors();
    }

    public void LoadConfig()
    {
        try
        {
            OnConfigLoadBegin?.Invoke(this, EventArgs.Empty);

            if (JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(PresetPath)) is { } loaded)
                CopyFrom(loaded);

            OnConfigLoadEnd?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            _logger.Error($"Failed to load preset -> {exception.Message}");
        }
    }

    public void SaveConfig()
    {
        try
        {
            OnConfigSaveBegin?.Invoke(this, EventArgs.Empty);

            var directory = Path.GetDirectoryName(PresetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(PresetPath, JsonConvert.SerializeObject(this, Formatting.Indented));

            OnConfigSaveEnd?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            _logger.Error($"Failed to save preset -> {exception.Message}");
        }
    }

    /// <summary>
    ///     Drains a loaded preset into this instance. Reference members are copied rather than aliased: the
    ///     loaded carrier is dropped as soon as this returns.
    /// </summary>
    private void CopyFrom(Configuration other)
    {
        SampleRate = other.SampleRate;
        ClockSource = other.ClockSource;
        MasterClockRate = other.MasterClockRate;
        StreamArgs = other.StreamArgs;
        RxGains = other.RxGains is null
            ? new Dictionary<string, double>()
            : new Dictionary<string, double>(other.RxGains);

        CenterFrequency = other.CenterFrequency;
        Span = other.Span;

        FftSize = other.FftSize;
        OverlapPercent = other.OverlapPercent;
        Window = other.Window;

        RefLevelDb = other.RefLevelDb;
        RangeDb = other.RangeDb;
        OffsetDb = other.OffsetDb;
        ScalePerDivision = other.ScalePerDivision;

        FadeSeconds = other.FadeSeconds;
        DensityGamma = other.DensityGamma;
        ShowWaterfall = other.ShowWaterfall;
        WaterfallIntervalMs = other.WaterfallIntervalMs;
        PinCores = other.PinCores;

        if (other.Triggers is { Length: > 0 })
            for (var i = 0; i < Triggers.Length && i < other.Triggers.Length; i++)
            {
                var source = other.Triggers[i];
                var target = Triggers[i];
                target.Enabled = source.Enabled;
                target.Mode = source.Mode;
                target.MagnitudeDb = source.MagnitudeDb;
                target.PeriodUs = source.PeriodUs;
                target.OffsetPercent = source.OffsetPercent;
                target.DurationUs = source.DurationUs;
                target.SyncEditors();
            }

        Mask = other.Mask is null ? new List<MaskPoint>() : new List<MaskPoint>(other.Mask);
        MaskRevision++;

        if (other.Markers is { Length: > 0 })
            for (var i = 0; i < Markers.Length && i < other.Markers.Length; i++)
            {
                var source = other.Markers[i];
                var target = Markers[i];
                target.IsActive = source.IsActive;
                target.Position = source.Position;
                target.DeltaReference = source.DeltaReference;
                target.BandPower = source.BandPower;
                target.BandPowerSpan = source.BandPowerSpan;
                target.BandPowerSpanStr = source.BandPowerSpanStr ?? "1M";
            }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
