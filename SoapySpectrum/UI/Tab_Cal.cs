using ClickableTransparentOverlay;
using Design_imGUINET;
using ImGuiNET;

namespace SoapySpectrum.UI
{
    public partial class UI : Overlay
    {
        static float dbTol = -80, inputDB = -40;
        public void renderCalibration()
        {
            ImGui.Text($"{FontAwesome5.ArrowLeft} Cable Atten:");
            ImGui.SliderFloat("Db Tolerance", ref dbTol, -114, -20);
            ImGui.SliderFloat("input DB", ref inputDB, -114, -20);
            ImGui.Text($"Calibration Status:\n FFT Window {FFTWindow[selectedFFTWINDOW]}\nstill calibrating: {calibrating}");
            if (ImGui.Button("Begin calibration"))
            {
                beginCalibration();
            }
            if (ImGui.Button("Save Deltas"))
            {
                getAllDeltas();
            }
        }
        static SortedDictionary<float, Tuple<float, float>> calibrationData = new SortedDictionary<float, Tuple<float, float>>();
        static bool calibrating = false;
        public static void beginCalibration()
        {
            calibrationData.Clear();
            traces[0].viewStatus = traceViewStatus.active;
            traces[0].dataStatus = traceDataStatus.maxHold;
            calibrating = true;
        }
        public static KeyValuePair<float, float> findMaxHoldRange(SortedDictionary<float, float> range, float start, float stop)
        {
            KeyValuePair<float, float> results = new KeyValuePair<float, float>(0, -1000);
            foreach (KeyValuePair<float, float> sample in range)
                if (sample.Value > results.Value && sample.Key >= start && sample.Key <= stop)
                    results = sample;

            return results;
        }
        public static void getAllDeltas()
        {

            traces[0].viewStatus = traceViewStatus.view;
            lock (traces[0].plot)
            {
                foreach (KeyValuePair<float, float> sample in traces[0].plot)
                {
                    if (sample.Value < dbTol) continue;
                    calibrationData.Add(sample.Key, new Tuple<float, float>(sample.Value, calculateDBInput(inputDB, sample.Value)));
                    Logger.Info($"{sample.Key} {sample.Value} dB+ {calculateDBInput(inputDB, sample.Value)}");
                }
            }
            saveToCsvFile();
        }
        //calibrationData.Add(x.Key, new Tuple<float, float>(x.Value, calculateDBInput(inputDB, x.Value) - Atten));
        public static void saveToCsvFile()
        {
            string csvFormat = "";
            foreach (KeyValuePair<float, Tuple<float, float>> data in calibrationData)
            {
                csvFormat += $"{data.Key},{data.Value.Item1},{data.Value.Item2}\n";
            }
            Logger.Info("Calibration Results:");
            Logger.Info(csvFormat);
            File.WriteAllText($"Calibration Results-{FFTWindow[selectedFFTWINDOW]}-{display_FreqStart.Replace(".", "")}-{display_FreqStop.Replace(".", "")}-{DateTime.Now.ToString("MM-dd-yyy")}.csv", csvFormat);
        }
        public static float calculateDBInput(float input, float results)
        {
            //example got -70 input -40 //Atten 8 db
            return Math.Abs(results) - Math.Abs(input); // +30 DB
        }
    }
}
