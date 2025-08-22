using NLog;
using SoapySA.Extentions;
using SoapyVNACommon.Extentions;

namespace SoapySA.View.measurements;

public partial class ChannelPowerView
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static string _sDisplayBw = "5M";
    private static string _sDisplayObw = "99%";
    private static double _calculatedbandPower;
    private static double _calculatedbandAveragePower;
    private static double _calculatedoccupiedBw;
    private static double _center;
    private static double _span;
    private static double _channelBandwith = 1e6;
    private static double _occupiedBwPrecentile = 0.99;
    private static double _dbOffset;
    private static double _refLevel;
    private static double _graphStartDb;
    private static double _graphEndDb;
    private static double _fftRbw;
    private static float _graphLabelIdx;
    private static float _left;
    private static float _right;
    private static float _top;
    private static float _bottom;
    private static bool _calculatingBandPower;
    private readonly MainWindowView _parent = initiator;

    public static Task CalculateMeasurements(float[] data)
    {
        if (_calculatingBandPower) //another task is doing it, i dont want to fill threadpool
            return Task.CompletedTask;
        _calculatingBandPower = true;

        try
        {
            var dBSpan = data.AsSpan();
            double tempbandPower = 0, tempbandPower2 = ((double)data[data.Length / 2]).ToMw(), occupiationLength = 0;
            //calculating Sum
            tempbandPower = data.Select(x => ((double)x).ToMw()).Sum();

            //calculating how much data for occupiedBW
            for (var i = 1; i < data.Length / 2; i++)
            {
                //satisfies the occupiedBW Precentile
                if (tempbandPower2 / tempbandPower >= _occupiedBwPrecentile)
                    break;

                //out of range can be a case where length is not even and therfore its just a sum of the right or left
                if (data.Length / 2 - i < 0 || data.Length / 2 + i > data.Length - 1)
                {
                    occupiationLength = data.Length / 2;
                    break;
                }

                //add to the sum from both side lobes
                tempbandPower2 += ((double)data[data.Length / 2 - i]).ToMw() +
                                  ((double)data[data.Length / 2 + i]).ToMw();
                occupiationLength = i;
            }

            _calculatedoccupiedBw = occupiationLength * 2.0 / data.Length * _channelBandwith;

            if (tempbandPower != 0) //not enough values in dbArray --> log(0) --> overflow -inf
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

    public void UpdateCanvasData(object? sender, KeyOfChangedValueEventArgs e)
    {
        #region Canvas_Data

        try
        {
            _span = (double)_parent.Configuration.Config[Configuration.SaVar.FreqStop] -
                    (double)_parent.Configuration.Config[Configuration.SaVar.FreqStart];
            _center = (double)_parent.Configuration.Config[Configuration.SaVar.FreqStart] + _span / 2;
            _refLevel = (double)_parent.Configuration.Config[Configuration.SaVar.GraphRefLevel];
            _graphStartDb = (double)_parent.Configuration.Config[Configuration.SaVar.GraphStartDb] + _refLevel;
            _graphEndDb = (double)_parent.Configuration.Config[Configuration.SaVar.GraphStopDb] + _refLevel;
            _dbOffset = (double)_parent.Configuration.Config[Configuration.SaVar.GraphOffsetDb];
            _graphLabelIdx = _parent.AmplitudeView.SScalePerDivision;
            _fftRbw = (double)_parent.Configuration.Config[Configuration.SaVar.FftRbw];
            _occupiedBwPrecentile = (double)_parent.Configuration.Config[Configuration.SaVar.ChannelOcp];
            _channelBandwith = (double)_parent.Configuration.Config[Configuration.SaVar.ChannelBw];
        }
        catch (Exception ex)
        {
            Logger.Error($"error on updateCanvasData -> {ex.Message}");
        }

        #endregion Canvas_Data
    }
}