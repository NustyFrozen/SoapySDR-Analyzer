using Pothosware.SoapySDR;
using ProtoBuf;
using Range = Pothosware.SoapySDR.Range;

namespace SoapyVNACommon.Extentions
{
    [ProtoContract]
    public class SdrDeviceComDTO
    {
        [ProtoMember(1)] public string Descriptor { get; set; }
        [ProtoMember(2)] public double RxSampleRate { get; set; }
        [ProtoMember(3)] public double TxSampleRate { get; set; }
        [ProtoMember(4)] public uint AvailableRxChannels { get; set; }
        [ProtoMember(5)] public uint AvailableTxChannels { get; set; }
        [ProtoMember(6)] public List<double> RxGainValues { get; set; } = new();
        [ProtoMember(7)] public List<double> TxGainValues { get; set; } = new();
        [ProtoMember(8)] public string SensorData { get; set; }

        [ProtoMember(9)] public Dictionary<int, RangeListSurrogate> DeviceRxFrequencyRange { get; set; } = new();
        [ProtoMember(10)] public Dictionary<int, RangeListSurrogate> DeviceTxFrequencyRange { get; set; } = new();
        [ProtoMember(11)] public Dictionary<int, RangeListSurrogate> DeviceRxSampleRates { get; set; } = new();
        [ProtoMember(12)] public Dictionary<int, RangeListSurrogate> DeviceTxSampleRates { get; set; } = new();

        [ProtoMember(13)] public Dictionary<uint, StringListSurrogate> AvailableRxAntennas { get; set; } = new();
        [ProtoMember(14)] public Dictionary<uint, StringListSurrogate> AvailableTxAntennas { get; set; } = new();

        [ProtoMember(15)] public Dictionary<UIntStringTupleSurrogate, RangeIntTupleSurrogate> RxGains { get; set; } = new();
        [ProtoMember(16)] public Dictionary<UIntStringTupleSurrogate, RangeIntTupleSurrogate> TxGains { get; set; } = new();

        [ProtoMember(17)] public UIntStringTupleSurrogate RxAntenna { get; set; }
        [ProtoMember(18)] public UIntStringTupleSurrogate TxAntenna { get; set; }

        // Conversion to DTO
        public static SdrDeviceComDTO ToDTO(sdrDeviceCOM src)
        {
            return new SdrDeviceComDTO
            {
                Descriptor = src.Descriptor,
                RxSampleRate = src.rxSampleRate,
                TxSampleRate = src.txSampleRate,
                AvailableRxChannels = src.availableRxChannels,
                AvailableTxChannels = src.availableTxChannels,
                RxGainValues = src.rxGainValues.ToList(),
                TxGainValues = src.txGainValues.ToList(),
                SensorData = src.sensorData,
                DeviceRxFrequencyRange = src.deviceRxFrequencyRange.ToDictionary(kvp => kvp.Key, kvp => (RangeListSurrogate)kvp.Value),
                DeviceTxFrequencyRange = src.deviceTxFrequencyRange.ToDictionary(kvp => kvp.Key, kvp => (RangeListSurrogate)kvp.Value),
                DeviceRxSampleRates = src.deviceRxSampleRates.ToDictionary(kvp => kvp.Key, kvp => (RangeListSurrogate)kvp.Value),
                DeviceTxSampleRates = src.deviceTxSampleRates.ToDictionary(kvp => kvp.Key, kvp => (RangeListSurrogate)kvp.Value),
                AvailableRxAntennas = src.availableRxAntennas.ToDictionary(kvp => kvp.Key, kvp => (StringListSurrogate)kvp.Value),
                AvailableTxAntennas = src.availableTxAntennas.ToDictionary(kvp => kvp.Key, kvp => (StringListSurrogate)kvp.Value),
                RxGains = src.rxGains.ToDictionary(kvp => (UIntStringTupleSurrogate)kvp.Key, kvp => (RangeIntTupleSurrogate)kvp.Value),
                TxGains = src.txGains.ToDictionary(kvp => (UIntStringTupleSurrogate)kvp.Key, kvp => (RangeIntTupleSurrogate)kvp.Value),
                RxAntenna = src.rxAntenna,
                TxAntenna = src.txAntenna
            };
        }

