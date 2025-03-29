using ImGuiNET;
using MathNet.Numerics;
using SoapySpectrum.Extentions;
using SoapySpectrum.UI;
using System.Numerics;
namespace SoapySpectrum
{
    public static class Configuration
    {
#if DEBUG
        public const ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar;
        private static readonly Vector2 screenSize = new Vector2(Convert.ToInt16(Screen.PrimaryScreen.Bounds.Width / 1.5), Convert.ToInt16(Screen.PrimaryScreen.Bounds.Height / 1.5));
        public static readonly Vector2 mainWindowPos = new Vector2(600, 0);
#else
        public static ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;
        private static Vector2 screenSize = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        public static Vector2 mainWindowPos = new Vector2(0, 0);
#endif

        public static readonly Vector2
        scaleSize = new Vector2(screenSize.X / 1920.0f, screenSize.Y / 1080.0f),

        mainWindowSize = screenSize,

        graphSize = new Vector2(Convert.ToInt16(mainWindowSize.X * .8), Convert.ToInt16(mainWindowSize.Y * .95)),

        optionSize = new Vector2(Convert.ToInt16(mainWindowSize.X * .2), Convert.ToInt16(mainWindowSize.Y));
        public static readonly string presetPath = Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), $"Preset");
        public static readonly string calibrationPath = Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), $"Cal");

        public static ObservableDictionary<saVar, object> config = new ObservableDictionary<saVar, object>();
        public static void initDefaultConfig()
        {
            if (!Directory.Exists(presetPath))
                Directory.CreateDirectory(presetPath);
            if (!Directory.Exists(calibrationPath))
                Directory.CreateDirectory(calibrationPath);
            var calibrations = new List<string>();
            foreach (var file in Directory.GetFiles(calibrationPath))
            {
                if (file.EndsWith($".cal"))
                {
                    calibrations.Add(file.Replace(calibrationPath, "").Replace($"\\", "").Replace($"/", "").Replace($".cal", ""));
                }
            }
            tab_Cal.calibrations = calibrations.ToArray();
            Configuration.config.CollectionChanged += updateUIElementsOnConfigChanged;
            Configuration.config[saVar.leakageSleep] = 5;
            Configuration.config.Add(saVar.deviecOptions, new string[] { });
            Configuration.config[saVar.iqCorrection] = true;

            Configuration.config.Add(saVar.graphStartDB, (double)-136);
            Configuration.config.Add(saVar.graphStopDB, (double)0);
            Configuration.config.Add(saVar.graphOffsetDB, (double)0);
            Configuration.config.Add(saVar.graphRefLevel, (double)-40);
            Func<int, double[]> windowFunction = length => Window.Hamming(length);
            Func<int, double[]> windowFunction_Periodic = length => Window.HammingPeriodic(length);
            Configuration.config.Add(saVar.fftWindow, windowFunction);
            Configuration.config[saVar.fftSize] = 4096;
            Configuration.config[saVar.fftSegment] = 13;
            Configuration.config[saVar.fftOverlap] = 0.5;
            Configuration.config[saVar.refreshRate] = (long)0;
            Configuration.config[saVar.automaticLevel] = false;
            Configuration.config[saVar.scalePerDivision] = 20;
        }

        private static void updateUIElementsOnConfigChanged(object? sender, keyOfChangedValueEventArgs e)
        {
            switch (e.key)
            {
                case saVar.leakageSleep:
                    tab_Device.leakageSleep = (float)(((int)Configuration.config[saVar.leakageSleep]) / 100.0f);
                    break;
                case saVar.iqCorrection:
                    tab_Device.correctIQ = (bool)config[saVar.iqCorrection];
                    break;
                case saVar.graphStartDB:
                    tab_Amplitude.displayStartDB = config[saVar.graphStartDB].ToString();
                    break;
                case saVar.graphStopDB:
                    tab_Amplitude.displayStopDB = config[saVar.graphStopDB].ToString();
                    break;
                case saVar.graphOffsetDB:
                    tab_Amplitude.displayOffset = config[saVar.graphOffsetDB].ToString();
                    break;
                case saVar.graphRefLevel:
                    tab_Amplitude.displayRefLevel = config[saVar.graphRefLevel].ToString();
                    break;
                case saVar.fftSize:
                    Enumerable.Range(0, tab_Video.FFTLength.Length).Where(i => tab_Video.FFTLength[i] == config[saVar.fftSize]);
                    break;
                case saVar.fftSegment:
                    tab_Video.FFT_segments = Configuration.config[saVar.fftSegment].ToString();
                    break;
                case saVar.automaticLevel:
                    tab_Amplitude.automaticLeveling = (bool)Configuration.config[saVar.automaticLevel];
                    break;
                case saVar.scalePerDivision:
                    tab_Amplitude.scalePerDivision = (int)Configuration.config[saVar.scalePerDivision];
                    break;
            }
        }
    }
}
