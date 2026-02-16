using ImGuiNET;
using NLog;
using Pothosware.SoapySDR;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoapySA.View.measurements
{
    public class SourceView : MeasurementFeature
    {
        private readonly NLog.Logger _logger = LogManager.GetCurrentClassLogger();
        static readonly string[] availableSourceModes = new string[] { "Disabled","Tracking", "CW"};
        Configuration config;
        SdrDeviceCom com;
        string[] _gainValues;
        public string transmissionFreq = string.Empty;
        public int selectedSourceMode = 0;// 0 = disabled,1 = Track, 2 = CW
        public SourceView(Configuration config, SdrDeviceCom com)
        {
            this.config = config;
            this.com = com;
            _gainValues = new string[com.TxGainValues.Count];
            config.OnConfigLoadBegin += (object? s, EventArgs e) =>
            {
                transmissionFreq = config.SourceFreq.ToString();
                selectedSourceMode = config.SourceMode;
            };
        }
        
        public override string Name => $"{FontAwesome5.TowerBroadcast} Source";
        public override bool renderSettings()
        {
            for (var i = 0; i < _gainValues.Length; i++) _gainValues[i] = com.TxGainValues[i].ToString();
            if (com.TxAntenna is null)
            {
                Theme.Text("This Widget was defined with no Tx Source!");
                return true;
            }
            Theme.Text("Tx Source");
            Theme.Text("Source Mode");
            if (Theme.GlowingCombo("TxSourceSelected",
               ref selectedSourceMode, availableSourceModes,
               Theme.InputTheme))
            {
                config.SourceMode = selectedSourceMode;
            }
            if(selectedSourceMode == 2)
            {
                Theme.Text("Frequency");
                if (Theme.GlowingInput("SourceFrequency", ref transmissionFreq,
                Theme.InputTheme)) //frequencyChangedByCenterSpan
                {
                    double freq = 0;
                    if (Global.TryFormatFreq(transmissionFreq, out freq))
                    {
                        config.SourceFreq = freq;
                        com.SdrDevice.SetFrequency(Direction.Tx,
                          com.TxAntenna.Item1, (double)freq);
                    }
                }
            }
            Theme.Text("Transmission Power");
            foreach (var gainElm in com.TxGains)
                if (gainElm.Key.Item1 == com.TxAntenna.Item1)
                {
                var gain = _gainValues[gainElm.Value.Item2];
                var range = gainElm.Value.Item1;
                ImGui.Text($"{gainElm.Key.Item2} {range.Minimum} - {range.Maximum}");
                if (Theme.GlowingInput($"{gainElm.Key.Item2}", ref _gainValues[gainElm.Value.Item2],
                        Theme.InputTheme))
                {
                    double results = 0;
                    var valid = double.TryParse(_gainValues[gainElm.Value.Item2], out results);
                    valid |= results >= range.Minimum && results <= range.Maximum;
                    if (!valid)
                    {
                        _logger.Error("invalid Double Value or value ot ouf range");
                    }
                    else
                    {
                        if (range.Step != 0)
                            com.SdrDevice.SetGain(Direction.Tx, com.TxAntenna.Item1, gainElm.Key.Item2,
                                Math.Round(results / range.Step) * range.Step);
                        else
                            //free value
                            com.SdrDevice.SetGain(Direction.Tx, com.TxAntenna.Item1, gainElm.Key.Item2,
                                results);
                    }
                }
            }
            return true;
        }
        public override bool renderGraph()
        {
            return false;//Source view has no graph data to render,Expecting Default Mode
        }
    }
}
