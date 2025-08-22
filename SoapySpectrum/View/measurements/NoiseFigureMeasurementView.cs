using System.DirectoryServices.ActiveDirectory;
using ImGuiNET;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using TraceDataStatus = SoapySA.Model.TraceDataStatus;
using TraceViewStatus = SoapySA.Model.TraceViewStatus;

namespace SoapySA.View.measurements;

public class NoiseFigureMeasurementView(MainWindowView initiator)
{
    private string _displayFreq = "100M", _displayENR = "15.0";
    private bool _isCalculating;
    private MainWindowView _parent = initiator;
    private Dictionary<double, double> _enrTable = new();
    private double coldState = 0.0;
    private double _centerFrequency = 100e6;
    private double _selectedENR = 0;
    private void RenderEnrTable()
    {
        Theme.Text("ENR Table");
        Theme.Text("Frequency:");
        Theme.GlowingInput("NF_FreqInput",ref _displayFreq,Theme.InputTheme);
        Theme.Text("ENR");
        Theme.GlowingInput("NF_ENRInput",ref _displayENR,Theme.InputTheme);
        Theme.NewLine();
        if (Theme.Button("Add To Table"))
        {
            try
            {
                if (Global.TryFormatFreq(_displayFreq, out double freq) &&
                    Global.TryFormatFreq(_displayENR, out double ENR))
                    _enrTable[freq] = ENR;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        Theme.Text("Frequency | ENR");
        var keys = _enrTable.Keys.ToArray();
        for (int i = 0; i < _enrTable.Keys.Count; i++)
        {
            if(Theme.DrawTextButton($"{keys[i]}|{_enrTable[keys[i]]}"))
                _enrTable.Remove(keys[i]);
        }
       
    }

    private void RenderCalculationSettings()
    {
        Theme.Text("Frequency to measure NF:");
        if (Theme.GlowingInput("NF_FreqCenterInput", ref _displayFreq, Theme.InputTheme))
            if (Global.TryFormatFreq(_displayFreq, out _centerFrequency))
            {
                _selectedENR = _enrTable[_enrTable.Keys.MinBy(x => Math.Abs(x - _centerFrequency))];
                coldState = 0.0;
                _parent.FrequencyView.ChangeFrequencyBySpan(_centerFrequency, 1e6);
            }

        try
        {
            var refPower = _parent.TraceView.GetClosestSampledFrequency(0, (float)_centerFrequency).Value;
            if (coldState.Equals(0))
            {
            
                Theme.Text($"Cold State: {refPower}");
                if (Theme.Button("Set Cold Reference"))
                    coldState = refPower;
            }
            else
            {
                Theme.Text($"Hot State: {refPower}");
                var yFactor =  Math.Abs(Math.Abs(coldState) - Math.Abs(refPower)) ;
                var nf = 10.0 * Math.Log10(Math.Pow(10, _selectedENR / 10) / Math.Pow(10, (yFactor / 10) - 1));
                Theme.Text($"Y Factor: {yFactor}");
                Theme.Text($"Noise Figure: {nf}");
            }
            Theme.NewLine();
            if(Theme.Button("Reset Averaging"))
                resetTrace();
        }
        catch (Exception e)
        {
            //user did not select frequency
        }
        
    }

    void resetTrace()
    {
        lock (_parent.TraceView.STraces)
        {
            _parent.TraceView.STraces[0].ViewStatus = TraceViewStatus.Active;
            _parent.TraceView.STraces[0].Average = 1;
            _parent.TraceView.STraces[0].DataStatus = TraceDataStatus.Average;
            _parent.TraceView.STraces[0].Plot.Clear();
        }
    }
    public void RenderNoiseFigureSettings()
    {
        Theme.Text("Noise Figure (Y method)");
        if (_enrTable.Count > 0 && !_isCalculating) //at least one ENR reference...
            if (Theme.Button("Begin NF Calculation"))
            {
                _parent.TraceView.DisableAllTraces();
                resetTrace();
                _isCalculating = true;
            }
        Theme.NewLine();
        if (!_isCalculating)
            RenderEnrTable();
        else
            RenderCalculationSettings();
    }
}