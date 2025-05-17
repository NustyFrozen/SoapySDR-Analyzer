// To customize application configuration such as set high DPI settings or default font,
// see https://aka.ms/applicationconfiguration.
using SoapyVNAMain;
using SoapyVNAMain.View;

ApplicationConfiguration.Initialize();
DeviceHelper.setupSoapyEnvironment();
using var overlay = new WidgetsWindow();
await overlay.Run();