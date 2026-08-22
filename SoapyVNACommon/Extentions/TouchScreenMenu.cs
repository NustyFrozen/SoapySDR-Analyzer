using System.ComponentModel;
using System.Numerics;
using ImGuiNET;
using SoapySA.Extentions;

namespace SoapyVNACommon.Extentions;

public unsafe class TouchScreenMenu
{
    /// <summary>Keypad size: 400x400px @1080p -> 20.8% x 37% of the screen.</summary>
    private const float PickerWidthPct = 400.0f / UserScreenConfiguration.DesignWidth;

    private const float PickerHeightPct = 400.0f / UserScreenConfiguration.DesignHeight;

    public static List<TouchScreenMenu> picks = new List<TouchScreenMenu>();
    private string _pick = string.Empty;
    public event PropertyChangedEventHandler OnNumberPicked;
    private void applyValueAndExit()
    {
        if (_pick  != string.Empty)
        {
             Global.TryFormatFreq(_pick, out double value);
            OnNumberPicked(this, new PropertyChangedEventArgs(value.ToString()));
        }
        picks.Remove(this);
    }

    public static void pickFrequency(PropertyChangedEventHandler e)
    {
        var pick = new TouchScreenMenu();
        pick.OnNumberPicked += e;
        picks.Add(pick);
    }
    public void RenderFrequencyPicker()
    {
        ImGui.SetNextWindowSize(UserScreenConfiguration.Percent(PickerWidthPct, PickerHeightPct));
        ImGui.Begin("Pick Frequency", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar);
        ImGui.Text(_pick);
        var windowSize = ImGui.GetWindowSize();
        var theme = Theme.GetbuttonTheme();
        theme.Size = (windowSize/6) * .7f;
        for (int i = 1; i < 10; i++)
        {
            theme.Text = i.ToString();
            if (Theme.Button($"{i.ToString()}_freqPicker_{this.GetHashCode()}",theme))
                _pick += i.ToString();
            ImGui.SetCursorPos(new Vector2(windowSize.X / 4.0f * (i % 3),windowSize.Y / 4.0f * (i / 3)));
        }

        var options = new[] { "Hz", "KHz","MHz"};
        for (int i = 0; i < options.Length; i++)
        {
            theme.Text = options[i];
            ImGui.SetCursorPos(new Vector2(windowSize.X - theme.Size.X ,windowSize.Y / 4.0f * (i / 3)));
            if (Theme.Button($"{options[i]}_freqPicker_{this.GetHashCode()}",theme))
                _pick += options[i].Replace("Hz", "");
            
        }
        ImGui.SetCursorPos(windowSize - theme.Size);
        if (Theme.Button(".",theme)) 
            _pick += ".";
        
        ImGui.End();
    }
}