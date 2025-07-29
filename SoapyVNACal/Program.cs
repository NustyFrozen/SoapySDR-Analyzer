using SoapyVNACal;
using SoapyVNAMain;

DeviceHelper.SetupSoapyEnvironment();
Console.Title = "SoapyVNA Calibration";
Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.White;



selectDevice:
DeviceHelper.RefreshDevices();
var options = DeviceHelper.AvailableDevices;
options = options.Append("Refresh Devices").ToArray();

var option = Calibrator.selectOption("Please Select a Device:", options);
if (option == options.Length - 1)
    goto selectDevice;
var device = DeviceHelper.AvailableDevicesCom[option];
Console.Clear();
Calibrator.calibrateRX(device, 0, "TX/RX", 900e6, 2000e6, 100e6, -40);
selectCalibrationType:
options = new String[] { "TX", "RX" };


//0 = TX, 1 = RX
var calibraionType = Calibrator.selectOption("Please Select Calibration Type:", options);

uint channels = (calibraionType == 0) ? device.AvailableTxChannels : device.AvailableRxChannels;
if (channels == 0)
{
    Console.WriteLine($"The Selected Device does not have any {options[calibraionType]} channels!");
    goto selectCalibrationType;
}
uint selectedChannel = (uint)Calibrator.selectOption("Please Select Channel:",
    Enumerable.Range(0, (int)channels).Select(x => x.ToString()).ToArray());
var Antennas = (calibraionType == 0) ? device.AvailableTxAntennas[selectedChannel].ToArray() : device.AvailableRxAntennas[selectedChannel].ToArray();
var selectedAntenna = Antennas[Calibrator.selectOption("Please Select Antenna", Antennas)];


Console.Clear();
selectRange:
Console.WriteLine("SWEEP MODE, (start, stop, hop)");
Console.WriteLine("Please select Start Frequency (hz):");
var freqStart = Console.ReadLine();
Console.WriteLine("Please select Stop Frequency (hz):");
var freqStop = Console.ReadLine();
Console.WriteLine("Please select hop size (hz)");
var hopSize = Console.ReadLine();
if (!double.TryParse(freqStart, out double freqStart_d) || !double.TryParse(freqStop, out double freqStop_d) ||
    !double.TryParse(hopSize, out double hopSize_d))
{
    Console.WriteLine($"{freqStart} or {freqStop} or {hopSize} is not a valid double type");
    goto selectRange;
}

var frequencyRange = (calibraionType == 0) ? device.DeviceTxFrequencyRange[(int)selectedChannel] : device.DeviceRxFrequencyRange[(int)selectedChannel];
if (!frequencyRange.Any(x => x.Minimum <= freqStart_d && x.Maximum >= freqStop_d))
{
    Console.WriteLine($"The selected Device & channel does not support the frequency range {freqStart_d} - {freqStop_d} Hz");
    goto selectRange;
}
Console.Clear();
selectLevel:
Console.WriteLine("what level are you transmitting (dBm):");
if (!double.TryParse(Console.ReadLine(), out double expecteddB))
{
    Console.WriteLine($"{expecteddB} is not a valid Double");
    goto selectLevel;
}
Calibrator.calibrateRX(device, selectedChannel, selectedAntenna, freqStart_d, freqStop_d, hopSize_d, expecteddB);