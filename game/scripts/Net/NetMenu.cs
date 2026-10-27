using System;
using System.Globalization;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;

namespace Rts.Client.Net;

/// <summary>
/// The network menu (docs/implementation/M9_network.md, 4.16, step 9): a name, then either a game hosted here — a map and a port — or a
/// game joined by the address of its host, or through a relay server (step 12): a room opened there for the map, or joined by its code.
/// Each opens the lobby. The launch options <c>--net-host PORT --net-map ID</c>, <c>--net-join ADDRESS:PORT</c> and
/// <c>--net-relay ADDRESS:PORT --net-room new|CODE</c> (with <c>--net-name NAME</c>) do the same without a click, for trying two copies on
/// one computer.
/// </summary>
public partial class NetMenu : Control
{
    public const string Scene = "res://scenes/net/NetMenu.tscn";

    // This computer: a second copy of the game joins the first one here.
    private static readonly string DefaultAddress = string.Join(':', "127.0.0.1", NetGame.DefaultPort.ToString(CultureInfo.InvariantCulture));
    private static readonly string DefaultRelay = string.Join(':', "127.0.0.1", NetGame.DefaultRelayPort.ToString(CultureInfo.InvariantCulture));

    private LineEdit _name = null!;
    private LineEdit _port = null!;
    private LineEdit _address = null!;
    private LineEdit _relay = null!;
    private LineEdit _room = null!;
    private OptionButton _map = null!;
    private Label _status = null!;
    private MapEntry[] _maps = [];

    public override void _Ready()
    {
        var background = new ColorRect { Color = new Color(0.09f, 0.1f, 0.12f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var column = new VBoxContainer { Position = new Vector2(96, 72), CustomMinimumSize = new Vector2(720, 0) };
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);
        column.AddChild(Title(Tr("UI_NET_TITLE"), 34));

        column.AddChild(Title(Tr("UI_NET_NAME"), 18));
        _name = new LineEdit { Text = System.Environment.UserName, CustomMinimumSize = new Vector2(360, 40) };
        column.AddChild(_name);

        column.AddChild(Title(Tr("UI_NET_HOST"), 24));
        _maps = MapCatalog.Load(MapPreviews.ReadMapFiles(), Rts.Client.App.DataFiles.Current.Rules.MapTypes).Maps.Where(entry => entry.Map.Players >= 2 && !MapCatalog.IsHidden(entry.File)).ToArray();
        _map = new OptionButton { CustomMinimumSize = new Vector2(480, 40) };
        foreach (var entry in _maps)
        {
            _map.AddItem(string.Format(CultureInfo.InvariantCulture, Tr("UI_MAP_LIST_ITEM"), entry.Map.Name, entry.Map.Players));
        }

        column.AddChild(_map);
        var hostRow = new HBoxContainer();
        hostRow.AddChild(new Label { Text = Tr("UI_NET_PORT") });
        _port = new LineEdit { Text = NetGame.DefaultPort.ToString(CultureInfo.InvariantCulture), CustomMinimumSize = new Vector2(120, 40) };
        hostRow.AddChild(_port);
        var create = new Button { Text = Tr("UI_NET_CREATE"), CustomMinimumSize = new Vector2(220, 44) };
        create.Pressed += Host;
        hostRow.AddChild(create);
        column.AddChild(hostRow);

        column.AddChild(Title(Tr("UI_NET_JOIN"), 24));
        var joinRow = new HBoxContainer();
        _address = new LineEdit { Text = DefaultAddress, CustomMinimumSize = new Vector2(320, 40) };
        joinRow.AddChild(_address);
        var join = new Button { Text = Tr("UI_NET_CONNECT"), CustomMinimumSize = new Vector2(220, 44) };
        join.Pressed += Join;
        joinRow.AddChild(join);
        column.AddChild(joinRow);

        // Through the relay: the room is for the map chosen above; the others join it by its code.
        column.AddChild(Title(Tr("UI_NET_RELAY"), 24));
        var relayRow = new HBoxContainer();
        _relay = new LineEdit { Text = DefaultRelay, CustomMinimumSize = new Vector2(320, 40) };
        relayRow.AddChild(_relay);
        var open = new Button { Text = Tr("UI_NET_OPEN_ROOM"), CustomMinimumSize = new Vector2(220, 44) };
        open.Pressed += OpenRoom;
        relayRow.AddChild(open);
        column.AddChild(relayRow);
        var roomRow = new HBoxContainer();
        _room = new LineEdit { PlaceholderText = Tr("UI_NET_ROOM_CODE"), CustomMinimumSize = new Vector2(320, 40) };
        roomRow.AddChild(_room);
        var enter = new Button { Text = Tr("UI_NET_JOIN_ROOM"), CustomMinimumSize = new Vector2(220, 44) };
        enter.Pressed += JoinRoom;
        roomRow.AddChild(enter);
        column.AddChild(roomRow);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(720, 0) };
        column.AddChild(_status);
        var back = new Button { Text = Tr("UI_BUTTON_BACK"), CustomMinimumSize = new Vector2(220, 44) };
        back.Pressed += () => SceneRouter.Instance?.Back();
        column.AddChild(back);

        if (NetGame.Current?.Rejection is { } rejection)
        {
            _status.Text = string.Format(Tr("UI_NET_REJECTED"), rejection.Detail);
            NetGame.Current.Close();
        }

        Launch();
    }

