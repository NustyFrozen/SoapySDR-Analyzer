namespace SoapyRTSA.Model;

public abstract class TabViewModel
{
    public abstract string tabName { get; }
    public abstract void Render();
}

/// <summary>Window applied before the transform. Wider main lobe buys lower sidelobes.</summary>
public enum FftWindowType
{
    Rectangular,
    Hann,
    Hamming,
    BlackmanHarris,
    FlatTop
}
