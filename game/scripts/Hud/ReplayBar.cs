using System.Globalization;
using Godot;
using Rts.Client.Matches;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The controls of watching a replay (docs/implementation/M6_menus_skirmish.md, 4.16, step 15), at the top of the field while one plays:
/// pause and go on, the time watched of the whole, the speed — 1×, 2×, 4×, 8× of the speed it was played at — whose eyes to watch with
/// (a player: its fog and its sidebar; or the whole map), whether the checksums held at the end, and Exit back to where it was chosen.
/// Rewinding is not there (M6, 4.16).
/// </summary>
public partial class ReplayBar : PanelContainer
{
    private SimulationHost? _host;
    private Button _pause = null!;
    private Label _time = null!;
    private Label _status = null!;
    private OptionButton _viewer = null!;
    private readonly Button[] _speeds = new Button[SimulationHost.PlaybackFactors.Length];
    private Replay? _shown;

    public void Bind(SimulationHost host) => _host = host;

    public override void _Ready()
    {
        AnchorLeft = 0f;
        AnchorRight = 0f;
        OffsetLeft = 24f;
        OffsetTop = 16f;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        AddChild(row);
        row.AddChild(new Label { Text = Tr("UI_REPLAY_WATCHING") });
        _pause = new Button { CustomMinimumSize = new Vector2(96, 36), FocusMode = FocusModeEnum.None };
        _pause.Pressed += () =>
        {
            if (_host is not null)
            {
                _host.Paused = !_host.Paused;
            }
        };
        row.AddChild(_pause);
        _time = new Label { CustomMinimumSize = new Vector2(120, 0) };
        row.AddChild(_time);

        var group = new ButtonGroup();
        for (var i = 0; i < _speeds.Length; i++)
        {
            var factor = SimulationHost.PlaybackFactors[i];
            var button = new Button
            {
                Name = $"Speed{factor}",
                Text = string.Format(CultureInfo.InvariantCulture, Tr("UI_REPLAY_SPEED"), factor),
                ToggleMode = true,
                ButtonGroup = group,
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(48, 36),
            };
            button.Pressed += () =>
            {
                if (_host is not null)
                {
                    _host.PlaybackFactor = factor;
                }
            };
            row.AddChild(button);
            _speeds[i] = button;
        }

        _viewer = new OptionButton { Name = "Viewer", CustomMinimumSize = new Vector2(220, 36), FocusMode = FocusModeEnum.None, TooltipText = Tr("UI_REPLAY_VIEW_HINT") };
        _viewer.ItemSelected += index => _host?.Watch(index == 0 ? null : new PlayerId(_viewer.GetItemId((int)index)));
        row.AddChild(_viewer);
        _status = new Label();
        row.AddChild(_status);
        var exit = new Button { Text = Tr("UI_REPLAY_EXIT"), CustomMinimumSize = new Vector2(96, 36), FocusMode = FocusModeEnum.None };
        exit.Pressed += () => UI.PauseMenu.LeaveMatch(GetTree());
        row.AddChild(exit);
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("ReplayBar.process");
        Visible = _host?.Playback is not null;
        if (_host?.Playback is not { } replay)
        {
            return;
        }

        if (!ReferenceEquals(replay, _shown))
        {
            _shown = replay;
            FillViewer(replay.Settings.Match);
        }

        var tps = _host.Simulation.Settings.TicksPerSecond;
        _time.Text = string.Format(CultureInfo.InvariantCulture, Tr("UI_REPLAY_TIME"),
            ReplayListing.Length(_host.Simulation.CurrentTick, tps), ReplayListing.Length(replay.TickCount, tps));
        _pause.Text = Tr(_host.Paused ? "UI_REPLAY_PLAY" : "UI_REPLAY_PAUSE");
        for (var i = 0; i < _speeds.Length; i++)
        {
            _speeds[i].SetPressedNoSignal(SimulationHost.PlaybackFactors[i] == _host.PlaybackFactor);
        }

        _status.Text = !_host.PlaybackFinished ? ""
            : _host.PlaybackVerified ? Tr("UI_REPLAY_FINISHED_OK")
            : Tr("UI_REPLAY_FINISHED_BAD");
    }

    // The whole map first, then every player of the match by name; the one watched now selected.
    private void FillViewer(MatchSetup? setup)
    {
        _viewer.Clear();
        _viewer.AddItem(Tr("UI_REPLAY_VIEW_ALL"), 0);
        foreach (var player in setup?.Players ?? [])
        {
            var name = _host!.Playback!.Network?.Names.TryGetValue(player.Slot, out var person) == true ? person
                : player.Kind == PlayerKind.Ai ? string.Format(CultureInfo.InvariantCulture, Tr("UI_REPLAY_VIEW_BOT"), player.Slot)
                : string.Format(CultureInfo.InvariantCulture, Tr("UI_REPLAY_VIEW_PLAYER"), player.Slot);
            _viewer.AddItem(name, player.Slot);
        }

        _viewer.Selected = _host!.WatchingAll ? 0 : _viewer.GetItemIndex(SimulationHost.LocalPlayer.Value);
    }
}
