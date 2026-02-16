using Newtonsoft.Json;
using NLog;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.tabs;

public partial class CalibrationView: TabViewModel
{
    public int selectedCalibrationIndex = -1;
    public string[] availableCalibrations;
    
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public override string tabName => "Calibration";
    private Configuration Config;
    private PerformFft fftManager;
    public CalibrationView(Configuration Config, PerformFft fftManager)
    {
        this.Config = Config;
        this.fftManager = fftManager;
        availableCalibrations = Directory.GetFiles(Global.CalibrationPath)
            .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToArray();
        Config.OnConfigLoadBegin += Config_OnConfigLoadBegin; ;
    }

    private void Config_OnConfigLoadBegin(object? sender, EventArgs e) => loadCalibration();

    public void loadCalibration()
    {
        try
        {
            var calibrationpath = Path.Combine(Global.CalibrationPath,
                $"{
                availableCalibrations[selectedCalibrationIndex]
            }.json");
            fftManager.calibrationData = JsonConvert.DeserializeObject<List<Tuple<float, float>>>(File.ReadAllText(calibrationpath));
            Config.SelectedCalibration = calibrationpath;
            
        }
        catch (Exception e)
        {
            _logger.Error($"Failed to load calibration --> {e.Message}");
        }
    }

    public override void Render()
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