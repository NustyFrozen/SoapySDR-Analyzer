using ImGuiNET;
using SoapyRTSA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;

namespace SoapyRTSA.View.tabs;

/// <summary>
///     Markers read the envelope of the persistence grid, so putting the display on hold freezes what
///     they report and a burst can be measured after the fact.
/// </summary>
public sealed class MarkerView : TabViewModel
{
    public override string tabName => $"{FontAwesome5.MapPin} Marker";

    private readonly Configuration _config;
    private readonly PerformRtsa _sweep;

    private readonly string[] _markerNames;
    private readonly string[] _deltaNames;
    private int _selected;
    private int _deltaIndex;
    private string _position = "0";

    public MarkerView(Configuration config, PerformRtsa sweep)
    {
        _config = config;
        _sweep = sweep;

        _markerNames = new string[config.Markers.Length];
        _deltaNames = new string[config.Markers.Length + 1];
        _deltaNames[0] = "none";
        for (var i = 0; i < config.Markers.Length; i++)
        {
            _markerNames[i] = $"Marker {i + 1}";
            _deltaNames[i + 1] = $"Marker {i + 1}";
        }

        SyncFromConfig();
        _config.OnConfigLoadEnd += (_, _) => SyncFromConfig();
    }

    private void SyncFromConfig()
    {
        _selected = _config.SelectedMarker;
        var marker = Current;
        _deltaIndex = marker.DeltaReference;
        _position = marker.Position.ToString();
    }

    private RtsaMarker Current => _config.Markers[Math.Clamp(_selected, 0, _config.Markers.Length - 1)];

    public override void Render()
    {
        var paused = _config.Paused;
        Theme.ButtonTheme.Text = paused ? "Resume Sweep" : "Hold Display";
        if (Theme.Button("rtsa_hold", Theme.ButtonTheme))
            _config.Paused = !paused;

        Theme.Text(
            paused
                ? "Held. Markers read the frozen envelope."
                : "Running. Hold the display for a stable reading.",
            Theme.InputTheme
        );
        Theme.NewLine();

        if (Theme.GlowingCombo("rtsa_marker_select", ref _selected, _markerNames, Theme.InputTheme))
        {
            _config.SelectedMarker = _selected;
            SyncFromConfig();
        }

        var marker = Current;

        var active = marker.IsActive;
        if (ImGui.Checkbox("Active", ref active))
            marker.IsActive = active;

        Theme.Text("Frequency", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Frequency";
        if (Theme.GlowingInput("rtsa_marker_freq", ref _position, Theme.InputTheme))
            if (Global.TryFormatFreq(_position, out var frequency) && frequency > 0)
            {
                marker.Position = frequency;
                marker.IsActive = true;
            }

        Theme.ButtonTheme.Text = "Peak Search";
        if (Theme.Button("rtsa_marker_peak", Theme.ButtonTheme))
        {
            marker.Position = PeakFrequency();
            marker.IsActive = true;
            _position = marker.Position.ToString();
        }

        Theme.NewLine();
        Theme.Text("Delta reference", Theme.InputTheme);
        if (Theme.GlowingCombo("rtsa_marker_delta", ref _deltaIndex, _deltaNames, Theme.InputTheme))
            marker.DeltaReference = _deltaIndex;

        Theme.NewLine();
        var bandPower = marker.BandPower;
        if (ImGui.Checkbox("Band power", ref bandPower))
            marker.BandPower = bandPower;

        if (bandPower)
        {
            Theme.Text("Band span", Theme.InputTheme);
            Theme.InputTheme.Prefix = "Band span";
            if (Theme.GlowingInput("rtsa_marker_band", ref marker.BandPowerSpanStr, Theme.InputTheme))
                if (Global.TryFormatFreq(marker.BandPowerSpanStr, out var span) && span > 0)
                    marker.BandPowerSpan = span;
        }

        Theme.NewLine();
        Theme.Text(Readout(marker), Theme.InputTheme);
    }

    private string Readout(RtsaMarker marker)
    {
        if (!marker.IsActive)
            return "Marker off. Click the graph to place it.";

        var value = double.IsNegativeInfinity(marker.Value) ? "--" : $"{marker.Value:0.##} dB";
        var text = $"{marker.Position / 1e6:0.######} MHz\n{value}";

        var reference = marker.DeltaReference - 1;
        if (reference >= 0 && reference < _config.Markers.Length && reference != marker.Id)
        {
            var other = _config.Markers[reference];
            text += $"\n\ndelta to M{reference + 1}\n{(marker.Position - other.Position) / 1e6:0.######} MHz"
                + $"\n{marker.Value - other.Value:0.##} dB";
        }

        if (marker.BandPower)
            text += double.IsNegativeInfinity(marker.BandPowerValue)
                ? "\n\nband power --"
                : $"\n\nband power\n{marker.BandPowerValue:0.##} dBm over {marker.BandPowerSpan / 1e6:0.###} MHz";

        return text;
    }

    /// <summary>Frequency of the strongest column currently in the persistence grid.</summary>
    private double PeakFrequency()
    {
        var grid = _sweep.Grid;
        var columns = _sweep.ActiveColumns < 1 ? 1 : _sweep.ActiveColumns;
        var start = _sweep.DisplayStartHz;
        var span = _sweep.DisplayStopHz - start;
        if (span <= 0.0)
        {
            span = _sweep.EffectiveSpan;
            start = _config.CenterFrequency - span / 2.0;
        }

        var floor = _sweep.DensityReference * 0.002f;
        var bestColumn = 0;
        var bestRow = PerformRtsa.DensityRows;

        for (var column = 0; column < columns; column++)
            for (var row = 0; row < bestRow; row++)
            {
                if (grid.Cells[row * PerformRtsa.Columns + column] <= floor)
                    continue;

                bestRow = row;
                bestColumn = column;
                break;
            }

        return start + (bestColumn + 0.5) / columns * span;
    }
}
