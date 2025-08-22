using Newtonsoft.Json;
using NLog;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.tabs;

public partial class CalibrationView(MainWindowView initiator)
{
    MainWindowView _parent = initiator;
    public List<Tuple<float, float>>? calibrationData;
    public int selectedCalibrationIndex = -1;
    public string[] availableCalibrations;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public void loadCalibration()
    {
        try
        {
            var calibrationpath = Path.Combine(Global.CalibrationPath,
                $"{
                availableCalibrations[selectedCalibrationIndex]
            }.json");
            calibrationData = JsonConvert.DeserializeObject<List<Tuple<float, float>>>(File.ReadAllText(calibrationpath));
            _parent.Configuration.Config[Configuration.SaVar.selectedCalibration] = calibrationpath;
        }
        catch (Exception e)
        {
            _logger.Error($"Failed to load calibration --> {e.Message}");
        }
    }

    public void renderCalibration()
    {
        Theme.Text("Calibration", Theme.InputTheme);
        if (Theme.GlowingCombo("calibrationSelector", 
                ref selectedCalibrationIndex,availableCalibrations,
                Theme.InputTheme))
        {
            loadCalibration();
        }
        
    }
}