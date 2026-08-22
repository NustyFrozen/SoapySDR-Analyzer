using ImGuiNET;
using SoapyRTSA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapyRTSA.View.tabs;

/// <summary>Reference level, how far down the display reaches, and the offset applied to every readout.</summary>
public sealed class AmplitudeView : TabViewModel
{
    public override string tabName => $"{FontAwesome5.Plus} Amplitude";

    private readonly Configuration _config;

    private string _refLevel = "-20";
    private string _range = "100";
    private string _offset = "0";
    private int _divisions = 10;

    public AmplitudeView(Configuration config)
    {
        _config = config;

        SyncFromConfig();
        _config.OnConfigLoadEnd += (_, _) => SyncFromConfig();
    }

    private void SyncFromConfig()
    {
        _refLevel = _config.RefLevelDb.ToString();
        _range = _config.RangeDb.ToString();
        _offset = _config.OffsetDb.ToString();
        _divisions = _config.ScalePerDivision;
    }

    public override void Render()
    {
        Theme.Text("Ref Level (dB, top of screen)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Ref Level";
        if (Theme.GlowingInput("rtsa_ref", ref _refLevel, Theme.InputTheme))
            if (double.TryParse(_refLevel, out var value))
                _config.RefLevelDb = value;

        Theme.Text("Range (dB below ref)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Range";
        if (Theme.GlowingInput("rtsa_range", ref _range, Theme.InputTheme))
            if (double.TryParse(_range, out var value) && value > 0)
                _config.RangeDb = value;

        Theme.Text($"{FontAwesome5.Plus} Offset (dB)", Theme.InputTheme);
        Theme.InputTheme.Prefix = "Offset";
        if (Theme.GlowingInput("rtsa_offset", ref _offset, Theme.InputTheme))
            if (double.TryParse(_offset, out var value))
                _config.OffsetDb = value;

        Theme.NewLine();
        Theme.Text("Divisions (4-20)", Theme.InputTheme);
        if (ImGui.InputInt("Divisions", ref _divisions))
        {
            _divisions = Math.Clamp(_divisions, 4, 20);
            _config.ScalePerDivision = _divisions;
        }

        Theme.NewLine();
        Theme.Text(
            $"Display window\n{_config.DisplayTopDb + _config.OffsetDb:0.#} dB down to "
                + $"{_config.DisplayBottomDb + _config.OffsetDb:0.#} dB",
            Theme.InputTheme
        );
    }
}
