using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;
using NLog;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapySA.View;
using SoapySA.View.tabs;
using SoapyVNACommon.Extentions;
using Trace = SoapySA.Model.Trace;

namespace SoapySA;

public class Configuration(string widgetName, MainWindowView initiator, Vector2 windowSize, Vector2 pos)
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly MainWindowView _parent = initiator;
#if DEBUG
    public static ImGuiWindowFlags MainWindowFlags = ImGuiWindowFlags.NoScrollbar;

    private Vector2 screenSize =
new Vector2(Convert.ToInt16(Screen.PrimaryScreen.Bounds.Width / 1.5), Convert.ToInt16(Screen.PrimaryScreen.Bounds.Height / 1.5));

    public Vector2 mainWindowPos = new Vector2(600, 0);

    public readonly Vector2 SWidgetSize = windowSize;

    public Vector2 MainWindowPos = pos;
#else

    public static ImGuiWindowFlags MainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar |
                                                     ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;

    public static Vector2 screenSize;
    public readonly Vector2 SWidgetSize = windowSize;

    public Vector2 MainWindowPos = pos;
#endif
    public static Vector2 GetScreenSize()
    {
        return screenSize;
    }

    public static Vector2 GetDefaultScaleSize()
    {
        return GetScreenSize() / new Vector2(1920.0f, 1080.0f);
    }

    public Vector2
        ScaleSize = new(windowSize.X / 1920.0f, windowSize.Y / 1080.0f),
        PositionOffset = new(50 * windowSize.X / 1920.0f, 10 * windowSize.Y / 1080.0f),
        GraphSize = new(Convert.ToInt16(windowSize.X * .8), Convert.ToInt16(windowSize.Y * .9)),
        OptionSize = new(Convert.ToInt16(windowSize.X * .2), Convert.ToInt16(windowSize.Y));

    //Path.GetDirectoryName(Application.ExecutablePath)
    public string PresetPath = Path.Combine(Global.ConfigPath, widgetName, "Preset.json");

    public string TracesPath = Path.Combine(Global.ConfigPath, widgetName, "traces.json");
    public string MarkersPath = Path.Combine(Global.ConfigPath, widgetName, "markers.json");
    
    public enum SaVar
    {
        //frequency
        FreqStart,

        FreqStop,

        //device
        LeakageSleep,

        DeviecOptions,
        IqCorrection,
        FreqInterleaving, //big credit to hackrf_sweep by the gsg team for this method to remove DC bias and nyquist alaiasing

        //amplitude
        GraphStartDb,

        GraphStopDb,
        GraphOffsetDb,
        GraphRefLevel,

        //calibration
        selectedCalibration,
        ///vbw
        FftWindow,

        FftRbw,
        FftSegment,
        FftOverlap,

        //measurement Channel
        ChannelBw,

        ChannelOcp,

        //measurement source
        SourceMode, // 0 = disabled,1 = Track, 2 = CW
        sourceFreq,

        //others
        RefreshRate,

        AutomaticLevel,
        ScalePerDivision
    }

    public ObservableDictionary<SaVar, object> Config = new();

    public void InitConfiguration()
    {
        if (!Directory.Exists(Global.CalibrationPath))
            Directory.CreateDirectory(Global.CalibrationPath);
        var calibrations = new List<string>();
        foreach (var file in Directory.GetFiles(Global.CalibrationPath))
            if (file.EndsWith(".json"))
                calibrations.Add(file.Replace(Global.CalibrationPath, "").Replace("\\", "").Replace("/", "")
                    .Replace(".json", ""));

       _parent.CalibrationView.availableCalibrations = calibrations.ToArray();
        Config[SaVar.FreqStart] = 933.4e6;
        Config[SaVar.FreqStop] = 943.4e6;
        Config[SaVar.LeakageSleep] = 5;
        Config[SaVar.DeviecOptions] = new string[] { };
        Config[SaVar.IqCorrection] = true;
        Config[SaVar.FreqInterleaving] = false;
        Config[SaVar.GraphStartDb] = (double)-136;
        Config[SaVar.GraphStopDb] = (double)0;
        Config[SaVar.GraphOffsetDb] = (double)0;
        Config[SaVar.GraphRefLevel] = (double)-40;
        Config[SaVar.FftRbw] = 0.01e6;
        Config[SaVar.FftSegment] = 13;
        Config[SaVar.FftOverlap] = 0.5;
        Config[SaVar.RefreshRate] = 0;
        Config[SaVar.AutomaticLevel] = false;
        Config[SaVar.ScalePerDivision] = 20;
        Config[SaVar.ChannelBw] = 5e6;
        Config[SaVar.ChannelOcp] = 0.9;
        Config[SaVar.sourceFreq] = 100e6;
        Config[SaVar.SourceMode] = 0;
        Config.CollectionChanged += updateUIElementsOnConfigChanged;
        UpdateAllConfigElements();
        if (File.Exists(PresetPath))
            LoadConfig();
    }

    public void LoadConfig()
    {
        try
        {
            //fftmanager constantly uses config so we gotta stop it
            var resume = false;
            if (_parent.FftManager.IsRunning)
            {
                resume = true;
                _parent.FftManager.StopFft();
            }

            var cfg = JsonConvert.DeserializeObject<ObservableDictionary<SaVar, object>>(File.ReadAllText(PresetPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() }
                });
            _parent.MarkerView.SMarkers = JsonConvert.DeserializeObject<Marker[]>(File.ReadAllText(MarkersPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() }
                });
            _parent.TraceView.STraces = JsonConvert.DeserializeObject<Trace[]>(File.ReadAllText(TracesPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() }
                });

            foreach (var keyvaluepair in cfg)
                Config[keyvaluepair.Key] = keyvaluepair.Value;
            if (Config.ContainsKey(SaVar.selectedCalibration))
            {
                try
                {
                    _parent.CalibrationView.calibrationData = JsonConvert.DeserializeObject<List<Tuple<float, float>>>(File.ReadAllText((string)Config[SaVar.selectedCalibration]));
                }
                catch (Exception e)
                {
                    _logger.Error($"Failed to load Calibration --> {e.Message}");
                }
                
            }
            UpdateAllConfigElements();
            //resuming fftmanager
            if (resume)
                _parent.FftManager.BeginFft();
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
            var resume = false;
            if (_parent.FftManager.IsRunning)
            {
                resume = true;
                _parent.FftManager.StopFft();
            }

            File.WriteAllText(PresetPath, JsonConvert.SerializeObject(Config));
            File.WriteAllText(MarkersPath, JsonConvert.SerializeObject(_parent.MarkerView.SMarkers));
            File.WriteAllText(TracesPath, JsonConvert.SerializeObject(_parent.TraceView.STraces));

            if (resume)
                _parent.FftManager.BeginFft();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save preset -> {ex.Message}");
        }
    }

    private void UpdateAllConfigElements()
    {
        var savarTypes = Enum.GetValues(typeof(SaVar)).Cast<SaVar>().ToList();
        foreach (var savar in savarTypes)
            updateUIElementsOnConfigChanged(null, new KeyOfChangedValueEventArgs(savar));
    }

    private void updateUIElementsOnConfigChanged(object? sender, KeyOfChangedValueEventArgs e)
    {
        switch (e.Key)
        {
            case SaVar.FreqStart:
                var start = (double)_parent.Configuration.Config[SaVar.FreqStart];
                var stop = (double)_parent.Configuration.Config[SaVar.FreqStop];
                _parent.FrequencyView.SDisplaySpan = (stop - start).ToString();
                _parent.FrequencyView.SDisplayFreqCenter = ((stop - start) / 2.0 + start).ToString();
                _parent.FrequencyView.SDisplayFreqStart = Config[SaVar.FreqStart].ToString();
                break;

            case SaVar.FreqStop:
                var start2 = (double)_parent.Configuration.Config[SaVar.FreqStart];
                var stop2 = (double)_parent.Configuration.Config[SaVar.FreqStop];
                _parent.FrequencyView.SDisplaySpan = (stop2 - start2).ToString();
                _parent.FrequencyView.SDisplayFreqCenter = ((stop2 - start2) / 2.0 + start2).ToString();
                _parent.FrequencyView.SDisplayFreqStop = Config[SaVar.FreqStop].ToString();
                break;

            case SaVar.LeakageSleep:
                _parent.DeviceView.SOsciliatorLeakageSleep = Config[SaVar.LeakageSleep].ToString();
                break;

            case SaVar.IqCorrection:
                _parent.DeviceView.SIsCorrectIqEnabled = (bool)Config[SaVar.IqCorrection];
                break;

            case SaVar.FreqInterleaving:
                _parent.DeviceView.SIsinterleavingEnabled = (bool)Config[SaVar.FreqInterleaving];
                break;

            case SaVar.GraphStartDb:
                _parent.AmplitudeView.SDisplayStartDb = Config[SaVar.GraphStartDb].ToString();
                break;

            case SaVar.GraphStopDb:
                _parent.AmplitudeView.SDisplayStopDb = Config[SaVar.GraphStopDb].ToString();
                break;

            case SaVar.GraphOffsetDb:
                _parent.AmplitudeView.SDisplayOffset = Config[SaVar.GraphOffsetDb].ToString();
                break;

            case SaVar.GraphRefLevel:
                _parent.AmplitudeView.SDisplayRefLevel = Config[SaVar.GraphRefLevel].ToString();
                break;

            case SaVar.FftSegment:
                _parent.VideoView.SFftSegments = Config[SaVar.FftSegment].ToString();
                break;

            case SaVar.AutomaticLevel:
                _parent.AmplitudeView.SAutomaticLevelingEnabled = (bool)Config[SaVar.AutomaticLevel];
                break;

            case SaVar.ScalePerDivision:
                _parent.AmplitudeView.SScalePerDivision = (int)Config[SaVar.ScalePerDivision];
                break;

            case SaVar.FftRbw:
                _parent.VideoView.FftRbw = Config[SaVar.FftRbw].ToString();
                break;

            case SaVar.FftOverlap:
                _parent.VideoView.SFftOverlap = ((double)Config[SaVar.FftOverlap] * 100.0).ToString();
                break;

            case SaVar.RefreshRate:
                _parent.VideoView.DisplayRefreshRate = ((int)Config[SaVar.RefreshRate] * 1000).ToString();
                break;

            case SaVar.sourceFreq:
                _parent.SourceView.transmissionFreq = ((double)Config[SaVar.sourceFreq]).ToString();
                break;
            case SaVar.SourceMode:
                _parent.SourceView.selectedSourceMode = (int)Config[SaVar.SourceMode];
                break;
        }
    }
}