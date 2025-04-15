using ImGuiNET;
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
            leakageSleep, deviecOptions, iqCorrection, freqStart, freqStop, txSampleRate, rxSampleRate,
            fftSegment, fftOverlap, scalePerDivision
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
            Configuration.config.CollectionChanged += updateUIElementsOnConfigChanged;

            Configuration.config[saVar.leakageSleep] = 5;
            Configuration.config.Add(saVar.deviecOptions, new string[] { });
            Configuration.config[saVar.iqCorrection] = true;
            Configuration.config[saVar.freqStart] = 100e6;
            Configuration.config[saVar.freqStop] = 200e6;
            Configuration.config[saVar.fftSegment] = 400;
            Configuration.config[saVar.fftOverlap] = 0.75;
            Configuration.config[saVar.scalePerDivision] = 20;
        }

        private static void updateUIElementsOnConfigChanged(object? sender, keyOfChangedValueEventArgs e)
        {
            switch (e.key)
            {
                case Configuration.saVar.leakageSleep:
                    tab_Device.s_osciliatorLeakageSleep = (float)(((int)Configuration.config[Configuration.saVar.leakageSleep]) / 100.0f);
                    break;

                case Configuration.saVar.iqCorrection:
                    tab_Device.s_isCorrectIQEnabled = (bool)config[Configuration.saVar.iqCorrection];
                    break;
            }
        }
    }
}