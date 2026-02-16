using SoapySA.Extentions;

namespace SoapySA.Model;

public abstract class MeasurementFeature
{
    public abstract string Name { get; }
    /// <summary>
    /// returns true if has special settings to the feature, if not MeasurementManager will render default settings
    /// </summary>
    /// <returns></returns>
    public abstract bool renderSettings();

    /// <summary>
    /// returns true if has special graph rendering hook, if not MeasurementManager will render default Graph
    /// </summary>
    /// <returns></returns>
    public abstract bool renderGraph();
}