using System.Numerics;
using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using ProtoBuf;
using SoapyRL.View;
using SoapySA;
using SoapySA.View;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;

namespace SoapyVNAMain.View;

[ProtoContract]
public class DefinedWidget
{
    [ProtoIgnore] public bool Attempted;

    [ProtoIgnore] public SdrDeviceCom Device;

    [ProtoMember(1)] public bool IsComplete;
    [ProtoMember(2)] public int WidgetType;

    [ProtoIgnore] public IWidget Window; // interface

    public void SaveWidget(string name)
    {
        var dir = Path.Combine(Global.ConfigPath, name);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (var file = File.Create(Path.Combine(dir, "widget.bin")))
        {
            Serializer.Serialize(file, this);
        }

        using (var file = File.Create(Path.Combine(dir, "devCOM.bin")))
        {
            Serializer.Serialize(file, SdrDeviceComDto.ToDto(Device));
        }
    }

    public static DefinedWidget LoadWidget(string path)
    {
        DefinedWidget results = null;
        using (var file = File.OpenRead(Path.Combine(path, "widget.bin")))
        {
            results = Serializer.Deserialize<DefinedWidget>(file);
        }

        using (var file = File.OpenRead(Path.Combine(path, "devCOM.bin")))
        {
            results.Device = SdrDeviceComDto.FromDto(Serializer.Deserialize<SdrDeviceComDto>(file));
        }

        return results;
    }
}

