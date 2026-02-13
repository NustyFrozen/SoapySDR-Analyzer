using NLog;
using SoapySA.Extentions;
using SoapyVNACommon.Extentions;
using System.Drawing;

namespace SoapySA.View.measurements;

public partial class FilterBandwithView
{
    public override string tabName => "Filter Bandwidth";
    private static readonly uint CColorPass = Color.FromArgb(0, 255, 0).ToUint();
    private static readonly uint CColorDeny = Color.Red.ToUint();
    private static readonly uint CColorTransition = Color.Yellow.ToUint();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private bool _calculateSideLobes;
    private bool _calculatingFilterBw;
    private float _filterCenterFreq;
    private float _leftBw;
    private float _leftTransitionWidth;
    private float _rightBw;
    private float _rightTransitionWidth;
    public float Bottom;
    public double DbOffset;
    public double FreqStart;
    public double FreqStop;
    public double GraphEndDb;
    public double GraphStartDb;
    public float GraphLabelIdx;
    public float Left;
    public double RefLevel;
    public float Right;
    public float Top;

    public void UpdateCanvasData(object? sender, KeyOfChangedValueEventArgs e)
    {
        #region Canvas_Data

        try
        {
            DbOffset = (double)Config[Configuration.SaVar.GraphOffsetDb];
            RefLevel = (double)Config[Configuration.SaVar.GraphRefLevel];
            GraphLabelIdx = (int)Config[Configuration.SaVar.ScalePerDivision];

            FreqStart = (double)Config[Configuration.SaVar.FreqStart];
            FreqStop = (double)Config[Configuration.SaVar.FreqStop];

            GraphStartDb = (double)Config[Configuration.SaVar.GraphStartDb] + RefLevel;
            GraphEndDb = (double)Config[Configuration.SaVar.GraphStopDb] + RefLevel;
        }
        catch (Exception ex)
        {
            _logger.Error($"error on updateCanvasData -> {ex.Message}");
        }

        #endregion Canvas_Data
    }

    public Task CalculateMeasurements(SortedDictionary<float, float> span)
    {
        if (_calculatingFilterBw) //another task is doing it, i dont want to fill threadpool
            return Task.CompletedTask;
        _calculatingFilterBw = true;
        try
        {
            int maxIdx = -1, minIdx = -1;
            float maxDb = -9999, minDb = 9999;
            var range = span.ToList();
            foreach (var sample in range)
            {
                if (sample.Value > maxDb && sample.Key >= FreqStart && sample.Key <= FreqStop)
                {
                    if (sample.Key == 0) continue; //some bug
                    maxDb = sample.Value;
                    maxIdx = range.FindIndex(x => x.Key == sample.Key);
                }

                if (sample.Value > maxDb && sample.Key >= span.First().Key && sample.Key <= span.Last().Key)
                    minDb = sample.Value;
            }

            int leftBwIdx = 0, leftLobeStopIdx = 0;
            for (var i = maxIdx; i != -1; i--)
                if (leftBwIdx == 0)
                {
                    if (Math.Abs(maxDb - range[i].Value) >= 5)
                        leftBwIdx = i;
                }
                else if (Math.Abs(range[i].Value - minDb) >= 0.2) //a bit higher of floor level
                {
                    leftLobeStopIdx = i;
                    break;
                }

            int rightBwIdx = range.Count, rightLobeStopIdx = range.Count;
            for (var i = maxIdx; i != range.Count; i++)
                if (rightBwIdx == range.Count)
                {
                    if (Math.Abs(maxDb - range[i].Value) >= 5)
                        rightBwIdx = i;
                }
                else if (Math.Abs(range[i].Value - minDb) >= 0.2)
                {
                    rightLobeStopIdx = i;
                    break;
                }

            _leftTransitionWidth = range[leftBwIdx].Key - range[leftLobeStopIdx].Key;
            _leftTransitionWidth = range[rightLobeStopIdx].Key - range[rightBwIdx].Key;
            _leftBw = range[maxIdx].Key - range[leftBwIdx].Key;
            _rightBw = range[rightBwIdx].Key - range[maxIdx].Key;
            _filterCenterFreq = range[maxIdx].Key;
        }
        catch (Exception e)
        {
            _logger.Trace($"FilterBandwith Measurement Error -> {e.Message}");
        }

        _calculatingFilterBw = false;
        return Task.CompletedTask;
    }
}