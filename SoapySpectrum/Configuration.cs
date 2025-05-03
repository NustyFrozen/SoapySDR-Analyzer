using ImGuiNET;
using MathNet.Numerics;
using SoapySA.Extentions;
using SoapySA.View.tabs;
using System.Numerics;

namespace SoapySA;

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
        positionOffset = new(50 * scaleSize.X, 10 * scaleSize.Y),
        mainWindowSize = screenSize,
        graphSize = new(Convert.ToInt16(mainWindowSize.X * .8), Convert.ToInt16(mainWindowSize.Y * .95)),
        optionSize = new(Convert.ToInt16(mainWindowSize.X * .2), Convert.ToInt16(mainWindowSize.Y));

    public static string presetPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Preset");
    public static string calibrationPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Cal");

    public enum saVar
    {
        //frequency
        freqStart,

        freqStop,

        //device
        sampleRate,

        leakageSleep,
        deviecOptions,
        iqCorrection,

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

        tab_Cal.s_AvailableCal = calibrations.ToArray();
        config.CollectionChanged += updateUIElementsOnConfigChanged;
        config.Add(saVar.freqStart, 933.4e6);
        config.Add(saVar.freqStop, 943.4e6);

        config.Add(saVar.sampleRate, 20e6);
        config[saVar.leakageSleep] = 5;
        config.Add(saVar.deviecOptions, new string[] { });
        config[saVar.iqCorrection] = true;

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

    private static void updateUIElementsOnConfigChanged(object? sender, keyOfChangedValueEventArgs e)
    {
        switch (e.key)
        {
            case saVar.freqStart:
                tab_Frequency.s_displayFreqStart = config[saVar.freqStart].ToString();
                break;

            case saVar.freqStop:
                tab_Frequency.s_displayFreqStop = config[saVar.freqStop].ToString();
                break;

            case saVar.sampleRate:
                tab_Device.s_customSampleRate = config[saVar.sampleRate].ToString();
                break;

            case saVar.leakageSleep:
                tab_Device.s_osciliatorLeakageSleep = (int)config[saVar.leakageSleep] / 100.0f;
                break;

            case saVar.iqCorrection:
                tab_Device.s_isCorrectIQEnabled = (bool)config[saVar.iqCorrection];
                break;

            case saVar.graphStartDB:
                tab_Amplitude.s_displayStartDB = config[saVar.graphStartDB].ToString();
                break;

            case saVar.graphStopDB:
                tab_Amplitude.s_displayStopDB = config[saVar.graphStopDB].ToString();
                break;

            case saVar.graphOffsetDB:
                tab_Amplitude.s_displayOffset = config[saVar.graphOffsetDB].ToString();
                break;

            case saVar.graphRefLevel:
                tab_Amplitude.s_displayRefLevel = config[saVar.graphRefLevel].ToString();
                break;

            case saVar.fftSegment:
                tab_Video.s_fftSegments = config[saVar.fftSegment].ToString();
                break;

            case saVar.automaticLevel:
                tab_Amplitude.s_automaticLevelingEnabled = (bool)config[saVar.automaticLevel];
                break;

            case saVar.scalePerDivision:
                tab_Amplitude.s_scalePerDivision = (int)config[saVar.scalePerDivision];
                break;
        }
    }
}