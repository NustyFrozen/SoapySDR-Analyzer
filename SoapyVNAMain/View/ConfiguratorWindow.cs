using ImGuiNET;
using Pothosware.SoapySDR;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapyVNAMain.View
{
    public struct definedWidget
    {
        public bool isComplete;
        public Widget window;
        public sdrDeviceCOM device;
    }

    public class ConfiguratorWindow
    {
        private static string widgetName = "Widget1";
        private static int selectedWidgetType = 0, selectedSDR = -1;
        private static string[] widgetType = new string[] { "Spectrum Analyzer", "Return Loss" };
        public static string s_customSampleRate = "0";
        private static int _selectedSampleRate;

        private static Dictionary<uint, StringList> availableRxAnntenna = new Dictionary<uint, StringList>();
        private static Dictionary<uint, StringList> availableTxAnntenna = new Dictionary<uint, StringList>();
        private static int _selectedRxChannel = 0, _selectedRxAnntenna = -1;
        private static int _selectedTxChannel = 0, _selectedTxAnntenna = -1;

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
                //i is the channel, j is the anntenna
                for (int j = 0; j < selectedDevice.availableRxAntennas[(uint)i].Count; j++)
                    //is there a widget that is using channel i and anntenna j?
                    if (WidgetsWindow.Widgets.Any(x =>
                    x.Value.device.rxAntenna.Item1 == i
                    &&
                    x.Value.device.rxAntenna.Item2 == selectedDevice.availableRxAntennas[(uint)i][j]))
                        continue;
                    else
                        availableRxAnntenna[(uint)i].Add(selectedDevice.availableRxAntennas[(uint)i][j]);
            }

            for (int i = 0; i < selectedDevice.availableTxChannels; i++)
            {
                if (!availableTxAnntenna.ContainsKey((uint)i))
                    availableTxAnntenna.Add((uint)i, new StringList());
                //i is the channel, j is the anntenna
                for (int j = 0; j < selectedDevice.availableTxAntennas[(uint)i].Count; j++)
                    //is there a widget that is using channel i and anntenna j?
                    if (WidgetsWindow.Widgets.Any(x =>
                    x.Value.device.txAntenna.Item1 == i
                    &&
                    x.Value.device.txAntenna.Item2 == selectedDevice.availableTxAntennas[(uint)i][j]))
                        continue;
                    else
                        availableTxAnntenna[(uint)i].Add(selectedDevice.availableTxAntennas[(uint)i][j]);
            }
        }

        public static void renderAddWidget()
        {
            if (selectedSDR > DeviceHelper.AvailableDevices.Length || DeviceHelper.availableDevicesCOM == null)
            {
                Theme.Text($"No SDR found, Cannot Create a widget");
                return;
            }
            Theme.Text($"Create A widget");
            Theme.newLine();
            Theme.Text($"Select An SDR:");
            for (int i = 0; i < DeviceHelper.AvailableDevices.Length; i++)
            {
                var devKwargs = DeviceHelper.AvailableDevices[i];
                if (selectedSDR == i)
                    Theme.textbuttonTheme.bgcolor = Color.Green.ToUint();
                if (Theme.drawTextButton($"{FontAwesome5.Microchip} {devKwargs}"))
                {
                    selectedSDR = i;
                    fetchAvailableAnntennas();
                }
                Theme.textbuttonTheme = Theme.getTextButtonTheme();
                Theme.newLine();
            }
            Theme.Text("Widget Type");
            Theme.glowingCombo("chooseWidgetType", ref selectedWidgetType, widgetType, Theme.inputTheme);
            Theme.newLine();
            Theme.Text($"{FontAwesome5.Pencil} Widget Name");
            Theme.glowingInput($"Widget Name", ref widgetName, Theme.inputTheme);
            bool isvalid = selectedSDR != -1;
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
                        isvalid = _selectedRxAnntenna != -1;
                        break;

                    case 1: //Return Loss
                        Theme.newLine();
                        Theme.Text("select Reflection Channel");
                        Theme.glowingCombo("select Reflection Channel", ref _selectedRxChannel,
                            Array.ConvertAll(Enumerable.Range(0, (int)DeviceHelper.availableDevicesCOM[selectedSDR].availableRxChannels).ToArray(), Convert.ToString)
                            , Theme.inputTheme);
                        Theme.newLine();
                        Theme.Text("select Reflection Anntenna");
                        Theme.glowingCombo("select Reflection Anntenna", ref _selectedRxAnntenna,
                                                availableRxAnntenna[(uint)_selectedRxChannel].ToArray()
                                                , Theme.inputTheme);
                        Theme.newLine();
                        Theme.Text("select Forward/transmitting Channel");
                        Theme.glowingCombo("select Reflection Channel", ref _selectedTxChannel,
                            Array.ConvertAll(Enumerable.Range(0, (int)DeviceHelper.availableDevicesCOM[selectedSDR].availableTxChannels).ToArray(), Convert.ToString)
                            , Theme.inputTheme);
                        Theme.newLine();
                        Theme.Text("select Forward/transmitting Anntenna");
                        Theme.glowingCombo("select Reflection Anntenna", ref _selectedTxAnntenna,
                                                availableTxAnntenna[(uint)_selectedTxChannel].ToArray()
                                                , Theme.inputTheme);
                        isvalid = _selectedTxAnntenna != -1 && _selectedRxAnntenna != -1;
                        break;
                }
            }
            catch (Exception ex)
            {
            }
            Theme.newLine();
            var text = isvalid ? $"{FontAwesome5.Check} Add Widget" : $"{FontAwesome5.Cross} Please Select Antenna, Sample Rate and Channel";
            Theme.textbuttonTheme.bgcolor = isvalid ? Color.Green.ToUint() : Color.Red.ToUint();
            if (Theme.drawTextButton($"{text}"))
            {
                if (isvalid) return;
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