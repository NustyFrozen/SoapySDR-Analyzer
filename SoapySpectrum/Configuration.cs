using ImGuiNET;
using Newtonsoft.Json;
using NLog;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapySA.View;
using SoapySA.View.tabs;
using SoapyVNACommon.Extentions;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Trace = SoapySA.Model.Trace;

namespace SoapySA;

public class Configuration : INotifyPropertyChanged
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly MainWindowView _parent;
    private readonly string _widgetName;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? OnConfigLoadBegin;
    public event EventHandler? OnConfigLoadEnd;

    public event EventHandler? OnConfigSaveBegin;
    public event EventHandler? OnConfigSaveEnd;

    public readonly string PresetPath;
    public readonly string TracesPath;
    public readonly string MarkersPath;

    public Configuration(string widgetName, MainWindowView initiator)
    {
        _widgetName = widgetName;
        _parent = initiator;

        PresetPath = Path.Combine(Global.ConfigPath, widgetName, "Preset.json");
        TracesPath = Path.Combine(Global.ConfigPath, widgetName, "traces.json");
        MarkersPath = Path.Combine(Global.ConfigPath, widgetName, "markers.json");
    }

    // --------------------------
    // Strongly-typed properties
    // --------------------------

    // frequency
    private double _freqStart;
    public double FreqStart
    {
        get => _freqStart;
        set
        {
            if (SetField(ref _freqStart, value))
            {
                OnPropertyChanged(nameof(Span));
                OnPropertyChanged(nameof(FreqCenter));
            }
        }
    }

    private double _freqStop;
    public double FreqStop
    {
        get => _freqStop;
        set
        {
            if (SetField(ref _freqStop, value))
            {
                OnPropertyChanged(nameof(Span));
                OnPropertyChanged(nameof(FreqCenter));
            }
        }
    }

    [JsonIgnore]
    public double Span => FreqStop - FreqStart;

    [JsonIgnore]
    public double FreqCenter => (FreqStop - FreqStart) / 2.0 + FreqStart;

    // device
    private int _leakageSleep;
    public int LeakageSleep
    {
        get => _leakageSleep;
        set => SetField(ref _leakageSleep, value);
    }

    private string[] _deviceOptions = Array.Empty<string>();
    public string[] DeviceOptions
    {
        get => _deviceOptions;
        set => SetField(ref _deviceOptions, value ?? Array.Empty<string>());
    }

    private bool _iqCorrection;
    public bool IqCorrection
    {
        get => _iqCorrection;
        set => SetField(ref _iqCorrection, value);
    }

    private bool _freqInterleaving;
    public bool FreqInterleaving
    {
        get => _freqInterleaving;
        set => SetField(ref _freqInterleaving, value);
    }

    // amplitude
    private double _graphStartDb;
    public double GraphStartDb
    {
        get => _graphStartDb;
        set => SetField(ref _graphStartDb, value);
    }

    private double _graphStopDb;
    public double GraphStopDb
    {
        get => _graphStopDb;
        set => SetField(ref _graphStopDb, value);
    }

    private double _graphOffsetDb;
    public double GraphOffsetDb
    {
        get => _graphOffsetDb;
        set => SetField(ref _graphOffsetDb, value);
    }

    private double _graphRefLevel;
    public double GraphRefLevel
    {
        get => _graphRefLevel;
        set => SetField(ref _graphRefLevel, value);
    }

    // calibration
    private string? _selectedCalibration;
    public string? SelectedCalibration
    {
        get => _selectedCalibration;
        set => SetField(ref _selectedCalibration, value);
    }

    // vbw / fft
    private string? _fftWindow;
    public string? FftWindow
    {
        get => _fftWindow;
        set => SetField(ref _fftWindow, value);
    }

    private double _fftRbw;
    public double FftRbw
    {
        get => _fftRbw;
        set => SetField(ref _fftRbw, value);
    }

    private int _fftSegment;
    public int FftSegment
    {
        get => _fftSegment;
        set => SetField(ref _fftSegment, value);
    }

    private double _fftOverlap;
    public double FftOverlap
    {
        get => _fftOverlap;
        set => SetField(ref _fftOverlap, value);
    }

    // measurement channel
    private double _channelBw;
    public double ChannelBw
    {
        get => _channelBw;
        set => SetField(ref _channelBw, value);
    }

    private double _channelOcp;
    public double ChannelOcp
    {
        get => _channelOcp;
        set => SetField(ref _channelOcp, value);
    }

    // measurement source
    private int _sourceMode; // 0 = disabled, 1 = Track, 2 = CW
    public int SourceMode
    {
        get => _sourceMode;
        set => SetField(ref _sourceMode, value);
    }

    private double _sourceFreq;
    public double SourceFreq
    {
        get => _sourceFreq;
        set => SetField(ref _sourceFreq, value);
    }

    // others
    private int _refreshRate;
    public int RefreshRate
    {
        get => _refreshRate;
        set => SetField(ref _refreshRate, value);
    }

    private bool _automaticLevel;
    public bool AutomaticLevel
    {
        get => _automaticLevel;
        set => SetField(ref _automaticLevel, value);
    }

    private int _scalePerDivision;
    public int ScalePerDivision
    {
        get => _scalePerDivision;
        set => SetField(ref _scalePerDivision, value);
    }

    // --------------------------
    // Init / Load / Save
    // --------------------------

    public void InitConfiguration()
    {
        if (!Directory.Exists(Global.CalibrationPath))
            Directory.CreateDirectory(Global.CalibrationPath);


        // Defaults (matching your existing ones)
        FreqStart = 933.4e6;
        FreqStop = 943.4e6;

        LeakageSleep = 5;
        DeviceOptions = Array.Empty<string>();
        IqCorrection = true;
        FreqInterleaving = false;

        GraphStartDb = -136;
        GraphStopDb = 0;
        GraphOffsetDb = 0;
        GraphRefLevel = -40;

        FftRbw = 0.01e6;
        FftSegment = 13;
        FftOverlap = 0.5;

        RefreshRate = 0;
        AutomaticLevel = false;
        ScalePerDivision = 20;

        ChannelBw = 5e6;
        ChannelOcp = 0.9;

        SourceFreq = 100e6;
        SourceMode = 0;

        if (File.Exists(PresetPath))
            LoadConfig();
    }

    public void LoadConfig()
    {
        try
        {
            onConfigLoadBegin();

            if (JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(PresetPath)) is { } LoadedConfig)
                CopyFrom(LoadedConfig);



            onConfigLoadEnd();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load preset -> {ex.Message}");
        }
    }

    public void SaveConfig()
    {
        try
        {
            onConfigSaveBegin();
            // Save ONLY the config (this) to preset
            File.WriteAllText(PresetPath, JsonConvert.SerializeObject(this, Formatting.Indented));      
            onConfigSaveEnd();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save preset -> {ex.Message}");
        }
    }

    // --------------------------
    // Helpers
    // --------------------------

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }
    private void onConfigLoadBegin()
        => OnConfigLoadBegin?.Invoke(this, EventArgs.Empty);

    private void onConfigLoadEnd()
        => OnConfigLoadEnd?.Invoke(this, EventArgs.Empty);
    private void onConfigSaveBegin()
            => OnConfigSaveBegin?.Invoke(this, EventArgs.Empty);

    private void onConfigSaveEnd()
        => OnConfigSaveEnd?.Invoke(this, EventArgs.Empty);

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Copy loaded config into the existing instance so all components keep referencing the same object.
    /// This is the key part that preserves "mutual reference" across the app.
    /// </summary>
    private void CopyFrom(Configuration other)
    {
        // frequency
        FreqStart = other.FreqStart;
        FreqStop = other.FreqStop;

        // device
        LeakageSleep = other.LeakageSleep;
        DeviceOptions = other.DeviceOptions ?? Array.Empty<string>();
        IqCorrection = other.IqCorrection;
        FreqInterleaving = other.FreqInterleaving;

        // amplitude
        GraphStartDb = other.GraphStartDb;
        GraphStopDb = other.GraphStopDb;
        GraphOffsetDb = other.GraphOffsetDb;
        GraphRefLevel = other.GraphRefLevel;

        // calibration
        SelectedCalibration = other.SelectedCalibration;

        // fft
        FftWindow = other.FftWindow;
        FftRbw = other.FftRbw;
        FftSegment = other.FftSegment;
        FftOverlap = other.FftOverlap;

        // measurement channel
        ChannelBw = other.ChannelBw;
        ChannelOcp = other.ChannelOcp;

        // source
        SourceMode = other.SourceMode;
        SourceFreq = other.SourceFreq;

        // others
        RefreshRate = other.RefreshRate;
        AutomaticLevel = other.AutomaticLevel;
        ScalePerDivision = other.ScalePerDivision;
    }
}