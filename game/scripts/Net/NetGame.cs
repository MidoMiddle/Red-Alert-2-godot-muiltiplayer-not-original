using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;
using Rts.Client.Matches;
using Rts.Net.Protocol.Messages;
using Rts.Net.Session;
using Rts.Net.Transport;
using Rts.Simulation;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Client.Net;

/// <summary>
/// A network game of this computer, from the network menu to the end of its match (docs/implementation/M9_network.md, 4.16, step 9):
/// the connection (<see cref="LiteNetTransport"/>) and the session — <see cref="HostSession"/> when it hosts, <see cref="ClientSession"/>
/// when it joins. Directly, or through a room of the relay server (step 12, <see cref="RelayTransport"/>): then the connection is to
/// the relay only, and the one who opened the room hosts. It lives in the root of the scene tree, so it outlasts the screens — the menu, the lobby, the loading, the match —
/// and keeps the session going every frame until the match runs; then the lockstep of the match (<see cref="SimulationHost"/>) has the
/// network. <see cref="Close"/> ends it.
/// </summary>
public partial class NetGame : Node
{
    public const int DefaultPort = 27015;

    /// <summary>The port of the relay server (<c>Rts.Server</c>) unless its address says another.</summary>
    public const int DefaultRelayPort = 27020;

    /// <summary>The router argument that makes the match scene a network match.</summary>
    public const string MatchArgument = "net";

    private readonly Func<string, ulong, bool> _hasMap;

    private NetGame(LiteNetTransport socket, ITransport transport, MapEntry? map)
    {
        Name = "NetGame";
        Socket = socket;
        Transport = transport;
        Map = map;
        var maps = MapCatalog.Load(MapPreviews.ReadMapFiles()).Maps;
        Maps = maps;
        _hasMap = (id, hash) => maps.Any(entry => entry.Map.Id == id && HashOf(entry) == hash);
    }

    /// <summary>The network game going on, if any.</summary>
    public static NetGame? Current { get; private set; }

    /// <summary>The real connection: to the other computers, or to the relay.</summary>
    public LiteNetTransport Socket { get; }

    /// <summary>The network as the sessions and the lockstep see it: the connection itself, or the room of the relay.</summary>
    public ITransport Transport { get; }

    /// <summary>The room of the relay; null when the game goes directly.</summary>
    public RelayTransport? Relay => Transport as RelayTransport;

    /// <summary>The code of the room of the relay, for the others to join; null directly or before the relay answers.</summary>
    public string? RoomCode => Relay?.RoomCode;

    public HostSession? Host { get; private set; }

    public ClientSession? Client { get; private set; }

    public bool IsHost => Host is not null;

    /// <summary>The maps of this computer: the host picks from them, a client is checked against them.</summary>
    public List<MapEntry> Maps { get; }

    /// <summary>The map of the game: the host's choice; for a client, the one of its catalog the lobby names.</summary>
    public MapEntry? Map { get; private set; }

    /// <summary>The phase of the session; closed once the relay is gone before the match.</summary>
    public SessionPhase Phase =>
        Relay is { Closed: true } && (Host?.Phase ?? Client!.Phase) is SessionPhase.Connecting or SessionPhase.Lobby or SessionPhase.Loading
            ? Rejection is null ? SessionPhase.Closed : SessionPhase.Rejected
            : Host?.Phase ?? Client!.Phase;

    public LobbyState? Lobby => Host?.Lobby ?? Client?.Lobby;

    public StartGame? Start => Host?.Started ?? Client?.Start;

    public PlayerId? LocalPlayer => IsHost ? Host!.Host : Client!.Player;

    public IReadOnlyList<PlayerId> ActivePlayers => Host?.ActivePlayers ?? Client!.ActivePlayers;

    /// <summary>Why the game ended before its match: the host's refusal, the relay's, or the relay lost.</summary>
    public Reject? Rejection =>
        Client?.Rejection ?? Relay?.Rejection
        ?? (Relay is { Closed: true } ? new Reject(RejectReason.RoomNotFound, TranslationServer.Translate("UI_NET_RELAY_LOST")) : null);

    /// <summary>The identity of this build: what a host checks of a client.</summary>
    public static NetIdentity Identity => new(GameInfo.Version, SimulationVersion.Current, DataFiles.Current.Hash);

