using System.Numerics;

namespace SoapyVNACommon.Extentions;

public class IqdcBlocker
{
    private readonly double _alpha;
    private double _prevI;
    private double _prevOutI;
    private double _prevOutQ;
    private double _prevQ;

    public IqdcBlocker(double alpha = 0.995f)
    {
        this._alpha = alpha;
    }

    public void ProcessSignal(Complex[] input)
    {
        var data = input.AsSpan();
        for (var pos = 0; pos < data.Length; pos++)
        {
            var sample = data[pos];

            input[pos] = new Complex(sample.Real - _prevI + _alpha * _prevOutI
                , sample.Imaginary - _prevQ + _alpha * _prevOutQ);
            _prevI = sample.Real;
            _prevQ = sample.Imaginary;
            _prevOutI = input[pos].Real;
            _prevOutQ = input[pos].Imaginary;
        }
    }
}