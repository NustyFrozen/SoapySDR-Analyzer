using System.Drawing;
using System.Numerics;
using ImGuiNET;
using NLog;
using SoapyRTSA.Model;
using SoapyRTSA.View.tabs;
using SoapyVNACommon;
using SoapyVNACommon.Extentions;
using SoapyVNACommon.Fonts;
using Logger = NLog.Logger;
using UserScreenConfiguration = SoapySA.Extentions.UserScreenConfiguration;

namespace SoapyRTSA.View;

/// <summary>
///     The real time spectrum analyser widget: a density display of every overlapping transform with a
///     waterfall beneath it, and the usual tabs down the right hand side.
/// </summary>
public sealed class MainWindowView : IWidget
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public Configuration Configuration { get; }
    public PerformRtsa Sweep { get; }

    private readonly GraphView _graph;
    private readonly List<TabViewModel> _tabs;
    private TabViewModel? _activeTab;

    public event EventHandler? OnWidgetExit;
    public event EventHandler? OnWidgetEnter;

    public MainWindowView(string widgetName, Vector2 position, Vector2 windowSize, SdrDeviceCom deviceCom)
    {
        Configuration = new Configuration(widgetName);
        Configuration.InitConfiguration();

        //no preset yet: start where the device already is
        if (Configuration.SampleRate <= 0)
            Configuration.SampleRate = deviceCom.RxSampleRate;

        if (Configuration.CenterFrequency <= 0)
            try
            {
                Configuration.CenterFrequency = deviceCom.SdrDevice.GetFrequency(
                    Pothosware.SoapySDR.Direction.Rx,
                    deviceCom.RxAntenna.Item1
                );
            }
            catch (Exception exception)
            {
                Logger.Warn($"could not read the tuned frequency -> {exception.Message}");
            }

        Sweep = new PerformRtsa(this, Configuration, deviceCom);
        _graph = new GraphView(Configuration, Sweep);

        _tabs = new List<TabViewModel>
        {
            new DeviceView(Configuration, deviceCom, Sweep),
            new FrequencyView(Configuration, Sweep),
            new AmplitudeView(Configuration),
            new DisplayView(Configuration, Sweep),
            new TriggerView(Configuration),
            new MarkerView(Configuration, Sweep)
        };
    }

    public void InitWidget()
    {
        Theme.InitDefaultTheme();
        Theme.Refresh();
    }

    public void RenderWidget() => Render();

    private void Render()
    {
        DrawToolbar();

        ImGui.BeginChild("RTSA Graph", UserScreenConfiguration.GraphSize);
        _graph.Draw();
        ImGui.EndChild();

        ImGui.SetCursorPos(new Vector2(
            UserScreenConfiguration.GraphSize.X + UserScreenConfiguration.PercentX(UserScreenConfiguration.OptionGapXPct),
            UserScreenConfiguration.PositionOffset.Y + UserScreenConfiguration.PercentY(UserScreenConfiguration.OptionGapYPct)
        ));

        ImGui.BeginChild("RTSA Options", UserScreenConfiguration.OptionSize);
        if (_activeTab is { } tab)
        {
            Theme.ButtonTheme.Text = "Return";
            if (Theme.Button("rtsa_return", Theme.ButtonTheme))
            {
                _activeTab = null;
            }
            else
            {
                Theme.NewLine();
                Theme.NewLine();
                tab.Render();
            }
        }
        else
        {
            RenderTabSelector();
        }

        ImGui.EndChild();
        DrawCursor();
    }

    private void RenderTabSelector()
    {
        ImGui.SetCursorPosY(UserScreenConfiguration.OptionSize.Y / 2.0f
            - (_tabs.Count - 1) * Theme.ButtonTheme.Size.Y / 2.0f);

        for (var i = 0; i < _tabs.Count; i++)
        {
            Theme.ButtonTheme.Text = _tabs[i].tabName;
            if (Theme.Button(_tabs[i].tabName, Theme.ButtonTheme))
                _activeTab = _tabs[i];

            Theme.NewLine();
        }
    }

    /// <summary>Hold, marker picker and preset buttons, on the strip above the plot.</summary>
    private void DrawToolbar()
    {
        var draw = ImGui.GetForegroundDrawList();
        var start = ImGui.GetWindowPos();
        start.X = UserScreenConfiguration.PositionOffset.X;
        var end = start;
        end.X += UserScreenConfiguration.GraphSize.X;
        draw.AddRectFilled(start, end, Color.FromArgb(12, 12, 12).ToUint());

        if (Theme.DrawTextButton(Configuration.Paused ? $"{FontAwesome5.Play} Resume" : $"{FontAwesome5.Pause} Hold"))
            Configuration.Paused = !Configuration.Paused;

        ImGui.SameLine();
        ImGui.Text("Marker");
        foreach (var marker in Configuration.Markers)
        {
            ImGui.SameLine();
            if (!ImGui.RadioButton($"{marker.Id + 1}", Configuration.SelectedMarker == marker.Id))
                continue;

            //clicking the marker already selected toggles it off the graph
            if (Configuration.SelectedMarker == marker.Id)
                marker.IsActive = !marker.IsActive;
            else
                marker.IsActive = true;

            Configuration.SelectedMarker = marker.Id;
        }

        ImGui.SameLine();
        if (Theme.DrawTextButton("Save User Preset"))
            Configuration.SaveConfig();

        ImGui.SameLine();
        if (Theme.DrawTextButton("Load User Preset"))
            Configuration.LoadConfig();
    }

    private static void DrawCursor()
    {
        ImGui.SetMouseCursor(ImGuiMouseCursor.None);
        var position = ImGui.GetMousePos();
        var half = UserScreenConfiguration.ScaleUniform(5);
        var draw = ImGui.GetForegroundDrawList();

        draw.AddLine(new Vector2(position.X - half, position.Y), new Vector2(position.X + half, position.Y),
            Color.White.ToUint());
        draw.AddLine(new Vector2(position.X, position.Y - half), new Vector2(position.X, position.Y + half),
            Color.White.ToUint());
    }

    public void WidgetEnter() => OnWidgetEnter?.Invoke(this, EventArgs.Empty);

    public void WidgetExit() => OnWidgetExit?.Invoke(this, EventArgs.Empty);
}
