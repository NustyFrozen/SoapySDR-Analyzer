using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;
using NLog;
using SoapyRL.Extentions;
using SoapyRL.View;
using SoapyRL.View.tabs;
using SoapySA.Extentions;
using SoapyVNACommon.Extentions;

namespace SoapyRL;

public class Configuration(string widgetName, MainWindow initiator, Vector2 windowSize, Vector2 pos)
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly MainWindow _parent = initiator;

#if DEBUG
    public ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar;
#endif

    // Every GUI dimension below is a percentage of the screen resolved by UserScreenConfiguration,
    // so this widget scales exactly like the spectrum widget instead of keeping its own copy of the
    // 1920x1080 pixel maths.
    public ImGuiWindowFlags MainWindowFlags => UserScreenConfiguration.MainWindowFlags;

    public static Vector2 ScreenSize => UserScreenConfiguration.ScreenSize;

    public Vector2 GetScreenSize()
    {
        return UserScreenConfiguration.ScreenSize;
    }

    public Vector2 GetDefaultScaleSize()
    {
        return UserScreenConfiguration.GetDefaultScaleSize();
    }

    /// <summary>Size this widget was created with. The layout itself follows the live screen size.</summary>
    public readonly Vector2 SWidgetSize = windowSize;

    public Vector2 MainWindowPos = pos;

    public Vector2 ScaleSize => UserScreenConfiguration.ScaleSize;
    public Vector2 PositionOffset => UserScreenConfiguration.PositionOffset;
    public Vector2 GraphSize => UserScreenConfiguration.GraphSize;
    public Vector2 OptionSize => UserScreenConfiguration.OptionSize;

    //Path.GetDirectoryName(Application.ExecutablePath)
    public string PresetPath = Path.Combine(Global.ConfigPath, widgetName, "Preset.json");

    public string TracesPath = Path.Combine(Global.ConfigPath, widgetName, "traces.json");
    public string MarkersPath = Path.Combine(Global.ConfigPath, widgetName, "markers.json");

    public string CalibrationPath = Path.Combine(
        System.AppDomain.CurrentDomain.BaseDirectory,
        "config",
        widgetName,
        "Cal.json"
    );

    public enum SaVar
    {
        LeakageSleep,
        DeviecOptions,
        IqCorrection,
        FreqStart,
        FreqStop,
        FftSegment,
        FftOverlap,
        ScalePerDivision,
        ValidImpedanceTol,
    }

    public ObservableDictionary<SaVar, object> Config = new();

    public void InitDefaultConfig()
    {
        Config.CollectionChanged += updateUIElementsOnConfigChanged;
        Config[SaVar.LeakageSleep] = 5;
        Config.Add(SaVar.DeviecOptions, new string[] { });
        Config[SaVar.IqCorrection] = true;
        Config[SaVar.FreqStart] = 800e6;
        Config[SaVar.FreqStop] = 3000e6;
        Config[SaVar.FftSegment] = 400;
        Config[SaVar.FftOverlap] = 0.95;
        Config[SaVar.ScalePerDivision] = 20;
        Config[SaVar.ValidImpedanceTol] = 0.9f;
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
            if (_parent.RlManager.IsRunning)
            {
                resume = true;
                _parent.RlManager.StopRl();
            }

            var cfg = JsonConvert.DeserializeObject<ObservableDictionary<SaVar, object>>(
                File.ReadAllText(PresetPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() },
                }
            );
            _parent.TabMarker.SMarker = JsonConvert.DeserializeObject<TabMarker.Marker>(
                File.ReadAllText(MarkersPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() },
                }
            );
            _parent.TabTrace.STraces = JsonConvert.DeserializeObject<TabTrace.Trace[]>(
                File.ReadAllText(TracesPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() },
                }
            );

            foreach (var keyvaluepair in cfg)
                Config[keyvaluepair.Key] = keyvaluepair.Value;

            UpdateAllConfigElements();
            //resuming RL
            if (resume)
                _parent.RlManager.BeginRl();
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
            if (_parent.RlManager.IsRunning)
            {
                resume = true;
                _parent.RlManager.StopRl();
            }

            File.WriteAllText(PresetPath, JsonConvert.SerializeObject(Config));
            File.WriteAllText(MarkersPath, JsonConvert.SerializeObject(_parent.TabMarker.SMarker));
            File.WriteAllText(TracesPath, JsonConvert.SerializeObject(_parent.TabTrace.STraces));

            if (resume)
                _parent.RlManager.BeginRl();
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
            case SaVar.LeakageSleep:
                _parent.TabDevice.SOsciliatorLeakageSleep = Config[SaVar.LeakageSleep].ToString();
                break;

            case SaVar.IqCorrection:
                _parent.TabDevice.IsCorrectIqEnabled = (bool)Config[SaVar.IqCorrection];
                break;
        }
    }
}
