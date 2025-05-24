using ImGuiNET;
using Newtonsoft.Json;
using NLog;
using SoapySA.Extentions;
using SoapySA.View;
using SoapySA.View.tabs;
using SoapyVNACommon.Extentions;
using System.Numerics;

namespace SoapySA
{
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

        public static ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar |
                                                         ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;

        public static Vector2 getScreenSize() => new(Screen.PrimaryScreen.Bounds.Width,
            Screen.PrimaryScreen.Bounds.Height);

        public static Vector2 getDefaultScaleSize() => getScreenSize() / new Vector2(1920.0f, 1080.0f);

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
            //frequency
            freqStart,

            freqStop,

            //device
            leakageSleep,

            deviecOptions,
            iqCorrection,
            freqInterleaving, //big credit to hackrf_sweep by the gsg team for this method to remove DC bias and nyquist alaiasing

            //amplitude
            graphStartDB,

            graphStopDB,
            graphOffsetDB,
            graphRefLevel,

            ///vbw
            fftWindow,

            fftRBW,
            fftSegment,
            fftOverlap,

            //measurement Channel
            channelBW,

            channelOCP,

            //others
            refreshRate,

            automaticLevel,
            scalePerDivision
        }

        public ObservableDictionary<saVar, object> config = new();

        public void initConfiguration()
        {
            if (!Directory.Exists(calibrationPath))
                Directory.CreateDirectory(calibrationPath);
            var calibrations = new List<string>();
            foreach (var file in Directory.GetFiles(calibrationPath))
                if (file.EndsWith(".cal"))
                    calibrations.Add(file.Replace(calibrationPath, "").Replace("\\", "").Replace("/", "")
                        .Replace(".cal", ""));

            //tab_Cal.s_AvailableCal = calibrations.ToArray();

            config[saVar.freqStart] = 933.4e6;
            config[saVar.freqStop] = 943.4e6;
            config[saVar.leakageSleep] = 5;
            config[saVar.deviecOptions] = new string[] { };
            config[saVar.iqCorrection] = true;
            config[saVar.freqInterleaving] = false;
            config[saVar.graphStartDB] = (double)-136;
            config[saVar.graphStopDB] = (double)0;
            config[saVar.graphOffsetDB] = (double)0;
            config[saVar.graphRefLevel] = (double)-40;
            config[saVar.fftRBW] = 0.01e6;
            config[saVar.fftSegment] = 13;
            config[saVar.fftOverlap] = 0.5;
            config[saVar.refreshRate] = 0;
            config[saVar.automaticLevel] = false;
            config[saVar.scalePerDivision] = 20;
            config[saVar.channelBW] = 5e6;
            config[saVar.channelOCP] = 0.9;
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
                if (parent.fftManager.isRunning)
                {
                    resume = true;
                    parent.fftManager.stopFFT();
                }
                var cfg = JsonConvert.DeserializeObject<ObservableDictionary<saVar, object>>(File.ReadAllText(presetPath),
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        Converters = new List<JsonConverter> { new ForceIntConverter() }
                    });
                parent.tab_Marker.s_markers = JsonConvert.DeserializeObject<tab_Marker.marker[]>(File.ReadAllText(markersPath),
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        Converters = new List<JsonConverter> { new ForceIntConverter() }
                    });
                parent.tab_Trace.s_traces = JsonConvert.DeserializeObject<tab_Trace.Trace[]>(File.ReadAllText(tracesPath),
                                    new JsonSerializerSettings
                                    {
                                        TypeNameHandling = TypeNameHandling.Auto,
                                        Converters = new List<JsonConverter> { new ForceIntConverter() }
                                    });

                foreach (var keyvaluepair in cfg)
                    config[keyvaluepair.Key] = keyvaluepair.Value;

                updateALLConfigElements();
                //resuming fftmanager
                if (resume)
                    parent.fftManager.beginFFT();
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
                if (parent.fftManager.isRunning)
                {
                    resume = true;
                    parent.fftManager.stopFFT();
                }
                File.WriteAllText(presetPath, Newtonsoft.Json.JsonConvert.SerializeObject(config));
                File.WriteAllText(markersPath, Newtonsoft.Json.JsonConvert.SerializeObject(parent.tab_Marker.s_markers));
                File.WriteAllText(tracesPath, Newtonsoft.Json.JsonConvert.SerializeObject(parent.tab_Trace.s_traces));

                if (resume)
                    parent.fftManager.beginFFT();
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
                case saVar.freqStart:
                    var start = (double)parent.Configuration.config[Configuration.saVar.freqStart];
                    var stop = (double)parent.Configuration.config[Configuration.saVar.freqStop];
                    parent.tab_Frequency.s_displaySpan = (stop - start).ToString();
                    parent.tab_Frequency.s_displayFreqCenter = ((stop - start) / 2.0 + start).ToString();
                    parent.tab_Frequency.s_displayFreqStart = config[saVar.freqStart].ToString();
                    break;

                case saVar.freqStop:
                    var start2 = (double)parent.Configuration.config[Configuration.saVar.freqStart];
                    var stop2 = (double)parent.Configuration.config[Configuration.saVar.freqStop];
                    parent.tab_Frequency.s_displaySpan = (stop2 - start2).ToString();
                    parent.tab_Frequency.s_displayFreqCenter = ((stop2 - start2) / 2.0 + start2).ToString();
                    parent.tab_Frequency.s_displayFreqStop = config[saVar.freqStop].ToString();
                    break;

                case saVar.leakageSleep:
                    parent.tab_Device.s_osciliatorLeakageSleep = config[saVar.leakageSleep].ToString();
                    break;

                case saVar.iqCorrection:
                    parent.tab_Device.s_isCorrectIQEnabled = (bool)config[saVar.iqCorrection];
                    break;

                case saVar.freqInterleaving:
                    parent.tab_Device.s_isinterleavingEnabled = (bool)config[saVar.freqInterleaving];
                    break;

                case saVar.graphStartDB:
                    parent.tab_Amplitude.s_displayStartDB = config[saVar.graphStartDB].ToString();
                    break;

                case saVar.graphStopDB:
                    parent.tab_Amplitude.s_displayStopDB = config[saVar.graphStopDB].ToString();
                    break;

                case saVar.graphOffsetDB:
                    parent.tab_Amplitude.s_displayOffset = config[saVar.graphOffsetDB].ToString();
                    break;

                case saVar.graphRefLevel:
                    parent.tab_Amplitude.s_displayRefLevel = config[saVar.graphRefLevel].ToString();
                    break;

                case saVar.fftSegment:
                    parent.tab_Video.s_fftSegments = config[saVar.fftSegment].ToString();
                    break;

                case saVar.automaticLevel:
                    parent.tab_Amplitude.s_automaticLevelingEnabled = (bool)config[saVar.automaticLevel];
                    break;

                case saVar.scalePerDivision:
                    parent.tab_Amplitude.s_scalePerDivision = (int)config[saVar.scalePerDivision];
                    break;

                case saVar.fftRBW:
                    parent.tab_Video._fftRBW = config[saVar.fftRBW].ToString();
                    break;

                case saVar.fftOverlap:
                    parent.tab_Video.s_fftOverlap = (((double)config[saVar.fftOverlap]) * 100.0).ToString();
                    break;

                case saVar.refreshRate:
                    parent.tab_Video._displayRefreshRate = (((int)config[saVar.refreshRate]) * 1000).ToString();
                    break;
            }
        }
    }
}