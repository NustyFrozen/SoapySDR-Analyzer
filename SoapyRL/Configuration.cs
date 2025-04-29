using System.Numerics;
using ImGuiNET;
using SoapyRL.Extentions;
using SoapyRL.View.tabs;

namespace SoapyRL;

public static class Configuration
{
#if DEBUG
        public static ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar;
        private static Vector2 screenSize =
 new Vector2(Convert.ToInt16(Screen.PrimaryScreen.Bounds.Width / 1.5), Convert.ToInt16(Screen.PrimaryScreen.Bounds.Height / 1.5));
        public static Vector2 mainWindowPos = new Vector2(600, 0);
#else
    public static ImGuiWindowFlags mainWindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar |
                                                     ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;

    private static readonly Vector2 screenSize = new(Screen.PrimaryScreen.Bounds.Width,
        Screen.PrimaryScreen.Bounds.Height);

    public static Vector2 mainWindowPos = new(0, 0);
#endif

    public static Vector2
        scaleSize = new(screenSize.X / 1920.0f, screenSize.Y / 1080.0f),
        positionOffset = new(50 * scaleSize.X, 20 * scaleSize.Y),
        mainWindowSize = screenSize,
        graphSize = new(Convert.ToInt16(mainWindowSize.X * .8), Convert.ToInt16(mainWindowSize.Y * .95)),
        optionSize = new(Convert.ToInt16(mainWindowSize.X * .2), Convert.ToInt16(mainWindowSize.Y));

    public static string presetPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Preset");
    public static string calibrationPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Cal");

    public enum saVar
    {
        leakageSleep,
        deviecOptions,
        iqCorrection,
        freqStart,
        freqStop,
        txSampleRate,
        rxSampleRate,
        fftSegment,
        fftOverlap,
        scalePerDivision
    }

    public static ObservableDictionary<saVar, object> config = new();

    public static void initDefaultConfig()
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

        config.CollectionChanged += updateUIElementsOnConfigChanged;

        config[saVar.leakageSleep] = 5;
        config.Add(saVar.deviecOptions, new string[] { });
        config[saVar.iqCorrection] = true;
        config[saVar.freqStart] = 100e6;
        config[saVar.freqStop] = 200e6;
        config[saVar.fftSegment] = 400;
        config[saVar.fftOverlap] = 0.95;
        config[saVar.scalePerDivision] = 20;
    }

    private static void updateUIElementsOnConfigChanged(object? sender, keyOfChangedValueEventArgs e)
    {
        switch (e.key)
        {
            case saVar.leakageSleep:
                tab_Device.s_osciliatorLeakageSleep = (int)config[saVar.leakageSleep] / 100.0f;
                break;

            case saVar.iqCorrection:
                tab_Device.s_isCorrectIQEnabled = (bool)config[saVar.iqCorrection];
                break;
        }
    }
}