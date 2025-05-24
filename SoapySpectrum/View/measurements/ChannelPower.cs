using ImGuiNET;
using NLog;
using SoapySA.Extentions;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using System.Numerics;

namespace SoapySA.View.measurements
{
    public class ChannelPower(MainWindow initiator)
    {
        private MainWindow parent = initiator;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static string s_displayBW = "5M", s_displayOBW = "99%";

        private static double _calculatedbandPower = 0, _calculatedbandAveragePower = 0, _calculatedoccupiedBW = 0,
            _center, _span,
            _channelBandwith = 1e6, _occupiedBWPrecentile = 0.99,
            dbOffset, refLevel, graph_startDB, graph_endDB, fftRBW;

        private static float graphLabelIdx, left, right, top, bottom;
        private static bool _calculatingBandPower;

        public void updateCanvasData(object? sender, keyOfChangedValueEventArgs e)
        {
            #region Canvas_Data

            try
            {
                _span = (double)parent.Configuration.config[Configuration.saVar.freqStop] - (double)parent.Configuration.config[Configuration.saVar.freqStart];
                _center = (double)parent.Configuration.config[Configuration.saVar.freqStart] + _span / 2;
                refLevel = (double)parent.Configuration.config[Configuration.saVar.graphRefLevel];
                graph_startDB = (double)parent.Configuration.config[Configuration.saVar.graphStartDB] + refLevel;
                graph_endDB = (double)parent.Configuration.config[Configuration.saVar.graphStopDB] + refLevel;
                dbOffset = (double)parent.Configuration.config[Configuration.saVar.graphOffsetDB];
                graphLabelIdx = (float)parent.tab_Amplitude.s_scalePerDivision;
                fftRBW = (double)parent.Configuration.config[Configuration.saVar.fftRBW];
                _occupiedBWPrecentile = (double)parent.Configuration.config[Configuration.saVar.channelOCP];
                _channelBandwith = (double)parent.Configuration.config[Configuration.saVar.channelBW];
            }
            catch (Exception ex)
            {
                _logger.Error($"error on updateCanvasData -> {ex.Message}");
            }

            #endregion Canvas_Data
        }

        public void renderChannelPowerSettings()
        {
            Theme.Text("Channel BW");
            if (Theme.glowingInput("channelPowerBandwith", ref s_displayBW, Theme.inputTheme)) //frequencyChangedByCenterSpan
            {
                double bw = 0;
                if (Global.TryFormatFreq(s_displayBW, out bw))
                {
                    parent.Configuration.config[Configuration.saVar.channelBW] = bw;
                }
            }
            Theme.newLine();
            Theme.Text("Channel Occupied Bandwidth (0-99%)");
            if (Theme.glowingInput("channelPowerOCP", ref s_displayOBW, Theme.inputTheme)) //frequencyChangedByCenterSpan
            {
                double ocp = 0;
                if (double.TryParse(s_displayOBW.Replace("%", ""), out ocp))
                {
                    if (ocp < 100 && ocp > 0)
                        parent.Configuration.config[Configuration.saVar.channelOCP] = ocp / 100.0;
                }
            }
        }

        public static Task calculateMeasurements(float[] data)
        {
            if (_calculatingBandPower) //another task is doing it, i dont want to fill threadpool
                return Task.CompletedTask;
            _calculatingBandPower = true;

            try
            {
                var dBSpan = data.AsSpan();
                double tempbandPower = 0, tempbandPower2 = ((double)data[data.Length / 2]).toMW(), occupiationLength = 0;
                //calculating Sum
                tempbandPower = data.Select(x => ((double)x).toMW()).Sum();

                //calculating how much data for occupiedBW
                for (int i = 1; i < data.Length / 2; i++)
                {
                    //satisfies the occupiedBW Precentile
                    if (tempbandPower2 / tempbandPower >= _occupiedBWPrecentile)
                        break;

                    //out of range can be a case where length is not even and therfore its just a sum of the right or left
                    if (data.Length / 2 - i < 0 || data.Length / 2 + i > data.Length - 1)
                    {
                        occupiationLength = data.Length / 2;
                        break;
                    }

                    //add to the sum from both side lobes
                    tempbandPower2 += ((double)data[(data.Length / 2) - i]).toMW() + ((double)data[(data.Length / 2) + i]).toMW();
                    occupiationLength = i;
                }
                _calculatedoccupiedBW = (occupiationLength * 2.0 / ((double)data.Length)) * _channelBandwith;

                if (tempbandPower != 0) //not enough values in dbArray --> log(0) --> overflow -inf
                {
                    _calculatedbandPower = tempbandPower.toDBm();
                    _calculatedbandAveragePower = (tempbandPower / dBSpan.Length).toDBm();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"ChannelPower Error measurement {ex.Message}");
            }
            _calculatingBandPower = false;
            return Task.CompletedTask;
        }

