using ImGuiNET;
using SoapySpectrum.soapypower;
using System.DirectoryServices.ActiveDirectory;
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
        mainWindow_Size = screenSize,
        graph_Size = new Vector2(Convert.ToInt16(mainWindow_Size.X * .8), Convert.ToInt16(mainWindow_Size.Y * .9)),
        option_Size = new Vector2(Convert.ToInt16(mainWindow_Size.X * .2), Convert.ToInt16(mainWindow_Size.Y)),
        input_Size = new Vector2(mainWindow_Size.X / 4, mainWindow_Size.X / 4); //square on purpose
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
            SoapyPower.initializeMinDB();
        }
        public static void initDefaultConfig()
        {
            if(File.Exists("cal.csv"))
            {
                loadCalibrationData();
            }
            Configuration.config.Add("freqStart", 930e6);
            Configuration.config.Add("freqStop", 960e6);
            Configuration.config.Add("sampleRate", (double)20e6);
            Configuration.config.Add("sampleRateOptions", new string[] { });
            Configuration.config.Add("devicesOptions", new string[] { });
            Configuration.config.Add("graph_startDB", (double)-136);
            Configuration.config.Add("graph_endDB", (double)0);
            Configuration.config.Add("graph_OffsetDB", (double)0);
            Configuration.config["FFTSize"] = 512;
            Configuration.config["weleching"] = 400;
            Configuration.config["driver"] = "uhd";
            Configuration.config["additional"] = "-g 0";
        }
    }
}
