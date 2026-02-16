using ImGuiNET;
using SoapySA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using TraceDataStatus = SoapySA.Model.TraceDataStatus;
using TraceViewStatus = SoapySA.Model.TraceViewStatus;

namespace SoapySA.View.measurements;

public class NoiseFigureMeasurementView(Configuration _config, GraphPlotManager Graph) : MeasurementFeature
{
    private string _displayFreq = "100M", _displayENR = "15.0";
    private bool _isCalculating;
    public override string Name => $"{FontAwesome5.TemperatureEmpty} Noise Figure (Y method) Measurement";

    private readonly Dictionary<double, double> _enrTable = new();

    private double coldState = 0.0;
    private double _centerFrequency = 100e6;
    private double _selectedENR = 0;
    private void RenderEnrTable()
    {
        Theme.Text("ENR Table");
        Theme.Text("Frequency:");
        Theme.GlowingInput("NF_FreqInput", ref _displayFreq, Theme.InputTheme);
        Theme.Text("ENR");
        Theme.GlowingInput("NF_ENRInput", ref _displayENR, Theme.InputTheme);
        Theme.NewLine();

        if (Theme.Button("Add To Table"))
        {
            try
            {
                if (Global.TryFormatFreq(_displayFreq, out double freq) &&
                    double.TryParse(_displayENR, out double enr))
                {
                    _enrTable[freq] = enr;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        Theme.Text("Frequency | ENR");
        var keys = _enrTable.Keys.ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            if (Theme.DrawTextButton($"{keys[i]}|{_enrTable[keys[i]]}"))
                _enrTable.Remove(keys[i]);
        }
    }

    private void RenderCalculationSettings()
    {
        Theme.Text("Frequency to measure NF:");
        if (Theme.GlowingInput("NF_FreqCenterInput", ref _displayFreq, Theme.InputTheme))
        {
            if (Global.TryFormatFreq(_displayFreq, out _centerFrequency))
            {
                // pick closest ENR reference
                _selectedENR = _enrTable[_enrTable.Keys.MinBy(x => Math.Abs(x - _centerFrequency))];

                coldState = 0.0;

                // Set span via strongly-typed config (DI)
                const double span = 1e6;
                _config.FreqStart = _centerFrequency - span / 2.0;
                _config.FreqStop = _centerFrequency + span / 2.0;
            }
        }

        try
        {
            var refPower = Graph.STraces[0].GetClosestSampledFrequency((float)_centerFrequency).Value;

            if (coldState.Equals(0))
            {
                Theme.Text($"Cold State: {refPower}");
                if (Theme.Button("Set Cold Reference"))
                    coldState = refPower;
            }
            else
            {
                Theme.Text($"Hot State: {refPower}");

                var yFactor = Math.Abs(Math.Abs(coldState) - Math.Abs(refPower));

                // Keeping your existing math as-is
                var nf = 10.0 * Math.Log10(Math.Pow(10, _selectedENR / 10) / Math.Pow(10, (yFactor / 10) - 1));

                Theme.Text($"Y Factor: {yFactor}");
                Theme.Text($"Noise Figure: {nf}");
            }

            Theme.NewLine();
            if (Theme.Button("Reset Averaging"))
                resetTrace();
        }
        catch (Exception)
        {
            // user did not select frequency / trace not ready
        }
    }

    void resetTrace()
    {
        lock (Graph.STraces)
        {
            Graph.STraces[0].ViewStatus = TraceViewStatus.Active;
            Graph.STraces[0].Average = 1;
            Graph.STraces[0].DataStatus = TraceDataStatus.Average;
            Graph.STraces[0].Plot.Clear();
        }
    }
    public override bool renderGraph() => false;
    public override bool renderSettings()
    {
        Theme.Text("Noise Figure (Y method)");

        if (_enrTable.Count > 0 && !_isCalculating) // at least one ENR reference...
        {
            if (Theme.Button("Begin NF Calculation"))
            {
                for (int i = 0; i < Graph.STraces.Length; i++)
                    Graph.STraces[i].ViewStatus = TraceViewStatus.Clear;
                resetTrace();
                _isCalculating = true;
            }
        }

        Theme.NewLine();

        if (!_isCalculating)
            RenderEnrTable();
        else
            RenderCalculationSettings();
        return true;
    }
}