    /// <summary>The 64 bits of a map's hash the protocol carries: the first of its SHA-256.</summary>
    public static ulong HashOf(MapEntry entry) => ulong.Parse(entry.Hash[..16], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Hosts a game on <paramref name="port"/> on <paramref name="map"/>; the host takes seat 1.</summary>
    public static NetGame Create(SceneTree tree, string playerName, int port, MapEntry map)
    {
        Current?.Close();
        var transport = new LiteNetTransport();
        transport.Listen(port);
        var game = new NetGame(transport, transport, map);
        game.Host = NewHost(transport, playerName, map);
        Attach(tree, game);
        GD.Print($"[net] hosting {map.Map.Id} on port {transport.LocalPort}");
        return game;
    }

    /// <summary>Opens a room on the relay at <paramref name="address"/>:<paramref name="port"/> and hosts <paramref name="map"/> in it.</summary>
    public static NetGame CreateInRelay(SceneTree tree, string playerName, string address, int port, MapEntry map)
    {
        Current?.Close();
        var socket = new LiteNetTransport();
        var relay = new RelayTransport(socket);
        var game = new NetGame(socket, relay, map);
        game.Host = NewHost(relay, playerName, map);
        socket.Connect(address, port);
        Attach(tree, game);
        GD.Print($"[net] opening a room on the relay {address}:{port} for {map.Map.Id}");
        return game;
    }

    /// <summary>Joins the room <paramref name="code"/> of the relay at <paramref name="address"/>:<paramref name="port"/>.</summary>
    public static NetGame JoinInRelay(SceneTree tree, string playerName, string address, int port, string code)
    {
        Current?.Close();
        var socket = new LiteNetTransport();
        var relay = new RelayTransport(socket, code);
        var game = new NetGame(socket, relay, null);
        game.Client = NewClient(game, relay, playerName, code);
        socket.Connect(address, port);
        Attach(tree, game);
        GD.Print($"[net] joining room {code} on the relay {address}:{port}");
        return game;
    }

    // The host of a game takes seat 1 of its lobby.
    private static HostSession NewHost(ITransport transport, string playerName, MapEntry map)
    {
        var lobby = new LobbyState(map.Map.Id, HashOf(map), new MatchOptions(FogOfWar: true),
            [new LobbySlot(1, PlayerKind.Human, AiDifficulty.Medium, playerName, DataFiles.Current.Rules.DefaultFaction.Id, "", 0, 1, LobbySlot.RandomStart, false)]);
        return new HostSession(transport, Identity, lobby, new PlayerId(1),
            new HostSettings { MaxPlayers = map.Map.Players, DefaultFaction = DataFiles.Current.Rules.DefaultFaction.Id });
    }

    private static ClientSession NewClient(NetGame game, ITransport transport, string playerName, string roomCode = "")
    {
        var client = new ClientSession(transport, Identity, playerName, game._hasMap, roomCode);
        client.LobbyChanged += lobby => game.Map = game.Maps.FirstOrDefault(entry => entry.Map.Id == lobby.MapId && HashOf(entry) == lobby.MapHash);
        return client;
    }

    /// <summary>Joins the game hosted at <paramref name="address"/>:<paramref name="port"/>.</summary>
    public static NetGame Join(SceneTree tree, string playerName, string address, int port)
    {
        Current?.Close();
        var transport = new LiteNetTransport();
        var game = new NetGame(transport, transport, null);
        game.Client = NewClient(game, transport, playerName);
        transport.Connect(address, port);
        Attach(tree, game);
        GD.Print($"[net] joining {address}:{port}");
        return game;
    }

    private static void Attach(SceneTree tree, NetGame game)
    {
        Current = game;
        game.ProcessMode = ProcessModeEnum.Always;
        tree.Root.AddChild(game);
    }

    private SessionPhase? _loggedPhase;
    private string? _loggedRoom;

    public override void _Process(double delta)
    {
        // Until the match runs the session reads the network; then the lockstep does.
        if (Phase is not SessionPhase.Running)
        {
            Host?.Update((long)(delta * 1000));
            Client?.Update();
        }

        if (Phase != _loggedPhase)
        {
            _loggedPhase = Phase;
            GD.Print($"[net] {Phase}{(Rejection is { } reject ? $": {reject.Reason} — {reject.Detail}" : "")}");
        }

        if (RoomCode is { } code && code != _loggedRoom)
        {
            _loggedRoom = code;
            GD.Print($"[net] room {code}");
        }
    }

    /// <summary>
    /// The host starts the match of its lobby: a player for every slot on the map's starts — a slot's own, the free ones in an order
    /// drawn from the seed for the others — and an input delay of 3 ticks to begin with.
    /// </summary>
    public void StartMatch()
    {
        if (Host is not { } host || Map is not { } map)
        {
            return;
        }

        var seed = MatchSetups.NewSeed();
        var starts = map.Map.Starts.Select(start => start.Cell).ToList();
        var free = Enumerable.Range(0, starts.Count).Where(index => host.Lobby.Slots.All(slot => slot.StartIndex != index)).ToList();
        var random = new Random(unchecked((int)seed));
        free = free.OrderBy(_ => random.Next()).ToList();
        var players = host.Lobby.Slots.Select(slot =>
        {
            var index = slot.StartIndex != LobbySlot.RandomStart ? slot.StartIndex : free[0];
            free.Remove(index);
            return new MatchPlayer(slot.Slot, slot.Kind, slot.Difficulty, slot.Faction, slot.Color, slot.Team, index, starts[index]) { Country = slot.Country };
        }).ToList();
        var setup = new MatchSetup(map.Map.Id, seed, host.Lobby.Options, [.. players], [.. starts]);
        host.Start(new StartGame(setup, SimulationSettings.DefaultTicksPerSecond, 15, HashOf(map), 3));
    }

    /// <summary>How far this computer has loaded the match, in percent.</summary>
    public void ReportLoaded(int percent)
    {
        if (Phase != SessionPhase.Loading)
        {
            return;
        }

        if (IsHost)
        {
            Host!.ReportLoaded(percent);
        }
        else
        {
            Client!.ReportLoaded(percent);
        }
    }

    /// <summary>Ends the network game: the connections close and the node goes.</summary>
    public void Close()
    {
        if (Current == this)
        {
            Current = null;
        }

        Socket.Dispose();
        QueueFree();
    }
}
