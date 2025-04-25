using ImGuiNET;
using MathNet.Numerics;
using SoapyRL.Extentions;
using SoapyRL.UI;
using System.Numerics;

namespace SoapyRL
{
    public static class Configuration
    {
#if DEBUG
        public static ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar;
        private static Vector2 screenSize = new Vector2(Convert.ToInt16(Screen.PrimaryScreen.Bounds.Width / 1.5), Convert.ToInt16(Screen.PrimaryScreen.Bounds.Height / 1.5));
        public static Vector2 mainWindowPos = new Vector2(600, 0);
#else
        public static ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;
        private static Vector2 screenSize = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        public static Vector2 mainWindowPos = new Vector2(0, 0);
#endif

        public static Vector2
        scaleSize = new Vector2(screenSize.X / 1920.0f, screenSize.Y / 1080.0f),
        positionOffset = new Vector2(50 * Configuration.scaleSize.X, 20 * Configuration.scaleSize.Y),

        mainWindowSize = screenSize,

        graphSize = new Vector2(Convert.ToInt16(mainWindowSize.X * .8), Convert.ToInt16(mainWindowSize.Y * .95)),

        optionSize = new Vector2(Convert.ToInt16(mainWindowSize.X * .2), Convert.ToInt16(mainWindowSize.Y));

        public static string presetPath = Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), $"Preset");
        public static string calibrationPath = Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), $"Cal");

        public enum saVar
        {
            freqStart, freqStop,
            sampleRate, leakageSleep, deviecOptions, iqCorrection,
            graphStartDB, graphStopDB, graphOffsetDB, graphRefLevel,
            fftWindow, fftSize, fftSegmentLength, fftOverlap, refreshRate, automaticLevel, scalePerDivision,
            fftRBW,fftVBW
        }

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
            tab_Cal.s_AvailableCal = calibrations.ToArray();
            Configuration.config.CollectionChanged += updateUIElementsOnConfigChanged;
            Configuration.config.Add(saVar.freqStart, 930e6);
            Configuration.config.Add(saVar.freqStop, 960e6);

            Configuration.config.Add(saVar.sampleRate, (double)20e6);
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
            Configuration.config[saVar.fftSegmentLength] = 13;
            Configuration.config[saVar.fftOverlap] = 0.5;
            Configuration.config[saVar.refreshRate] = (long)0;
            Configuration.config[saVar.automaticLevel] = false;
            Configuration.config[saVar.scalePerDivision] = 20;
            Configuration.config[saVar.fftRBW] = 1e3;
            Configuration.config[saVar.fftVBW] = 5e5;
            PerformFFT.calculateRBWVBW();
        }

        private static void updateUIElementsOnConfigChanged(object? sender, keyOfChangedValueEventArgs e)
        {
            switch (e.key)
            {
                case Configuration.saVar.freqStart:
                    tab_Frequency.s_displayFreqStart = config[saVar.freqStart].ToString();
                    break;

                case Configuration.saVar.freqStop:
                    tab_Frequency.s_displayFreqStop = config[Configuration.saVar.freqStop].ToString();
                    break;

                case Configuration.saVar.sampleRate:
                    tab_Device.s_customSampleRate = config[Configuration.saVar.sampleRate].ToString();
                    break;

                case Configuration.saVar.leakageSleep:
                    tab_Device.s_osciliatorLeakageSleep = (float)(((int)Configuration.config[Configuration.saVar.leakageSleep]) / 100.0f);
                    break;

                case Configuration.saVar.iqCorrection:
                    tab_Device.s_isCorrectIQEnabled = (bool)config[Configuration.saVar.iqCorrection];
                    break;

                case Configuration.saVar.graphStartDB:
                    tab_Amplitude.s_displayStartDB = config[Configuration.saVar.graphStartDB].ToString();
                    break;

                case Configuration.saVar.graphStopDB:
                    tab_Amplitude.s_displayStopDB = config[Configuration.saVar.graphStopDB].ToString();
                    break;

                case Configuration.saVar.graphOffsetDB:
                    tab_Amplitude.s_displayOffset = config[Configuration.saVar.graphOffsetDB].ToString();
                    break;

                case Configuration.saVar.graphRefLevel:
                    tab_Amplitude.s_displayRefLevel = config[Configuration.saVar.graphRefLevel].ToString();
                    break;
                case Configuration.saVar.automaticLevel:
                    tab_Amplitude.s_automaticLevelingEnabled = (bool)Configuration.config[Configuration.saVar.automaticLevel];
                    break;

                case Configuration.saVar.scalePerDivision:
                    tab_Amplitude.s_scalePerDivision = (int)Configuration.config[Configuration.saVar.scalePerDivision];
                    break;
            }
        }
    }
}