        public void renderChannelPower()
        {
            #region Canvas_Data

            var windowPos = ImGui.GetWindowPos();
            var draw = ImGui.GetForegroundDrawList();
            var graphStatus = new Vector2();
            left = windowPos.X + parent.Configuration.positionOffset.X;
            right = left + parent.Configuration.graphSize.X;
            top = windowPos.Y + parent.Configuration.positionOffset.Y;
            bottom = top + parent.Configuration.graphSize.Y * .75f - parent.Configuration.positionOffset.Y;
            var text = string.Empty;
            var textSize = new Vector2();
            var textPos = new Vector2();
            uint traceColor_uint = 0x7FFFFF00;
            int startBandPower = 0, endBandPower = 0;
            float peakValue = -99999, minValue = 99999;
            Span<KeyValuePair<float, float>> plotData = new Span<KeyValuePair<float, float>>();

            #endregion Canvas_Data

            #region graphDraw

            try
            {
                draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(16, 16, 16).ToUint());
                for (float i = 0; i < graphLabelIdx; i++)
                {
                    //draw Y axis
                    text = Imports.Scale(i, 0, graphLabelIdx, graph_endDB + dbOffset, graph_startDB + dbOffset).ToString()
                        .TruncateLongString(5);
                    textSize = ImGui.CalcTextSize(text);
                    var posY = top + i / graphLabelIdx * parent.Configuration.graphSize.Y * .75f;
                    draw.AddText(new Vector2(left - textSize.X, posY - textSize.Y / 2),
                        Color.LightGray.ToUint(), text);
                    draw.AddLine(new Vector2(left, posY), new Vector2(right, posY), Color.FromArgb(100, Color.Gray).ToUint());
                }

                var plot = parent.tab_Trace.s_traces[0].plot;

                plotData = plot.ToArray().AsSpan(); //asspan is fastest iteration
                                                    //x = left, y = right
                var channelBandwithFreq = new Vector2((float)(_center - _channelBandwith / 2.0f), (float)(_center + _channelBandwith / 2.0f));

                for (var i = 1; i < plotData.Length; i++)
                {
                    var sampleA = plotData[i - 1];
                    var sampleB = plotData[i];

                    var sampleAPos = parent.Graph.scaleToGraph(left, top, right, bottom, sampleA.Key, sampleA.Value, _center - _span / 2,
                        _center + _span / 2, graph_startDB, graph_endDB);
                    var sampleBPos = parent.Graph.scaleToGraph(left, top, right, bottom, sampleB.Key, sampleB.Value, _center - _span / 2,
                        _center + _span / 2, graph_startDB, graph_endDB);
                    //draw two lines of bandwith on the graph
                    if (sampleA.Key <= channelBandwithFreq.X && sampleB.Key >= channelBandwithFreq.X)
                    {
                        startBandPower = i;
                        draw.AddLine(new Vector2(sampleAPos.X, top), new Vector2(sampleAPos.X, bottom), 0xFF726E6D);
                        traceColor_uint = 0xFFFFFF00;
                    }
                    else if (sampleA.Key <= channelBandwithFreq.Y && sampleB.Key >= channelBandwithFreq.Y)
                    {
                        endBandPower = i;
                        draw.AddLine(new Vector2(sampleAPos.X, top), new Vector2(sampleAPos.X, bottom), 0xFF726E6D);
                        traceColor_uint = 0x7FFFFF00;
                    }
                    if (startBandPower == i || endBandPower == i)
                    {
                        if (sampleA.Value > peakValue)
                            peakValue = sampleA.Value;
                        if (sampleA.Value < minValue)
                            minValue = sampleA.Value;
                    }
                    //bounds check
                    if (sampleBPos.X > right || sampleAPos.X < left) continue;
                    if (sampleAPos.Y < top || sampleBPos.Y < top || sampleAPos.Y > bottom || sampleBPos.Y > bottom)
                    {
                        if (!(bool)parent.Configuration.config[Configuration.saVar.automaticLevel]) continue;
                        if (sampleAPos.Y < top || sampleBPos.Y < top)
                            parent.Configuration.config[Configuration.saVar.graphStartDB] =
                                (double)Math.Min(sampleA.Value, sampleB.Value);
                        else
                            parent.Configuration.config[Configuration.saVar.graphStopDB] =
                                (double)Math.Max(sampleA.Value, sampleB.Value);
                    }
                    draw.AddLine(sampleAPos, sampleBPos, traceColor_uint, 1.0f);
                }
                if (!_calculatingBandPower)
                    calculateMeasurements(plotData.Slice(startBandPower, endBandPower - startBandPower).ToArray().Select(x => x.Value).ToArray());
            }
            catch (Exception ex)
            {
                _calculatingBandPower = false;
                _logger.Error($"Channe Power trace Render error -> {ex.Message}");
            }
            //draw OccupiedBW
            var occupiedStart = parent.Graph.scaleToGraph(left, top, right, bottom, (float)(_center - (_calculatedoccupiedBW / 2.0f)), peakValue, _center - _span / 2,
                        _center + _span / 2, graph_startDB, graph_endDB);
            var occupiedStop = parent.Graph.scaleToGraph(left, top, right, bottom, (float)(_center + (_calculatedoccupiedBW / 2.0f)), peakValue, _center - _span / 2,
                    _center + _span / 2, graph_startDB, graph_endDB);
            draw.AddLine(occupiedStart, occupiedStop, 0XFF00FF00);
            draw.AddLine(new Vector2(occupiedStart.X, top), new Vector2(occupiedStart.X, bottom), 0XFF00FF00);
            draw.AddLine(new Vector2(occupiedStop.X, top), new Vector2(occupiedStop.X, bottom), 0XFF00FF00);
            text = $"OBW {_calculatedoccupiedBW}hz";
            textSize = ImGui.CalcTextSize(text);
            draw.AddText(occupiedStart + new Vector2(0, -2 - textSize.Y),
                0XFF00FF00,
                text);

