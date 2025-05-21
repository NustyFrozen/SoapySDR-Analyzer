using ImGuiNET;
using MathNet.Numerics;
using SoapySA.Extentions;
using SoapySA.View;
using System.Numerics;

namespace SoapySA
{
    public class Configuration(MainWindow initiator, Vector2 windowSize, Vector2 Pos)
    {
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

        public static string presetPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Preset");
        public static string calibrationPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Cal");

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

        public void initDefaultConfig()
        {
            if (!Directory.Exists(presetPath))
                Directory.CreateDirectory(presetPath);
            if (!Directory.Exists(calibrationPath))
                Directory.CreateDirectory(calibrationPath);
            var calibrations = new List<string>();
            foreach (var file in Directory.GetFiles(calibrationPath))
                if (file.EndsWith(".cal"))
                    calibrations.Add(file.Replace(calibrationPath, "").Replace("\\", "").Replace("/", "")
                        .Replace(".cal", ""));

            //tab_Cal.s_AvailableCal = calibrations.ToArray();
            config.CollectionChanged += updateUIElementsOnConfigChanged;
            config.Add(saVar.freqStart, 933.4e6);
            config.Add(saVar.freqStop, 943.4e6);
            config[saVar.leakageSleep] = 5;
            config.Add(saVar.deviecOptions, new string[] { });
            config[saVar.iqCorrection] = true;
            config[saVar.freqInterleaving] = true;
            config.Add(saVar.graphStartDB, (double)-136);
            config.Add(saVar.graphStopDB, (double)0);
            config.Add(saVar.graphOffsetDB, (double)0);
            config.Add(saVar.graphRefLevel, (double)-40);
            Func<int, double[]> windowFunction = length => Window.Hamming(length);
            Func<int, double[]> windowFunction_Periodic = length => Window.HammingPeriodic(length);
            config.Add(saVar.fftWindow, windowFunction);
            config[saVar.fftRBW] = 0.01e6;
            config[saVar.fftSegment] = 13;
            config[saVar.fftOverlap] = 0.5;
            config[saVar.refreshRate] = (long)0;
            config[saVar.automaticLevel] = false;
            config[saVar.scalePerDivision] = 20;
            config[saVar.channelBW] = 5e6;
            config[saVar.channelOCP] = 0.9;
        }

        private void updateUIElementsOnConfigChanged(object? sender, keyOfChangedValueEventArgs e)
        {
            switch (e.key)
            {
                case saVar.freqStart:
                    parent.tab_Frequency.s_displayFreqStart = config[saVar.freqStart].ToString();
                    break;

                case saVar.freqStop:
                    parent.tab_Frequency.s_displayFreqStop = config[saVar.freqStop].ToString();
                    break;



                case saVar.leakageSleep:
                    parent.tab_Device.s_osciliatorLeakageSleep = (int)config[saVar.leakageSleep] / 100.0f;
                    break;

                case saVar.iqCorrection:
                    parent.tab_Device.s_isCorrectIQEnabled = (bool)config[saVar.iqCorrection];
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
            }
        }
    }
}