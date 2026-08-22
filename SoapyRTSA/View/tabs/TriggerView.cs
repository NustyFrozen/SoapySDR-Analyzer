using ImGuiNET;
using SoapyRTSA.Model;
using SoapyVNACommon;
using SoapyVNACommon.Fonts;

namespace SoapyRTSA.View.tabs;

/// <summary>
///     Three trigger slots. With none armed every transform reaches the display; with any armed a frame
///     gets through if any of them accepts it, so three different things can be watched at once.
/// </summary>
public sealed class TriggerView : TabViewModel
{
    public override string tabName => $"{FontAwesome5.Bolt} Trigger";

    private static readonly string[] Modes = { "Above magnitude", "Above mask", "Time qualified" };

    private readonly Configuration _config;
    private readonly int[] _modeIndex = new int[3];

    public TriggerView(Configuration config)
    {
        _config = config;

        SyncFromConfig();
        _config.OnConfigLoadEnd += (_, _) => SyncFromConfig();
    }

    private void SyncFromConfig()
    {
        for (var i = 0; i < _config.Triggers.Length && i < _modeIndex.Length; i++)
        {
            _modeIndex[i] = (int)_config.Triggers[i].Mode;
            _config.Triggers[i].SyncEditors();
        }
    }

    public override void Render()
    {
        var armed = 0;
        foreach (var trigger in _config.Triggers)
            if (trigger.Enabled)
                armed++;

        Theme.Text(armed == 0 ? "Free running (nothing armed)" : $"{armed} of 3 armed", Theme.InputTheme);
        Theme.NewLine();

        for (var i = 0; i < _config.Triggers.Length; i++)
            RenderTrigger(i, _config.Triggers[i]);

        Theme.NewLine();
        var editing = _config.MaskEditing;
        if (ImGui.Checkbox("Draw mask on the graph", ref editing))
            _config.MaskEditing = editing;

        if (editing)
            Theme.Text("Click the graph to drop mask points.\nMarkers are on hold while editing.", Theme.InputTheme);

        Theme.Text($"Mask points: {_config.Mask.Count}", Theme.InputTheme);
        Theme.ButtonTheme.Text = "Clear Mask";
        if (Theme.Button("rtsa_clear_mask", Theme.ButtonTheme))
            _config.ClearMask();
    }

    private void RenderTrigger(int index, RtsaTrigger trigger)
    {
        var enabled = trigger.Enabled;
        if (ImGui.Checkbox($"Trigger {index + 1}", ref enabled))
            trigger.Enabled = enabled;

        if (!enabled)
        {
            Theme.NewLine();
            return;
        }

        ImGui.Text($"  hits {trigger.Hits}");

        if (Theme.GlowingCombo($"rtsa_trigger_mode_{index}", ref _modeIndex[index], Modes, Theme.InputTheme))
            trigger.Mode = (TriggerMode)_modeIndex[index];

        switch (trigger.Mode)
        {
            case TriggerMode.Magnitude:
                Theme.Text("Level (dB)", Theme.InputTheme);
                Theme.InputTheme.Prefix = "Level";
                if (Theme.GlowingInput($"rtsa_trigger_level_{index}", ref trigger.MagnitudeStr, Theme.InputTheme))
                    if (double.TryParse(trigger.MagnitudeStr, out var level))
                        trigger.MagnitudeDb = level;
                break;

            case TriggerMode.Mask:
                Theme.Text(
                    _config.Mask.Count == 0
                        ? "No mask drawn yet: tick the box below\nand click the graph."
                        : "Fires on any bin above the mask.",
                    Theme.InputTheme
                );
                break;

            default:
                Theme.Text("Period (us)", Theme.InputTheme);
                Theme.InputTheme.Prefix = "Period";
                if (Theme.GlowingInput($"rtsa_trigger_period_{index}", ref trigger.PeriodStr, Theme.InputTheme))
                    if (double.TryParse(trigger.PeriodStr, out var period) && period > 0)
                        trigger.PeriodUs = period;

                Theme.Text($"Offset {trigger.OffsetPercent:0.#}% "
                    + $"({trigger.PeriodUs * trigger.OffsetPercent / 100.0:0.#} us into the period)", Theme.InputTheme);
                if (Theme.Slider($"rtsa_trigger_offset_{index}", 0, 100, ref trigger.OffsetKnob, Theme.SliderTheme))
                    trigger.OffsetPercent = trigger.OffsetKnob;

                Theme.Text("Duration (us)", Theme.InputTheme);
                Theme.InputTheme.Prefix = "Duration";
                if (Theme.GlowingInput($"rtsa_trigger_duration_{index}", ref trigger.DurationStr, Theme.InputTheme))
                    if (double.TryParse(trigger.DurationStr, out var duration) && duration > 0)
                        trigger.DurationUs = duration;
                break;
        }

        Theme.NewLine();
    }
}
