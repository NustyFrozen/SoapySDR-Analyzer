using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using ProtoBuf;
using SoapySA;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using System.Numerics;

namespace SoapyVNAMain.View
{
    [ProtoContract]
    public class definedWidget
    {
        [ProtoMember(1)]
        public bool isComplete;

        [ProtoMember(2)]
        public int widgetType;

        [ProtoIgnore]
        public bool attempted;

        [ProtoIgnore]
        public Widget window;  // interface

        [ProtoIgnore]
        public sdrDeviceCOM device;

        public void saveWidget(string name)
        {
            string dir = Path.Combine(Global.configPath, name);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var file = File.Create(Path.Combine(dir, "widget.bin")))
            {
                Serializer.Serialize(file, this);
            }
            using (var file = File.Create(Path.Combine(dir, "devCOM.bin")))
            {
                Serializer.Serialize(file, SdrDeviceComDTO.ToDTO(device));
            }
        }

        public static definedWidget loadWidget(string path)
        {
            definedWidget results = null;
            using (var file = File.OpenRead(Path.Combine(path, "widget.bin")))
            {
                results = Serializer.Deserialize<definedWidget>(file);
            }
            using (var file = File.OpenRead(Path.Combine(path, "devCOM.bin")))
            {
                results.device = SdrDeviceComDTO.FromDTO(Serializer.Deserialize<SdrDeviceComDTO>(file));
            }
            return results;
        }
    }

    public class ConfiguratorWindow
    {
        private static readonly NLog.Logger _logger = LogManager.GetCurrentClassLogger();
        private static string widgetName = "Widget1";
        private static int selectedWidgetType = 0, selectedSDR = -1;
        private static string[] widgetType = new string[] { "Spectrum Analyzer", "Return Loss" };

        private static Dictionary<uint, StringList> availableRxAnntenna = new Dictionary<uint, StringList>();
        private static Dictionary<uint, StringList> availableTxAnntenna = new Dictionary<uint, StringList>();
        private static int _selectedRxChannel = 0, _selectedRxAnntenna = -1;
        private static int _selectedTxChannel = 0, _selectedTxAnntenna = -1;
        public static string s_customRxSampleRate = "0", s_customTxSampleRate = "0";
        public static int _selectedRxSampleRate = -1, _selectedTxSampleRate = -1;
        public static double RxSampleRate, TxSampleRate;

        private static void createWidget()
        {
        }

        private static void fetchAvailableAnntennas()
        {
            _selectedRxAnntenna = -1;
            _selectedTxAnntenna = -1;
            availableRxAnntenna.Clear();
            availableTxAnntenna.Clear();
            var selectedDevice = DeviceHelper.availableDevicesCOM[selectedSDR];

            for (int i = 0; i < selectedDevice.availableRxChannels; i++)
            {
                if (!availableRxAnntenna.ContainsKey((uint)i))
                    availableRxAnntenna.Add((uint)i, new StringList());
                availableRxAnntenna[(uint)i].AddRange(selectedDevice.availableRxAntennas[(uint)i]);
            }

            for (int i = 0; i < selectedDevice.availableTxChannels; i++)
            {
                if (!availableTxAnntenna.ContainsKey((uint)i))
                    availableTxAnntenna.Add((uint)i, new StringList());
                //i is the channel, j is the anntenna
                if (WidgetsWindow.Widgets.Any(x =>
                    {
                        if (x.Value.device.txAntenna is null)
                            return false;
                        else
                            return x.Value.device.txAntenna.Item1 == i;
                    }))
                    continue;
                else
                    availableTxAnntenna[(uint)i].AddRange(selectedDevice.availableTxAntennas[(uint)i])
                        ;
            }
        }

        public static void renderAddWidget()
        {
            bool isvalid = true;
            bool noSDRExists = selectedSDR > DeviceHelper.AvailableDevices.Length ||
                               DeviceHelper.availableDevicesCOM == null;
            Theme.Text($"Create A widget");
            Theme.newLine();
            Theme.Text($"Select An SDR:");
            for (int i = 0; i < DeviceHelper.AvailableDevices.Length; i++)
            {
                var devKwargs = DeviceHelper.AvailableDevices[i];
                if (selectedSDR == i)
                    Theme.textbuttonTheme.bgcolor = Color.Green.ToUint();
                if (Theme.drawTextButton($"#{i + 1} {FontAwesome5.Microchip} {devKwargs}"))
                {
                    if (!noSDRExists)
                    {
                        selectedSDR = i;
                        fetchAvailableAnntennas();
                    }
                }
                Theme.textbuttonTheme = Theme.getTextButtonTheme();
            }

            if (noSDRExists)
                Theme.Text($"No SDR found, Cannot Create a widget");

            if (selectedSDR == -1)
                return;

            Theme.Text("Widget Type");
            Theme.glowingCombo("chooseWidgetType", ref selectedWidgetType, widgetType, Theme.inputTheme);
            Theme.newLine();
            Theme.Text($"{FontAwesome5.Pencil} Widget Name");
            Theme.glowingInput($"Widget Name", ref widgetName, Theme.inputTheme);
            if (WidgetsWindow.Widgets.Any(x => x.Key == widgetName))
            {
                isvalid = false;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{widgetName} Already Exists");
            }
            isvalid &= selectedSDR != -1;
            try
            {
                switch (selectedWidgetType)
                {
                    case 0: //spectrum analyzer
                        Theme.newLine();
                        Theme.Text("select Rx Channel");
                        Theme.glowingCombo("select Rx Channel", ref _selectedRxChannel,
                            Array.ConvertAll(Enumerable.Range(0, (int)DeviceHelper.availableDevicesCOM[selectedSDR].availableRxChannels).ToArray(), Convert.ToString)
                            , Theme.inputTheme);
                        Theme.newLine();
                        Theme.Text("select Rx Anntenna");
                        Theme.glowingCombo("select Rx Anntenna", ref _selectedRxAnntenna,
                                                availableRxAnntenna[(uint)_selectedRxChannel].ToArray()
                                                , Theme.inputTheme);
                        Theme.newLine();
                        Theme.Text("Rx Sample Rate:");
                        //not optimal to do in a loop, but its only on widget creation so performance doesn't matter
                        var combos = Array.ConvertAll(DeviceHelper.availableDevicesCOM[selectedSDR].deviceRxSampleRates[_selectedRxChannel]
                            .ToList().FindAll(x => x.Maximum == x.Minimum && x.Step == 0).Select(x => x.Minimum).ToArray(), Convert.ToString);
                        if (Theme.glowingCombo("selectRXWidget", ref _selectedRxSampleRate, combos, Theme.inputTheme))
                            RxSampleRate = Convert.ToDouble(combos[_selectedRxSampleRate]);
                        isvalid &= _selectedRxAnntenna != -1;
                        break;

                    case 1: //Return Loss
                        Theme.Text("select Reflection Channel");
                        Theme.glowingCombo("select Reflection Channel", ref _selectedRxChannel,
                            Array.ConvertAll(Enumerable.Range(0, (int)DeviceHelper.availableDevicesCOM[selectedSDR].availableRxChannels).ToArray(), Convert.ToString)
                            , Theme.inputTheme);
                        Theme.Text("select Reflection Anntenna");
                        Theme.glowingCombo("select Reflection Anntenna", ref _selectedRxAnntenna,
                                                availableRxAnntenna[(uint)_selectedRxChannel].ToArray()
                                                , Theme.inputTheme);
                        Theme.Text("select Forward Channel");
                        Theme.glowingCombo("select forward Channel", ref _selectedTxChannel,
                            Array.ConvertAll(Enumerable.Range(0, (int)DeviceHelper.availableDevicesCOM[selectedSDR].availableTxChannels).ToArray(), Convert.ToString)
                            , Theme.inputTheme);
                        Theme.Text("select Forward Anntenna");
                        Theme.glowingCombo("select forward Anntenna", ref _selectedTxAnntenna,
                                                availableTxAnntenna[(uint)_selectedTxChannel].ToArray()
                                                , Theme.inputTheme);
                        Theme.Text("RX & TX Sample Rate:");
                        //not optimal to do in a loop, but its only on widget creation so performance doesn't matter that much
                        combos = Array.ConvertAll(DeviceHelper.availableDevicesCOM[selectedSDR].deviceRxSampleRates[_selectedRxChannel]
                            .ToList().FindAll(x => x.Maximum == x.Minimum && x.Step == 0 &&
                            DeviceHelper.availableDevicesCOM[selectedSDR].deviceTxSampleRates[_selectedTxChannel].Any(y => y.Maximum == x.Maximum && y.Step == 0)).Select(x => x.Minimum).ToArray(), Convert.ToString);
                        if (Theme.glowingCombo("selectRXWidget", ref _selectedRxSampleRate, combos, Theme.inputTheme))
                        {
                            RxSampleRate = Convert.ToDouble(combos[_selectedRxSampleRate]);
                            TxSampleRate = Convert.ToDouble(combos[_selectedRxSampleRate]); ;
                        }
                        isvalid &= _selectedTxAnntenna != -1 && _selectedRxAnntenna != -1;
                        break;
                }
            }
            catch (Exception ex)
            {
            }
            Theme.newLine();
            var text = isvalid ? $"{FontAwesome5.Check} Add Widget" : $"{FontAwesome5.Cross} Please Select Antenna, Sample Rate and Channel";
            Theme.textbuttonTheme.bgcolor = isvalid ? Color.Green.ToUint() : Color.Red.ToUint();
            if (Theme.drawTextButton($"{text}") && isvalid)
            {
                var definedSdrCom = new sdrDeviceCOM(DeviceHelper.availableDevicesCOM[selectedSDR])
                {
                    rxSampleRate = RxSampleRate,
                    txSampleRate = TxSampleRate,
                    rxAntenna = (_selectedRxAnntenna == -1) ? null :
                        new Tuple<uint, string>((uint)_selectedRxChannel,
                        availableRxAnntenna[(uint)_selectedRxChannel][_selectedRxAnntenna]),
                    txAntenna = (_selectedTxAnntenna == -1) ? null :
                        new Tuple<uint, string>((uint)_selectedTxChannel,
                        availableTxAnntenna[(uint)_selectedTxChannel][_selectedTxAnntenna])
                };

                Widget widget = null;
                switch (selectedWidgetType)
                {
                    case 0:
                        widget = new SoapySA.View.MainWindow(widgetName, new Vector2(), Configuration.getScreenSize(), definedSdrCom);
                        break;

                    case 1:
                        widget = new SoapyRL.View.MainWindow(widgetName, new Vector2(), Configuration.getScreenSize(), definedSdrCom);
                        break;
                }
                var definedwidget = new definedWidget() { widgetType = selectedWidgetType, isComplete = false, device = definedSdrCom, window = widget };
                definedwidget.saveWidget(widgetName);
                WidgetsWindow.Widgets.Add(widgetName, definedwidget);
                selectedSDR = -1;
                _selectedRxSampleRate = -1;
                _selectedTxSampleRate = -1;
                WidgetsWindow.editMode = false;
            }
            Theme.textbuttonTheme = Theme.getTextButtonTheme();
        }

        public static void Render()
        {
            ImGui.SameLine();
            if (Theme.drawTextButton($"{FontAwesome5.Recycle} Refresh Devices"))
                Task.Run(DeviceHelper.refreshDevices);
            ImGui.NewLine();
            renderAddWidget();
        }
    }
}