using System.Reflection;
using System.Runtime.InteropServices;
using NLog;
using NLog.Fluent;
using Pothosware.SoapySDR;
using SoapyVNACommon.Extentions;
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
        var currentPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)!;

        var soapyPath = Path.Combine(currentPath!, @"SoapySDR");
        var libsPath = Path.Combine(soapyPath, @"Libs");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            NativeLibrary.SetDllImportResolver(
                Assembly.GetExecutingAssembly(),
                (libraryName, assembly, searchPath) =>
                {
                    if (
                        libraryName.Equals(
                            "Pothosware.SoapySDR.dll",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        // Force it to load the Linux version instead
                        // This prevents the runtime from ever looking for the Windows .dll
                        return NativeLibrary.Load(
                            "Pothosware.SoapySDRLinux.dll",
                            assembly,
                            searchPath
                        );
                    }

                    return IntPtr.Zero;
                }
            );
            NativeLibrary.SetDllImportResolver(
                typeof(Pothosware.SoapySDR.Device).Assembly,
                (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == "SoapySDRCSharpSWIG")
                    {
                        string coreLibPath = Path.Combine(libsPath, "libSoapySDR.so.0.8-3");
                        if (File.Exists(coreLibPath))
                        {
                            NativeLibrary.Load(coreLibPath);
                        }

                        string swigLibPath = Path.Combine(libsPath, "libSoapySDRCSharpSWIG.so");
                        if (File.Exists(swigLibPath))
                        {
                            return NativeLibrary.Load(swigLibPath);
                        }
                    }
                    return IntPtr.Zero;
                }
            );
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Environment.SetEnvironmentVariable(
                "SOAPY_SDR_PLUGIN_PATH",
                Path.Combine(currentPath, @"SoapySDR\root\SoapySDR\lib\SoapySDR\modules0.8-3\")
            );
            Environment.SetEnvironmentVariable(
                "SOAPY_SDR_ROOT",
                Path.Combine(currentPath, @"SoapySDR\root\SoapySDR")
            );
        }
        Logger.Info(
            $"SOAPY_SDR_PLUGIN_PATH -> {Environment.GetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH")}"
        );
        Environment.SetEnvironmentVariable(
            "PATH",
            $"{Environment.GetEnvironmentVariable("PATH")};{soapyPath};{libsPath}"
        );
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

