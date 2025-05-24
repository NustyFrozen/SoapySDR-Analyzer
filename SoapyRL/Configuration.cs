using ImGuiNET;
using Newtonsoft.Json;
using NLog;
using SoapyRL.Extentions;
using SoapyRL.View;
using SoapyVNACommon.Extentions;
using System.Numerics;

namespace SoapyRL;

public class Configuration(string widgetName, MainWindow initiator, Vector2 windowSize, Vector2 Pos)
{
    private Logger _logger = LogManager.GetCurrentClassLogger();
    private MainWindow parent = initiator;
#if DEBUG
    public ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar;

    private Vector2 screenSize =
new Vector2(Convert.ToInt16(Screen.PrimaryScreen.Bounds.Width / 1.5), Convert.ToInt16(Screen.PrimaryScreen.Bounds.Height / 1.5));

    public Vector2 mainWindowPos = new Vector2(600, 0);
#else

    public ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar |
                                                     ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;

    public Vector2 getScreenSize() => new(Screen.PrimaryScreen.Bounds.Width,
        Screen.PrimaryScreen.Bounds.Height);

    public Vector2 getDefaultScaleSize() => getScreenSize() / new Vector2(1920.0f, 1080.0f);

    public readonly Vector2 s_widgetSize = windowSize;

    public Vector2 mainWindowPos = Pos;
#endif

    public Vector2
        scaleSize = new(windowSize.X / 1920.0f, windowSize.Y / 1080.0f),
        positionOffset = new(50 * windowSize.X / 1920.0f, 10 * windowSize.Y / 1080.0f),
        graphSize = new(Convert.ToInt16(windowSize.X * .8), Convert.ToInt16(windowSize.Y * .9)),
        optionSize = new(Convert.ToInt16(windowSize.X * .2), Convert.ToInt16(windowSize.Y));

    //Path.GetDirectoryName(Application.ExecutablePath)
    public string presetPath = Path.Combine(Global.configPath, widgetName, "Preset.json");

    public string tracesPath = Path.Combine(Global.configPath, widgetName, "traces.json");
    public string markersPath = Path.Combine(Global.configPath, widgetName, "markers.json");

    public string calibrationPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "config", widgetName, "Cal.json");

    public enum saVar
    {
        leakageSleep,
        deviecOptions,
        iqCorrection,
        freqStart,
        freqStop,
        fftSegment,
        fftOverlap,
        scalePerDivision,
        validImpedanceTol
    }

    public ObservableDictionary<saVar, object> config = new();

    public void initDefaultConfig()
    {
        config.CollectionChanged += updateUIElementsOnConfigChanged;
        config[saVar.leakageSleep] = 5;
        config.Add(saVar.deviecOptions, new string[] { });
        config[saVar.iqCorrection] = true;
        config[saVar.freqStart] = 800e6;
        config[saVar.freqStop] = 3000e6;
        config[saVar.fftSegment] = 400;
        config[saVar.fftOverlap] = 0.95;
        config[saVar.scalePerDivision] = 20;
        config[saVar.validImpedanceTol] = 0.9f;
        config.CollectionChanged += updateUIElementsOnConfigChanged;
        updateALLConfigElements();
        if (File.Exists(presetPath))
            loadConfig();
    }

    public void loadConfig()
    {
        try
        {
            //fftmanager constantly uses config so we gotta stop it
            bool resume = false;
            if (parent.rlManager.isRunning)
            {
                resume = true;
                parent.rlManager.stopRL();
            }
            var cfg = JsonConvert.DeserializeObject<ObservableDictionary<saVar, object>>(File.ReadAllText(presetPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() }
                });
            parent.tab_Marker.s_Marker = JsonConvert.DeserializeObject<View.tabs.tab_Marker.marker>(File.ReadAllText(markersPath),
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Converters = new List<JsonConverter> { new ForceIntConverter() }
                });
            parent.tab_Trace.s_traces = JsonConvert.DeserializeObject<View.tabs.tab_Trace.Trace[]>(File.ReadAllText(tracesPath),
                                new JsonSerializerSettings
                                {
                                    TypeNameHandling = TypeNameHandling.Auto,
                                    Converters = new List<JsonConverter> { new ForceIntConverter() }
                                });

            foreach (var keyvaluepair in cfg)
                config[keyvaluepair.Key] = keyvaluepair.Value;

            updateALLConfigElements();
            //resuming RL
            if (resume)
                parent.rlManager.beginRL();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load preset -> {ex.Message}");
        }
    }

    public void saveConfig()
    {
        try
        {
            bool resume = false;
            if (parent.rlManager.isRunning)
            {
                resume = true;
                parent.rlManager.stopRL();
            }
            File.WriteAllText(presetPath, Newtonsoft.Json.JsonConvert.SerializeObject(config));
            File.WriteAllText(markersPath, Newtonsoft.Json.JsonConvert.SerializeObject(parent.tab_Marker.s_Marker));
            File.WriteAllText(tracesPath, Newtonsoft.Json.JsonConvert.SerializeObject(parent.tab_Trace.s_traces));

            if (resume)
                parent.rlManager.beginRL();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save preset -> {ex.Message}");
        }
    }

    private void updateALLConfigElements()
    {
        List<saVar> savarTypes = Enum.GetValues(typeof(saVar)).Cast<saVar>().ToList();
        foreach (var savar in savarTypes)
            updateUIElementsOnConfigChanged(null, new keyOfChangedValueEventArgs(savar));
    }

    private void updateUIElementsOnConfigChanged(object? sender, keyOfChangedValueEventArgs e)
    {
        switch (e.key)
        {
            case saVar.leakageSleep:
                parent.tab_Device.s_osciliatorLeakageSleep = config[saVar.leakageSleep].ToString();
                break;

            case saVar.iqCorrection:
                parent.tab_Device.isCorrectIQEnabled = (bool)config[saVar.iqCorrection];
                break;
        }
    }
}