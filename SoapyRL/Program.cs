using SoapyRL;
using SoapyRL.Extentions;
using SoapyRL.UI;

Imports.AllocConsole();
// To customize application configuration such as set high DPI settings or default font,
// see https://aka.ms/applicationconfiguration.
ApplicationConfiguration.Initialize();
Configuration.initDefaultConfig();
using var overlay = new UI();
await overlay.Run();