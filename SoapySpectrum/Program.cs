using Microsoft.Win32;
using SoapySpectrum;
using SoapySpectrum.Extentions;
using SoapySpectrum.UI;
Imports.AllocConsole();
// To customize application configuration such as set high DPI settings or default font,
// see https://aka.ms/applicationconfiguration.
ApplicationConfiguration.Initialize();
Configuration.initDefaultConfig();
using var overlay = new UI();
await overlay.Run();


//make sure to close soapy_power before goin bye bye
SystemEvents.SessionEnded += (s, e) =>
{
    SoapyPower.stopStream();
    while (SoapyPower.isSoapyPowerRunning()) { } //wait for the thread to terminate
};