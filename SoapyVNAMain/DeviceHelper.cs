using NLog;
using Pothosware.SoapySDR;
using SoapyVNACommon.Extentions;
using System.Runtime.InteropServices;
using Logger = NLog.Logger;

namespace SoapyVNAMain;

public class DeviceHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static SdrDeviceCom[]? AvailableDevicesCom;
    public static string[] AvailableDevices = new[] { "No Devices Found" };

    //UI input pereference

    public static void SetupSoapyEnvironment()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var currentPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            var soapyPath = Path.Combine(currentPath, @"SoapySDR");
            var libsPath = Path.Combine(soapyPath, @"Libs");
            Environment.SetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH",
                Path.Combine(currentPath, @"SoapySDR\root\SoapySDR\lib\SoapySDR\modules0.8-3\"));
            Environment.SetEnvironmentVariable("SOAPY_SDR_ROOT", Path.Combine(currentPath, @"SoapySDR\root\SoapySDR"));
            Environment.SetEnvironmentVariable("PATH",
                $"{Environment.GetEnvironmentVariable("PATH")};{soapyPath};{libsPath}");
           
        }
        Device.Enumerate();
    }

    /// <summary>
    ///     enumrates over the available devices and updates the UI accordingly
    /// </summary>
    public static Task RefreshDevices()
    {
        AvailableDevices = new[] { "Refreshing Devices..." };
        Logger.Info("Iterating Devices...");
        var devices = Device.Enumerate().ToList();
        var availableDevicesCom = new List<SdrDeviceCom>();
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
            Logger.Info($"Opening {idenefiers}");
            var deviceCom = new SdrDeviceCom(idenefiers);
            Logger.Info($"Fetching {idenefiers}");
            deviceCom.FetchSdrData();
            Logger.Info($"added {idenefiers}");
            availableDevicesCom.Add(deviceCom);
            deviceLabels.Add(deviceCom.Descriptor);
        }

        Logger.Info("Done iterating Devices");
        if (deviceLabels.Count > 0)
        {
            AvailableDevices = deviceLabels.ToArray();
            DeviceHelper.AvailableDevicesCom = availableDevicesCom.ToArray();
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