    // The launch options: host or join at once — after the router's fade into this screen, as it opens nothing while it switches.
    private void Launch()
    {
        var args = OS.GetCmdlineUserArgs();
        string? Option(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        if (Option("--net-name") is { } name)
        {
            _name.Text = name;
        }

        if (Option("--net-host") is { } port)
        {
            _port.Text = port;
            if (Option("--net-map") is { } map && Array.FindIndex(_maps, entry => entry.Map.Id == map) is var index and >= 0)
            {
                _map.Selected = index;
            }

            GetTree().CreateTimer(0.5).Timeout += Host;
        }
        else if (Option("--net-join") is { } address)
        {
            _address.Text = address;
            GetTree().CreateTimer(0.5).Timeout += Join;
        }
        else if (Option("--net-relay") is { } relay && Option("--net-room") is { } room)
        {
            _relay.Text = relay;
            if (room == "new")
            {
                if (Option("--net-map") is { } map && Array.FindIndex(_maps, entry => entry.Map.Id == map) is var index and >= 0)
                {
                    _map.Selected = index;
                }

                GetTree().CreateTimer(0.5).Timeout += OpenRoom;
            }
            else
            {
                _room.Text = room;
                GetTree().CreateTimer(0.5).Timeout += JoinRoom;
            }
        }
    }

    private void Host()
    {
        if (_map.Selected < 0 || !int.TryParse(_port.Text, out var port))
        {
            _status.Text = Tr("UI_NET_BAD_PORT");
            return;
        }

        try
        {
            NetGame.Create(GetTree(), PlayerName(), port, _maps[_map.Selected]);
            SceneRouter.Instance?.GoTo(NetLobby.Scene);
        }
        catch (InvalidOperationException error)
        {
            _status.Text = error.Message;
        }
    }

    private void Join()
    {
        if (!TryAddress(_address.Text, NetGame.DefaultPort, out var address, out var port))
        {
            _status.Text = Tr("UI_NET_BAD_ADDRESS");
            return;
        }

        NetGame.Join(GetTree(), PlayerName(), address, port);
        SceneRouter.Instance?.GoTo(NetLobby.Scene);
    }

    private void OpenRoom()
    {
        if (_map.Selected < 0 || !TryAddress(_relay.Text, NetGame.DefaultRelayPort, out var address, out var port))
        {
            _status.Text = Tr("UI_NET_BAD_RELAY");
            return;
        }

        NetGame.CreateInRelay(GetTree(), PlayerName(), address, port, _maps[_map.Selected]);
        SceneRouter.Instance?.GoTo(NetLobby.Scene);
    }

    private void JoinRoom()
    {
        var code = _room.Text.Trim().ToUpperInvariant();
        if (code.Length == 0 || !TryAddress(_relay.Text, NetGame.DefaultRelayPort, out var address, out var port))
        {
            _status.Text = Tr("UI_NET_BAD_RELAY");
            return;
        }

        NetGame.JoinInRelay(GetTree(), PlayerName(), address, port, code);
        SceneRouter.Instance?.GoTo(NetLobby.Scene);
    }

    // «address» or «address:port».
    private static bool TryAddress(string text, int defaultPort, out string address, out int port)
    {
        var parts = text.Trim().Split(':');
        address = parts[0];
        port = defaultPort;
        return address.Length > 0 && (parts.Length == 1 || int.TryParse(parts[1], out port));
    }

    private string PlayerName() => string.IsNullOrWhiteSpace(_name.Text) ? "Player" : _name.Text.Trim();

    private static Label Title(string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}
