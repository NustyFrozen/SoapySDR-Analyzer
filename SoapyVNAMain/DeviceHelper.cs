using NLog;
using Pothosware.SoapySDR;
using SoapyVNACommon.Extentions;
using Logger = NLog.Logger;

namespace SoapyVNAMain;

public class DeviceHelper
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public static sdrDeviceCOM[] availableDevicesCOM = null;
    public static string[] AvailableDevices = new[] { "No Devices Found" };

    //UI input pereference

    public static void setupSoapyEnvironment()
    {
        var currentPath = Path.GetDirectoryName(Application.ExecutablePath);
        var soapyPath = Path.Combine(currentPath, @"SoapySDR");
        var libsPath = Path.Combine(soapyPath, @"Libs");
        Environment.SetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH",
            Path.Combine(currentPath, @"SoapySDR\root\SoapySDR\lib\SoapySDR\modules0.8-3\"));
        Environment.SetEnvironmentVariable("SOAPY_SDR_ROOT", Path.Combine(currentPath, @"SoapySDR\root\SoapySDR"));
        Environment.SetEnvironmentVariable("PATH",
            $"{Environment.GetEnvironmentVariable("PATH")};{soapyPath};{libsPath}");
    }

    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public static Task refreshDevices()
    {
        AvailableDevices = new[] { "Refreshing Devices..." };
        _logger.Info("Iterating Devices...");
        var devices = Device.Enumerate().ToList();
        var availableDevicesCOM = new List<sdrDeviceCOM>();
        var deviceLabels = new List<string>();
        foreach (var device in devices)
        {
            var idenefiers = string.Empty;
            if (device.ContainsKey("label"))
                idenefiers += $"label={device["label"]},";

            if (device.ContainsKey("driver"))
                idenefiers += $"driver={device["driver"]},";

            if (device.ContainsKey("serial"))
                idenefiers += $"serial={device["serial"]},";

            if (device.ContainsKey("hardware"))
                idenefiers += $"hardware={device["hardware"]}";
            if (idenefiers.EndsWith(","))
                idenefiers = idenefiers.Substring(0, idenefiers.Length - 1);
            _logger.Info($"Opening {idenefiers}");
            var deviceCOM = new sdrDeviceCOM(idenefiers);
            _logger.Info($"Fetching {idenefiers}");
            deviceCOM.fetchSDRData();
            _logger.Info($"added {idenefiers}");
            availableDevicesCOM.Add(deviceCOM);
            deviceLabels.Add(deviceCOM.Descriptor);
        }
        _logger.Info($"Done iterating Devices");
        if (deviceLabels.Count > 0)
        {
            AvailableDevices = deviceLabels.ToArray();
            DeviceHelper.availableDevicesCOM = availableDevicesCOM.ToArray();
        }
        else
        {
            AvailableDevices = new[] { "No Devices Found" };
        }
        return Task.CompletedTask;
    }

    /// <summary>
    ///     gets all of the sdr data to the ui elements
    /// </summary>
}