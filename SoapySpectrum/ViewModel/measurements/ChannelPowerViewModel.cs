using NLog;
using SoapySA.Extentions;
using SoapySA.Model;
using SoapyVNACommon.Extentions;
using System.ComponentModel;

namespace SoapySA.View.measurements;

public partial class ChannelPowerView
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // UI fields
    private string _sDisplayBw = "5M";
    private string _sDisplayObw = "99%";

    // Calculated measurements
    private double _calculatedbandPower;
    private double _calculatedbandAveragePower;
    private double _calculatedoccupiedBw;

    // Cached config-derived values for rendering
    private double _center;
    private double _span;
    private double _channelBandwith = 1e6;
    private double _occupiedBwPrecentile = 0.99;
    private double _dbOffset;
    private double _refLevel;
    private double _graphStartDb;
    private double _graphEndDb;
    private double _fftRbw;

    // Graph state
    private float _graphLabelIdx;
    private float _left;
    private float _right;
    private float _top;
    private float _bottom;

    private bool _calculatingBandPower;

    private void HookConfig()
    {
        _config.PropertyChanged -= OnConfigPropertyChanged;
        _config.PropertyChanged += OnConfigPropertyChanged;
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only update cached render values when relevant fields change
        if (e.PropertyName is nameof(Configuration.FreqStart)
            or nameof(Configuration.FreqStop)
            or nameof(Configuration.GraphRefLevel)
            or nameof(Configuration.GraphStartDb)
            or nameof(Configuration.GraphStopDb)
            or nameof(Configuration.GraphOffsetDb)
            or nameof(Configuration.ScalePerDivision)
            or nameof(Configuration.FftRbw)
            or nameof(Configuration.ChannelOcp)
            or nameof(Configuration.ChannelBw))
        {
            UpdateFromConfig();
        }
    }

    private void UpdateFromConfig()
    {
        try
        {
            _span = _config.FreqStop - _config.FreqStart;
            _center = _config.FreqStart + _span / 2.0;

            _refLevel = _config.GraphRefLevel;
            _graphStartDb = _config.GraphStartDb + _refLevel;
            _graphEndDb = _config.GraphStopDb + _refLevel;

            _dbOffset = _config.GraphOffsetDb;
            _graphLabelIdx = _config.ScalePerDivision;

            _fftRbw = _config.FftRbw;
            _occupiedBwPrecentile = _config.ChannelOcp;
            _channelBandwith = _config.ChannelBw;

            // keep settings UI text aligned too
            _sDisplayBw = _channelBandwith.ToString();
            _sDisplayObw = (_occupiedBwPrecentile * 100.0).ToString() + "%";
        }
        catch (Exception ex)
        {
            Logger.Error($"error on UpdateFromConfig -> {ex.Message}");
        }
    }

    public Task CalculateMeasurements(float[] data)
    {
        if (_calculatingBandPower)
            return Task.CompletedTask;

        _calculatingBandPower = true;

        try
        {
            var dBSpan = data.AsSpan();
            double tempbandPower = 0;
            double tempbandPower2 = ((double)data[data.Length / 2]).ToMw();
            double occupiationLength = 0;

            // sum power
            tempbandPower = data.Select(x => ((double)x).ToMw()).Sum();

            // occupied bandwidth percentile
            for (var i = 1; i < data.Length / 2; i++)
            {
                if (tempbandPower2 / tempbandPower >= _occupiedBwPrecentile)
                    break;

                if (data.Length / 2 - i < 0 || data.Length / 2 + i > data.Length - 1)
                {
                    occupiationLength = data.Length / 2;
                    break;
                }

                tempbandPower2 += ((double)data[data.Length / 2 - i]).ToMw()
                                + ((double)data[data.Length / 2 + i]).ToMw();
                occupiationLength = i;
            }

            _calculatedoccupiedBw = occupiationLength * 2.0 / data.Length * _channelBandwith;

            if (tempbandPower != 0)
            {
                _calculatedbandPower = tempbandPower.ToDBm();
                _calculatedbandAveragePower = (tempbandPower / dBSpan.Length).ToDBm();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"ChannelPower Error measurement {ex.Message}");
        }

        _calculatingBandPower = false;
        return Task.CompletedTask;
    }
}