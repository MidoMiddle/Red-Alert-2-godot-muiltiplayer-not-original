using System.Collections.Generic;
using Godot;
using Rts.Simulation.Events;

namespace Rts.Client.Hud;

/// <summary>
/// The text notices of the base low in the middle of the field, above the command bar (docs/implementation/M3_base_economy.md, 4.16;
/// the top is the debug panel's): what <see cref="EvaMessages"/> says about the local player's events, newest at the bottom, three at
/// most, each fading out after a few seconds.
/// </summary>
public partial class Notifications : VBoxContainer
{
    public const int MaxLines = 3;
    public const double ShowSeconds = 4;
    private const double FadeSeconds = 0.6;

    private readonly EvaMessages _messages = new();
    private readonly List<(Label Label, double Age)> _lines = [];
    private SimulationHost? _host;
    private double _now;

    /// <summary>Listens to the events of the match.</summary>
    public void Bind(SimulationHost host)
    {
        _host = host;
        host.EventRaised += OnEvent;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Alignment = AlignmentMode.End;
        SetAnchorsPreset(LayoutPreset.BottomWide);
        OffsetLeft = 0;
        OffsetRight = -SidebarLayout.Width;
        OffsetBottom = -CommandBarLayout.Height - 16;
        OffsetTop = OffsetBottom - 130;
    }

    public override void _ExitTree()
    {
        if (_host is not null)
        {
            _host.EventRaised -= OnEvent;
        }
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("Notifications.process");
        _now += delta;
        for (var i = _lines.Count - 1; i >= 0; i--)
        {
            var (label, age) = _lines[i];
            age += delta;
            _lines[i] = (label, age);
            var left = ShowSeconds - age;
            label.Modulate = new Color(1, 1, 1, (float)Mathf.Clamp(left / FadeSeconds, 0, 1));
            if (left <= 0)
            {
                label.QueueFree();
                _lines.RemoveAt(i);
            }
        }
    }

    private void OnEvent(SimEvent simEvent)
    {
        if (_messages.Next(simEvent, SimulationHost.LocalPlayer, _now) is { } key)
        {
            Say(key);
        }
    }

    /// <summary>A notice of the client, not of an event of the simulation: the radar coming on or going off (M6 step 11).</summary>
    public void Say(string key)
    {
        if (_lines.Count == MaxLines)
        {
            _lines[0].Label.QueueFree();
            _lines.RemoveAt(0);
        }

        var label = new Label
        {
            Text = Tr(key),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", new Color("#F4C843"));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        AddChild(label);
        _lines.Add((label, 0));
    }
}