            #endregion graphDraw

            text = $"Center {_center} Span {_span} Channel BW {_channelBandwith} RBW {fftRBW}";
            textSize = ImGui.CalcTextSize(text);
            draw.AddText(new Vector2(left, bottom - textSize.Y), 0xFFFFFFFF, text);
            top = bottom;
            bottom += parent.Configuration.graphSize.Y * .25f;
            draw.AddRectFilled(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(16, 16, 16).ToUint());
            draw.AddRect(new Vector2(left, top), new Vector2(right, bottom), Color.FromArgb(91, 36, 221).ToUint());
            var shrink = (right - left) * .25f;
            left += shrink;
            right -= shrink;

            text = $"Channel Power: {_calculatedbandPower.ToString().TruncateLongString(5)} dB\n" +
                $"Channel Flatness {(peakValue - minValue).ToString().TruncateLongString(5)} Pk->Pk\n" +
                $"Spectral Density: {(_calculatedbandPower - (_channelBandwith).toDBm()).ToString().TruncateLongString(5)} dBm/hz\n" +
                $"PAPR: {(peakValue - _calculatedbandAveragePower).ToString().TruncateLongString(5)} dBm\n" +
                $"{(_occupiedBWPrecentile * 100).ToString().TruncateLongString(5)}% Occupied Bandwith {(_calculatedoccupiedBW / 1e6).ToString().TruncateLongString(5)}Mhz";
            textPos = new Vector2(left, top);
            var measurements = text.Split('\n');
            foreach (var measurement in measurements)
            {
                textSize = ImGui.CalcTextSize(measurement);
                draw.AddText(textPos, 0xFFFFFFFF, measurement);
                textPos.Y += textSize.Y + 5 * parent.Configuration.scaleSize.Y;
            }
        }
    }
}