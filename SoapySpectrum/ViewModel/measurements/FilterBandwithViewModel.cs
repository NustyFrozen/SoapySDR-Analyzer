using NLog;
using SoapySA.Extentions;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using System.ComponentModel;
using System.Drawing;

namespace SoapySA.View.measurements;

public partial class FilterBandwithView
{
    public override string Name => $"{FontAwesome5.Filter} Filter Bandwidth";
    private static readonly uint CColorPass = Color.FromArgb(0, 255, 0).ToUint();
    private static readonly uint CColorDeny = Color.Red.ToUint();
    private static readonly uint CColorTransition = Color.Yellow.ToUint();
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly Configuration _config;
    private readonly GraphPlotManager _graphData;
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

    private void ConfigOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only update cache when relevant fields change
        if (e.PropertyName is nameof(Configuration.GraphOffsetDb)
            or nameof(Configuration.GraphRefLevel)
            or nameof(Configuration.ScalePerDivision)
            or nameof(Configuration.FreqStart)
            or nameof(Configuration.FreqStop)
            or nameof(Configuration.GraphStartDb)
            or nameof(Configuration.GraphStopDb))
        {
            UpdateCanvasDataFromConfig();
        }
    }

    private void UpdateCanvasDataFromConfig()
    {
        try
        {
            DbOffset = _config.GraphOffsetDb;
            RefLevel = _config.GraphRefLevel;
            GraphLabelIdx = (float)_config.ScalePerDivision;

            FreqStart = _config.FreqStart;
            FreqStop = _config.FreqStop;

            GraphStartDb = _config.GraphStartDb + RefLevel;
            GraphEndDb = _config.GraphStopDb + RefLevel;
        }
        catch (Exception ex)
        {
            _logger.Error($"error on updateCanvasData -> {ex.Message}");
        }
    }

    public Task CalculateMeasurements(SortedDictionary<float, float> span)
    {
        if (_calculatingFilterBw || span.Count == 0)
            return Task.CompletedTask;

        _calculatingFilterBw = true;
        try
        {
            int maxIdx = -1;
            float maxDb = -9999, minDb = 9999;
            var range = span.ToList();

            foreach (var sample in range)
            {
                if (sample.Value > maxDb && sample.Key >= FreqStart && sample.Key <= FreqStop)
                {
                    if (sample.Key == 0) continue;
                    maxDb = sample.Value;
                    maxIdx = range.FindIndex(x => x.Key == sample.Key);
                }

                if (sample.Value < minDb) // Corrected logic for actual floor
                    minDb = sample.Value;
            }

            if (maxIdx == -1) return Task.CompletedTask;

            int leftBwIdx = 0, leftLobeStopIdx = 0;
            for (var i = maxIdx; i >= 0; i--)
            {
                if (leftBwIdx == 0)
                {
                    if (Math.Abs(maxDb - range[i].Value) >= 3) // Standard 3dB or 5dB point
                        leftBwIdx = i;
                }
                else if (Math.Abs(range[i].Value - minDb) <= 0.2)
                {
                    leftLobeStopIdx = i;
                    break;
                }
            }

            int rightBwIdx = range.Count - 1, rightLobeStopIdx = range.Count - 1;
            for (var i = maxIdx; i < range.Count; i++)
            {
                if (rightBwIdx == range.Count - 1)
                {
                    if (Math.Abs(maxDb - range[i].Value) >= 3)
                        rightBwIdx = i;
                }
                else if (Math.Abs(range[i].Value - minDb) <= 0.2)
                {
                    rightLobeStopIdx = i;
                    break;
                }
            }

            _leftTransitionWidth = range[leftBwIdx].Key - range[leftLobeStopIdx].Key;
            _rightTransitionWidth = range[rightLobeStopIdx].Key - range[rightBwIdx].Key;
            _leftBw = range[maxIdx].Key - range[leftBwIdx].Key;
            _rightBw = range[rightBwIdx].Key - range[maxIdx].Key;
            _filterCenterFreq = range[maxIdx].Key;
        }
        catch (Exception e)
        {
            _logger.Trace($"FilterBandwith Measurement Error -> {e.Message}");
        }
        finally
        {
            _calculatingFilterBw = false;
        }

        return Task.CompletedTask;
    }
}