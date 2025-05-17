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

    private static string insertNewLines(string text, int X)
    {
        if (text.Length <= X) return text; // No need to wrap short items

        var formattedText = new List<char>();
        for (var i = 0; i < text.Length; i++)
        {
            formattedText.Add(text[i]);
            if ((i + 1) % X == 0) formattedText.Add('\n');
        }

        return new string(formattedText.ToArray());
    }

    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public static Task refreshDevices()
    {
        AvailableDevices = new[] { "Refreshing Devices..." };
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
            var deviceCOM = new sdrDeviceCOM(new Device(device));
            deviceCOM.fetchSDRData();

            availableDevicesCOM.Add(deviceCOM);
            deviceLabels.Add(insertNewLines(idenefiers, 60));
        }

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