        // Conversion from DTO
        public static sdrDeviceCOM FromDTO(SdrDeviceComDTO dto)
        {
            var result = new sdrDeviceCOM(dto.Descriptor)
            {
                rxSampleRate = dto.RxSampleRate,
                txSampleRate = dto.TxSampleRate,
                availableRxChannels = dto.AvailableRxChannels,
                availableTxChannels = dto.AvailableTxChannels,
                rxGainValues = new List<double>(dto.RxGainValues),
                txGainValues = new List<double>(dto.TxGainValues),
                sensorData = dto.SensorData,
                deviceRxFrequencyRange = dto.DeviceRxFrequencyRange.ToDictionary(kvp => kvp.Key, kvp => (RangeList)kvp.Value),
                deviceTxFrequencyRange = dto.DeviceTxFrequencyRange.ToDictionary(kvp => kvp.Key, kvp => (RangeList)kvp.Value),
                deviceRxSampleRates = dto.DeviceRxSampleRates.ToDictionary(kvp => kvp.Key, kvp => (RangeList)kvp.Value),
                deviceTxSampleRates = dto.DeviceTxSampleRates.ToDictionary(kvp => kvp.Key, kvp => (RangeList)kvp.Value),
                availableRxAntennas = dto.AvailableRxAntennas.ToDictionary(kvp => kvp.Key, kvp => (StringList)kvp.Value),
                availableTxAntennas = dto.AvailableTxAntennas.ToDictionary(kvp => kvp.Key, kvp => (StringList)kvp.Value),
                rxGains = dto.RxGains.ToDictionary(kvp => (Tuple<uint, string>)kvp.Key, kvp => (Tuple<Range, int>)kvp.Value),
                txGains = dto.TxGains.ToDictionary(kvp => (Tuple<uint, string>)kvp.Key, kvp => (Tuple<Range, int>)kvp.Value),
                rxAntenna = dto.RxAntenna,
                txAntenna = dto.TxAntenna
            };

            return result;
        }
    }

    [ProtoContract]
    public class StringListSurrogate
    {
        [ProtoMember(1)] public List<string> Items { get; set; } = new();

        public static implicit operator StringListSurrogate(StringList src) =>
            src == null ? null : new StringListSurrogate { Items = src.ToList() };

        public static implicit operator StringList(StringListSurrogate s) =>
            s == null ? null : new StringList(s.Items);
    }

    [ProtoContract]
    public class UIntStringTupleSurrogate
    {
        [ProtoMember(1)] public uint Item1 { get; set; }
        [ProtoMember(2)] public string Item2 { get; set; }

        public static implicit operator UIntStringTupleSurrogate(Tuple<uint, string> t) =>
            t == null ? null : new UIntStringTupleSurrogate { Item1 = t.Item1, Item2 = t.Item2 };

        public static implicit operator Tuple<uint, string>(UIntStringTupleSurrogate s) =>
            s == null ? null : Tuple.Create(s.Item1, s.Item2);
    }

    [ProtoContract]
    public class RangeIntTupleSurrogate
    {
        [ProtoMember(1)] public RangeSurrogate Range { get; set; }
        [ProtoMember(2)] public int Value { get; set; }

        public static implicit operator RangeIntTupleSurrogate(Tuple<Range, int> t) =>
            t == null ? null : new RangeIntTupleSurrogate { Range = t.Item1, Value = t.Item2 };

        public static implicit operator Tuple<Range, int>(RangeIntTupleSurrogate s) =>
            s == null ? null : Tuple.Create((Range)s.Range, s.Value);
    }

    [ProtoContract]
    public class RangeSurrogate
    {
        [ProtoMember(1)]
        public double Minimum { get; set; }

        [ProtoMember(2)]
        public double Maximum { get; set; }

        [ProtoMember(3)]
        public double Step { get; set; }

        public RangeSurrogate()
        { }

        public RangeSurrogate(double min, double max, double step)
        {
            Minimum = min;
            Maximum = max;
            Step = step;
        }

        // ✅ These methods must be static and public
        public static implicit operator RangeSurrogate(Range source) =>
            source == null ? null : new RangeSurrogate(source.Minimum, source.Maximum, source.Step);

        public static implicit operator Range(RangeSurrogate surrogate) =>
            surrogate == null ? null : new Range(surrogate.Minimum, surrogate.Maximum, surrogate.Step);
    }

    public static class RangeListExtensions
    {
        public static RangeListSurrogate ToSurrogate(this RangeList rl)
        {
            if (rl == null) return null;
            return new RangeListSurrogate(rl);
        }

        public static RangeList ToRangeList(this RangeListSurrogate surrogate)
        {
            if (surrogate == null) return null;
            var rl = new RangeList();
            foreach (var rs in surrogate.Ranges)
            {
                rl.Add(rs.ToRange());
            }
            return rl;
        }
    }

    public static class RangeExtensions
    {
        public static RangeSurrogate ToSurrogate(this Range r) =>
            r == null ? null : new RangeSurrogate(r.Minimum, r.Maximum, r.Step);

        public static Range ToRange(this RangeSurrogate surrogate) =>
            surrogate == null ? null : new Range(surrogate.Minimum, surrogate.Maximum, surrogate.Step);
    }

    [ProtoContract]
    public class RangeListSurrogate
    {
        [ProtoMember(1)]
        public List<RangeSurrogate> Ranges { get; set; } = new List<RangeSurrogate>();

        // Parameterless ctor needed for protobuf-net
        public RangeListSurrogate()
        { }

        public RangeListSurrogate(IEnumerable<Range> ranges)
        {
            if (ranges != null)
            {
                foreach (var r in ranges)
                {
                    Ranges.Add(r.ToSurrogate());
                }
            }
        }

        public static implicit operator RangeListSurrogate(RangeList source) =>
    source == null ? null : new RangeListSurrogate(source);

        public static implicit operator RangeList(RangeListSurrogate surrogate)
        {
            if (surrogate == null) return null;
            var rl = new RangeList();
            foreach (var r in surrogate.Ranges)
                rl.Add(r);
            return rl;
        }
    }
}