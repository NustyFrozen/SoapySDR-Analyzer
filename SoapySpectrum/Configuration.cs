using ImGuiNET;
using MathNet.Numerics;
using System.Numerics;
namespace SoapySpectrum
{
    public static class Configuration
    {
#if DEBUG
        public static ImGuiWindowFlags mainWindow_flags = ImGuiWindowFlags.NoScrollbar;
        private static Vector2 screenSize = new Vector2(Convert.ToInt16(Screen.PrimaryScreen.Bounds.Width / 1.5), Convert.ToInt16(Screen.PrimaryScreen.Bounds.Height / 1.5));
        public static Vector2 mainWindow_Pos = new Vector2(600, 0);
#else
        public static ImGuiWindowFlags mainWindow_flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;
        private static Vector2 screenSize = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        public static Vector2 mainWindow_Pos = new Vector2(0, 0);
#endif

        public static Vector2
        scale_Size = new Vector2(screenSize.X / 1920.0f, screenSize.Y / 1080.0f),

        mainWindow_Size = screenSize,

        graph_Size = new Vector2(Convert.ToInt16(mainWindow_Size.X * .8), Convert.ToInt16(mainWindow_Size.Y * .95)),

        option_Size = new Vector2(Convert.ToInt16(mainWindow_Size.X * .2), Convert.ToInt16(mainWindow_Size.Y));

        public static Dictionary<string, object> config = new Dictionary<string, object>();
        public static Dictionary<float, float> calibrationData = new Dictionary<float, float>();
        public static bool hasCalibration = false;
        public static void loadCalibrationData()
        {
            hasCalibration = true;
            string[] data = File.ReadAllText("cal.csv").Split('\n');
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == string.Empty) break;
                string[] tempLine = data[i].Split(',');
                calibrationData.Add((float)Convert.ToDouble(tempLine[0]), (float)Convert.ToDouble(tempLine[2]));
            }
        }
        public static void initDefaultConfig()
        {
            if (File.Exists("cal.csv"))
            {
                loadCalibrationData();
            }
            Configuration.config.Add("freqStart", 80e6);
            Configuration.config.Add("freqStop", 120e6);

            Configuration.config.Add("sampleRate", (double)20e6);
            Configuration.config["leakageSleep"] = 5;
            Configuration.config.Add("devicesOptions", new string[] { });
            Configuration.config["IQCorrection"] = true;

            Configuration.config.Add("graph_startDB", (double)-136);
            Configuration.config.Add("graph_endDB", (double)0);
            Configuration.config.Add("graph_OffsetDB", (double)0);
            Configuration.config.Add("graph_RefLevel", (double)0);
            Func<int, double[]> windowFunction = length => Window.Hamming(length);
            Func<int, double[]> windowFunction_Periodic = length => Window.HammingPeriodic(length);
            Configuration.config.Add("FFT_WINDOW", windowFunction);
            Configuration.config.Add("FFT_WINDOW_PERIODIC", windowFunction_Periodic);
            Configuration.config["FFT_Size"] = 32768;
            Configuration.config["FFT_segments"] = 22;
            Configuration.config["FFT_overlap"] = 0.5;
            Configuration.config["refreshRate"] = (long)0;
        }
    }
}
