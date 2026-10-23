using Godot;
using Rts.Client.Matches;
using Rts.Simulation.Events;
using Rts.Simulation.Matches;

namespace Rts.Client.UI;

/// <summary>
/// The results of a match (docs/implementation/M5_first_bot.md, 4.10, step 12), over the field of <c>Main.tscn</c> like the pause menu:
/// it opens <see cref="DelaySeconds"/> after the match ends — the banner shows first — and pauses the game. The outcome and the length,
/// a row of the statistics of every player, the graph of the strength of the armies, and three buttons: Play Again — the same match
/// with a new seed, on the spot; Save Replay — into user://replays, its path shown; Exit — as Quit in the pause menu. Not while a replay
/// plays back. The look of RA2 comes with M6.
/// </summary>
public partial class MatchResults : CanvasLayer
{
    public const double DelaySeconds = 4;

    private SimulationHost? _host;
    private Control _overlay = null!;
    private Label _title = null!;
    private Label _duration = null!;
    private GridContainer _table = null!;
    private ArmyGraph _graph = null!;
    private Button _save = null!;
    private Label _saved = null!;
    private double _countdown = -1;

    /// <summary>Whether the results are up.</summary>
    public bool IsOpen => _overlay.Visible;

    /// <summary>Listens to the match of <paramref name="host"/>.</summary>
    public void Bind(SimulationHost host)
    {
        _host = host;
        host.EventRaised += OnEvent;
        host.SessionStarted += Close;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 11;

        _overlay = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);
        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(dimmer);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        panel.AddChild(column);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 56);
        _title.AddThemeColorOverride("font_color", new Color("#F4C843"));
        column.AddChild(_title);
        _duration = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        column.AddChild(_duration);

        _table = new GridContainer { Columns = 1 + MatchSummary.Columns.Length };
        _table.AddThemeConstantOverride("h_separation", 18);
        column.AddChild(_table);

        column.AddChild(new Label { Text = Tr("UI_RESULTS_ARMY") });
        _graph = new ArmyGraph { CustomMinimumSize = new Vector2(900, 220) };
        column.AddChild(_graph);

        _saved = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        column.AddChild(_saved);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        column.AddChild(buttons);
        var again = AddButton("UI_RESULTS_PLAY_AGAIN", buttons);
        again.Name = "PlayAgain";
        again.Pressed += () => _host?.PlayAgain();
        _save = AddButton("UI_RESULTS_SAVE_REPLAY", buttons);
        _save.Name = "SaveReplay";
        _save.Pressed += SaveReplay;
        var exit = AddButton("UI_RESULTS_EXIT", buttons);
        exit.Name = "ExitResults";
        exit.Pressed += () => PauseMenu.LeaveMatch(GetTree());
    }

    public override void _ExitTree()
    {
        if (_host is not null)
        {
            _host.EventRaised -= OnEvent;
            _host.SessionStarted -= Close;
        }
    }

    public override void _Process(double delta)
    {
        if (_countdown < 0)
        {
            return;
        }

        _countdown -= delta;
        if (_countdown < 0)
        {
            Open();
        }
    }

    private void OnEvent(SimEvent simEvent)
    {
        if (simEvent is MatchEnded && _host is { IsPlayback: false })
        {
            _countdown = DelaySeconds;
        }
    }

    /// <summary>Fills the screen from the world as it is now and pauses the game.</summary>
    private void Open()
    {
        if (_host is null || MatchSummary.Of(_host.Simulation.World, SimulationHost.LocalPlayer, _host.Simulation.Settings.TicksPerSecond) is not { } summary)
        {
            return;
        }

        _title.Text = Tr(summary.Outcome switch
        {
            MatchOutcome.Victory => "UI_RESULTS_VICTORY",
            MatchOutcome.Defeat => "UI_RESULTS_DEFEAT",
            _ => "UI_RESULTS_DRAW",
        });
        _duration.Text = string.Format(Tr("UI_RESULTS_DURATION"), summary.Duration);
        FillTable(summary);
        _graph.Plot(summary);
        _saved.Text = "";
        _save.Disabled = false;
        _overlay.Show();
        GetTree().Paused = true;
    }

    private void Close()
    {
        _countdown = -1;
        if (_overlay.Visible)
        {
            _overlay.Hide();
            GetTree().Paused = false;
        }
    }

    private void FillTable(MatchSummary summary)
    {
        foreach (var child in _table.GetChildren())
        {
            child.QueueFree();
        }

        _table.AddChild(new Label { Text = Tr("UI_RESULTS_PLAYER") });
        foreach (var column in MatchSummary.Columns)
        {
            _table.AddChild(new Label { Text = Tr(column), HorizontalAlignment = HorizontalAlignment.Right });
        }

        foreach (var row in summary.Rows)
        {
            var name = new HBoxContainer();
            name.AddChild(new ColorRect { Color = PlayerColors.Of(row.Player), CustomMinimumSize = new Vector2(18, 18), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            var text = Tr(row.IsYou ? "UI_RESULTS_YOU" : "UI_RESULTS_BOT") + (row.Won ? $" — {Tr("UI_RESULTS_WINNER")}" : "");
            name.AddChild(new Label { Text = text });
            _table.AddChild(name);
            foreach (var number in row.Numbers)
            {
                _table.AddChild(new Label { Text = number.ToString(System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Right });
            }
        }
    }

    private void SaveReplay()
    {
        if (_host?.SaveReplay() is { } path)
        {
            _saved.Text = string.Format(Tr("UI_RESULTS_SAVED"), path);
            _save.Disabled = true;
        }
    }

    private Button AddButton(string key, Container parent)
    {
        var button = new Button { Text = Tr(key), CustomMinimumSize = new Vector2(220, 52) };
        parent.AddChild(button);
        return button;
    }
}
