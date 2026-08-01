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
        SoapyEnvironment.Setup();
        Device.Enumerate();
    }

    /// <summary>
    ///     Driver arguments worth starting from for a given device.
    ///     <para>
    ///         UHD sizes its transport when the driver is constructed, so buffer depth can only be set here
    ///         and never as a stream argument. The stock frame count is small enough that a fast USB stream
    ///         overruns on any scheduling hiccup; more frames simply give the radio somewhere to put samples
    ///         while the host is busy. Frame size is only filled in when the product is recognisable, because
    ///         the ceiling is transport specific - 16360 bytes over USB 3, jumbo 8000 over ethernet - and
    ///         guessing too high on an unknown link causes timeouts rather than throughput.
    ///     </para>
    /// </summary>
    public static string SuggestDeviceArguments(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor) || !descriptor.Contains("driver=uhd", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var product = descriptor.ToLowerInvariant();

        //256 recv frames at 16360 bytes is about 4 MB, which still fits the stock 16 MB usbfs allowance
        if (product.Contains("b200") || product.Contains("b205") || product.Contains("b210"))
            return "num_recv_frames=256,recv_frame_size=16360,num_send_frames=64,send_frame_size=16360";

        if (product.Contains("x3") || product.Contains("n3") || product.Contains("x4") || product.Contains("e3"))
            return "num_recv_frames=256,recv_frame_size=8000,num_send_frames=64,send_frame_size=8000";

        //unknown uhd hardware: deepen the buffers but leave the frame size to the driver
        return "num_recv_frames=256,num_send_frames=64";
    }

    /// <summary>
    ///     Closes a device and opens it again with different driver arguments, then republishes it so every
    ///     widget built afterwards shares the reopened handle. Falls back to reopening without the arguments
    ///     if the driver rejects them, so a bad argument cannot leave the app with no device at all.
    /// </summary>
    public static bool ReopenDevice(int index, string? deviceArgs)
    {
        if (AvailableDevicesCom is null || index < 0 || index >= AvailableDevicesCom.Length)
            return false;

        var existing = AvailableDevicesCom[index];
        var descriptor = existing.Descriptor;
        var wanted = deviceArgs?.Trim() ?? string.Empty;

        if (wanted == (existing.DeviceArgs ?? string.Empty))
            return true;

        try
        {
            //the hardware only allows one handle, so the enumerated one has to go first
            existing.SdrDevice?.Dispose();
        }
        catch (Exception exception)
        {
            Logger.Warn($"could not release {descriptor} -> {exception.Message}");
        }

        try
        {
            Logger.Info($"reopening {descriptor} with '{wanted}'");
            var reopened = new SdrDeviceCom(descriptor, wanted);
            reopened.FetchSdrData();
            AvailableDevicesCom[index] = reopened;
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error($"device rejected arguments '{wanted}' -> {exception.Message}");

            try
            {
                var plain = new SdrDeviceCom(descriptor);
                plain.FetchSdrData();
                AvailableDevicesCom[index] = plain;
                Logger.Warn($"{descriptor} reopened without the arguments");
            }
            catch (Exception fallback)
            {
                Logger.Error($"could not reopen {descriptor} at all -> {fallback.Message}");
            }

            return false;
        }
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

