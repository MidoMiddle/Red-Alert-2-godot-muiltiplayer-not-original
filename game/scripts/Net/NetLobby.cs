using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Net.Protocol.Messages;
using Rts.Net.Session;

namespace Rts.Client.Net;

/// <summary>
/// The minimal network lobby (docs/implementation/M9_network.md, 4.16, step 9; the full one comes with M10): the map, everyone's
/// seat with their name, faction, team and whether they are ready; this computer's faction and ready; on the host, Start once there
/// are two people or more and all are ready. When the match starts every computer goes to its loading screen; a refusal or a lost
/// host goes back to the network menu with the reason. Launch options for the tests: <c>--net-ready</c> makes this computer ready
/// at once, <c>--net-players N</c> makes the host start once N people are ready.
/// </summary>
public partial class NetLobby : Control
{
    public const string Scene = "res://scenes/net/NetLobby.tscn";

    private static readonly string[] Sides = ["union", "alliance"];

    private Label _title = null!;
    private Label _slots = null!;
    private OptionButton _faction = null!;
    private CheckBox _ready = null!;
    private Button _start = null!;
    private LobbyState? _shown;
    private string? _shownRoom;
    private bool _routed;
    private bool _autoReady;
    private int _autoStart;

    public override void _Ready()
    {
        var background = new ColorRect { Color = new Color(0.09f, 0.1f, 0.12f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var column = new VBoxContainer { Position = new Vector2(96, 72), CustomMinimumSize = new Vector2(820, 0) };
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 30);
        column.AddChild(_title);
        _slots = new Label { CustomMinimumSize = new Vector2(820, 240) };
        _slots.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(_slots);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        _faction = new OptionButton { CustomMinimumSize = new Vector2(220, 40) };
        foreach (var side in Sides)
        {
            _faction.AddItem(Tr(FactionKey(side)));
        }

        _faction.ItemSelected += _ => SendChoice();
        row.AddChild(_faction);
        _ready = new CheckBox { Text = Tr("UI_NET_READY") };
        _ready.Toggled += _ => SendChoice();
        row.AddChild(_ready);
        column.AddChild(row);

        _start = new Button { Text = Tr("UI_NET_START"), CustomMinimumSize = new Vector2(220, 44), Visible = NetGame.Current?.IsHost == true };
        _start.Pressed += () => NetGame.Current?.StartMatch();
        column.AddChild(_start);
        var leave = new Button { Text = Tr("UI_NET_LEAVE"), CustomMinimumSize = new Vector2(220, 44) };
        leave.Pressed += Leave;
        column.AddChild(leave);

        var args = OS.GetCmdlineUserArgs();
        _autoReady = args.Contains("--net-ready");
        _autoStart = Array.IndexOf(args, "--net-players") is var i and >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var n) ? n : 0;
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("NetLobby.process");
        if (NetGame.Current is not { } game)
        {
            SceneRouter.Instance?.Replace(NetMenu.Scene);
            return;
        }

        // The router opens nothing while it switches: what this screen asks of it waits for the next frame.
        if (SceneRouter.Instance?.Switching == true)
        {
            return;
        }

        switch (game.Phase)
        {
            case SessionPhase.Connecting:
                _title.Text = Tr("UI_NET_CONNECTING");
                return;
            case SessionPhase.Rejected or SessionPhase.Closed:
                // Back to the network menu, which tells why.
                SceneRouter.Instance?.Replace(NetMenu.Scene);
                return;
            case SessionPhase.Loading or SessionPhase.Running when !_routed && game.Map is { } map:
                _routed = true;
                SceneRouter.Instance?.GoTo(SceneRouter.MatchScene, new Dictionary<string, object>
                {
                    [SimulationHost.MapArgument] = map.File,
                    [NetGame.MatchArgument] = game,
                });
                return;
        }

        if (game.Lobby is not { } lobby)
        {
            return;
        }

        if (!ReferenceEquals(lobby, _shown) || game.RoomCode != _shownRoom)
        {
            _shown = lobby;
            _shownRoom = game.RoomCode;
            Show(game, lobby);
        }

        if (_autoReady && !_ready.ButtonPressed)
        {
            _ready.ButtonPressed = true;
        }

        var people = lobby.Slots.Length;
        _start.Disabled = people < 2 || !lobby.Slots.All(slot => slot.Ready);
        if (_autoStart > 0 && game.IsHost && people >= _autoStart && !_start.Disabled)
        {
            _autoStart = 0;
            game.StartMatch();
        }
    }

    private void Show(NetGame game, LobbyState lobby)
    {
        var map = game.Map?.Map.Name ?? lobby.MapId;
        _title.Text = string.Format(CultureInfo.InvariantCulture, Tr("UI_NET_LOBBY_TITLE"), map, lobby.Slots.Length, game.Map?.Map.Players ?? lobby.Slots.Length);
        if (game.Relay is not null)
        {
            // Through the relay the others need the code of the room: the host gives it to them.
            _title.Text += "\n" + (game.RoomCode is { } code ? string.Format(CultureInfo.InvariantCulture, Tr("UI_NET_ROOM"), code) : Tr("UI_NET_ROOM_OPENING"));
        }
        _slots.Text = string.Join('\n', lobby.Slots.Select(slot =>
            $"{slot.Slot}. {slot.Name}{(slot.Slot == game.LocalPlayer?.Value ? " *" : "")} — {Tr(FactionKey(slot.Faction))}, "
            + $"{string.Format(CultureInfo.InvariantCulture, Tr("UI_NET_TEAM"), slot.Team)} — {(slot.Ready ? Tr("UI_NET_READY") : Tr("UI_NET_NOT_READY"))}"));
    }

    // This computer's faction and readiness: the host sets its own slot, a client asks the host.
    private void SendChoice()
    {
        if (NetGame.Current is not { Lobby: { } lobby } game || game.LocalPlayer is not { } player
            || lobby.Slots.FirstOrDefault(slot => slot.Slot == player.Value) is not { } mine)
        {
            return;
        }

        var faction = Sides[Math.Max(0, _faction.Selected)];
        if (game.IsHost)
        {
            game.Host!.SetLobby(lobby with
            {
                Slots = lobby.Slots.Select(slot => slot.Slot == mine.Slot ? slot with { Faction = faction, Ready = _ready.ButtonPressed } : slot).ToImmutableArray(),
            });
        }
        else if (game.Client!.Phase == SessionPhase.Lobby)
        {
            game.Client.ChangeSlot(new LobbyChange(faction, "", mine.Color, mine.Team, mine.StartIndex, _ready.ButtonPressed));
        }
    }

    private static string FactionKey(string faction) => faction switch
    {
        "alliance" => "UI_FACTION_ALLIANCE",
        "cult" => "UI_FACTION_CULT",
        _ => "UI_FACTION_UNION",
    };

    private void Leave()
    {
        NetGame.Current?.Close();
        SceneRouter.Instance?.Replace(NetMenu.Scene);
    }
}