public class ConfiguratorWindow
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static string _widgetName = "Widget1";
    private static int _selectedWidgetType, _selectedSdr = -1;
    private static readonly string[] WidgetType = new[] { "Spectrum Analyzer", "Return Loss" };

    private static readonly Dictionary<uint, StringList> AvailableRxAnntenna = new();
    private static readonly Dictionary<uint, StringList> AvailableTxAnntenna = new();
    private static int _selectedRxChannel, _selectedRxAnntenna = -1;
    private static int _selectedTxChannel, _selectedTxAnntenna = -1;
    public static string SCustomRxSampleRate = "0", SCustomTxSampleRate = "0";
    public static int SelectedRxSampleRate = -1, SelectedTxSampleRate = -1;
    public static double RxSampleRate, TxSampleRate;

    private static void CreateWidget()
    {
    }

    private static void FetchAvailableAnntennas()
    {
        _selectedRxAnntenna = -1;
        _selectedTxAnntenna = -1;
        AvailableRxAnntenna.Clear();
        AvailableTxAnntenna.Clear();
        var selectedDevice = DeviceHelper.AvailableDevicesCom[_selectedSdr];

        for (var i = 0; i < selectedDevice.AvailableRxChannels; i++)
        {
            if (!AvailableRxAnntenna.ContainsKey((uint)i))
                AvailableRxAnntenna.Add((uint)i, new StringList());
            AvailableRxAnntenna[(uint)i].AddRange(selectedDevice.AvailableRxAntennas[(uint)i]);
        }

        for (var i = 0; i < selectedDevice.AvailableTxChannels; i++)
        {
            if (!AvailableTxAnntenna.ContainsKey((uint)i))
                AvailableTxAnntenna.Add((uint)i, new StringList());
            //i is the channel, j is the anntenna
            if (WidgetsWindow.Widgets.Any(x =>
                {
                    if (x.Value.Device.TxAntenna is null)
                        return false;
                    return x.Value.Device.TxAntenna.Item1 == i;
                }))
                continue;
            AvailableTxAnntenna[(uint)i].AddRange(selectedDevice.AvailableTxAntennas[(uint)i])
                ;
        }
    }

    public static void RenderAddWidget()
    {
        var isvalid = true;
        var noSdrExists = _selectedSdr > DeviceHelper.AvailableDevices.Length ||
                          DeviceHelper.AvailableDevicesCom == null;
        Theme.Text("Create A widget");
        Theme.NewLine();
        Theme.Text("Select An SDR:");
        for (var i = 0; i < DeviceHelper.AvailableDevices.Length; i++)
        {
            var devKwargs = DeviceHelper.AvailableDevices[i];
            if (_selectedSdr == i)
                Theme.TextbuttonTheme.Bgcolor = Color.Green.ToUint();
            if (Theme.DrawTextButton($"#{i + 1} {FontAwesome5.Microchip} {devKwargs}"))
                if (!noSdrExists)
                {
                    _selectedSdr = i;
                    FetchAvailableAnntennas();
                }

            Theme.TextbuttonTheme = Theme.GetTextButtonTheme();
        }

        if (noSdrExists)
            Theme.Text("No SDR found, Cannot Create a widget");

        if (_selectedSdr == -1)
            return;

        Theme.Text("Widget Type");
        Theme.GlowingCombo("chooseWidgetType", ref _selectedWidgetType, WidgetType, Theme.InputTheme);
        Theme.NewLine();
        Theme.Text($"{FontAwesome5.Pencil} Widget Name");
        Theme.GlowingInput("Widget Name", ref _widgetName, Theme.InputTheme);
        if (WidgetsWindow.Widgets.Any(x => x.Key == _widgetName))
        {
            isvalid = false;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{_widgetName} Already Exists");
        }

        isvalid &= _selectedSdr != -1;
        try
        {
            switch (_selectedWidgetType)
            {
                case 0: //spectrum analyzer
                    Theme.NewLine();
                    Theme.Text("select Rx Channel");
                    Theme.GlowingCombo("select Rx Channel", ref _selectedRxChannel,
                        Array.ConvertAll(
                            Enumerable.Range(0, (int)DeviceHelper.AvailableDevicesCom[_selectedSdr].AvailableRxChannels)
                                .ToArray(), Convert.ToString)
                        , Theme.InputTheme);
                    Theme.NewLine();
                    Theme.Text("select Rx Anntenna");
                    Theme.GlowingCombo("select Rx Anntenna", ref _selectedRxAnntenna,
                        AvailableRxAnntenna[(uint)_selectedRxChannel].ToArray()
                        , Theme.InputTheme);
                    Theme.NewLine();
                    Theme.Text("Rx Sample Rate:");
                    //not optimal to do in a loop, but its only on widget creation so performance doesn't matter
                    var combos = Array.ConvertAll(DeviceHelper.AvailableDevicesCom[_selectedSdr]
                            .DeviceRxSampleRates[_selectedRxChannel]
                            .ToList().FindAll(x => x.Maximum == x.Minimum && x.Step == 0).Select(x => x.Minimum)
                            .ToArray(),
                        Convert.ToString);
                    if (Theme.GlowingCombo("selectRXWidget", ref SelectedRxSampleRate, combos, Theme.InputTheme))
                        RxSampleRate = Convert.ToDouble(combos[SelectedRxSampleRate]);
                    isvalid &= _selectedRxAnntenna != -1;
                    break;

                case 1: //Return Loss
                    Theme.Text("select Reflection Channel");
                    Theme.GlowingCombo("select Reflection Channel", ref _selectedRxChannel,
                        Array.ConvertAll(
                            Enumerable.Range(0, (int)DeviceHelper.AvailableDevicesCom[_selectedSdr].AvailableRxChannels)
                                .ToArray(), Convert.ToString)
                        , Theme.InputTheme);
                    Theme.Text("select Reflection Anntenna");
                    Theme.GlowingCombo("select Reflection Anntenna", ref _selectedRxAnntenna,
                        AvailableRxAnntenna[(uint)_selectedRxChannel].ToArray()
                        , Theme.InputTheme);
                    Theme.Text("select Forward Channel");
                    Theme.GlowingCombo("select forward Channel", ref _selectedTxChannel,
                        Array.ConvertAll(
                            Enumerable.Range(0, (int)DeviceHelper.AvailableDevicesCom[_selectedSdr].AvailableTxChannels)
                                .ToArray(), Convert.ToString)
                        , Theme.InputTheme);
                    Theme.Text("select Forward Anntenna");
                    Theme.GlowingCombo("select forward Anntenna", ref _selectedTxAnntenna,
                        AvailableTxAnntenna[(uint)_selectedTxChannel].ToArray()
                        , Theme.InputTheme);
                    Theme.Text("RX & TX Sample Rate:");
                    //not optimal to do in a loop, but its only on widget creation so performance doesn't matter that much
                    combos = Array.ConvertAll(DeviceHelper.AvailableDevicesCom[_selectedSdr]
                        .DeviceRxSampleRates[_selectedRxChannel]
                        .ToList().FindAll(x => x.Maximum == x.Minimum && x.Step == 0 &&
                                               DeviceHelper.AvailableDevicesCom[_selectedSdr]
                                                   .DeviceTxSampleRates[_selectedTxChannel]
                                                   .Any(y => y.Maximum == x.Maximum && y.Step == 0))
                        .Select(x => x.Minimum).ToArray(), Convert.ToString);
                    if (Theme.GlowingCombo("selectRXWidget", ref SelectedRxSampleRate, combos, Theme.InputTheme))
                    {
                        RxSampleRate = Convert.ToDouble(combos[SelectedRxSampleRate]);
                        TxSampleRate = Convert.ToDouble(combos[SelectedRxSampleRate]);
                        ;
                    }

                    isvalid &= _selectedTxAnntenna != -1 && _selectedRxAnntenna != -1;
                    break;
            }
        }
        catch (Exception ex)
        {
        }

        Theme.NewLine();
        var text = isvalid
            ? $"{FontAwesome5.Check} Add Widget"
            : $"{FontAwesome5.Cross} Please Select Antenna, Sample Rate and Channel";
        Theme.TextbuttonTheme.Bgcolor = isvalid ? Color.Green.ToUint() : Color.Red.ToUint();
        if (Theme.DrawTextButton($"{text}") && isvalid)
        {
            var definedSdrCom = new SdrDeviceCom(DeviceHelper.AvailableDevicesCom[_selectedSdr])
            {
                RxSampleRate = RxSampleRate,
                TxSampleRate = TxSampleRate,
                RxAntenna = _selectedRxAnntenna == -1
                    ? null
                    : new Tuple<uint, string>((uint)_selectedRxChannel,
                        AvailableRxAnntenna[(uint)_selectedRxChannel][_selectedRxAnntenna]),
                TxAntenna = _selectedTxAnntenna == -1
                    ? null
                    : new Tuple<uint, string>((uint)_selectedTxChannel,
                        AvailableTxAnntenna[(uint)_selectedTxChannel][_selectedTxAnntenna])
            };

            IWidget widget = null;
            switch (_selectedWidgetType)
            {
                case 0:
                    widget = new MainWindowView(_widgetName, new Vector2(), Configuration.GetScreenSize(),
                        definedSdrCom);
                    break;

                case 1:
                    widget = new MainWindow(_widgetName, new Vector2(), Configuration.GetScreenSize(), definedSdrCom);
                    break;
            }

            var definedwidget = new DefinedWidget
                { WidgetType = _selectedWidgetType, IsComplete = false, Device = definedSdrCom, Window = widget };
            definedwidget.SaveWidget(_widgetName);
            WidgetsWindow.Widgets.Add(_widgetName, definedwidget);
            _selectedSdr = -1;
            SelectedRxSampleRate = -1;
            SelectedTxSampleRate = -1;
            WidgetsWindow.EditMode = false;
        }

        Theme.TextbuttonTheme = Theme.GetTextButtonTheme();
    }

    public static void Render()
    {
        ImGui.SameLine();
        if (Theme.DrawTextButton($"{FontAwesome5.Recycle} Refresh Devices"))
            Task.Run(DeviceHelper.RefreshDevices);
        ImGui.NewLine();
        RenderAddWidget();
    }
}