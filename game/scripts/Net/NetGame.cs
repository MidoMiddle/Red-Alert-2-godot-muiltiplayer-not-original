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
/// when it joins. It lives in the root of the scene tree, so it outlasts the screens — the menu, the lobby, the loading, the match —
/// and keeps the session going every frame until the match runs; then the lockstep of the match (<see cref="SimulationHost"/>) has the
/// network. <see cref="Close"/> ends it.
/// </summary>
public partial class NetGame : Node
{
    public const int DefaultPort = 27015;

    /// <summary>The router argument that makes the match scene a network match.</summary>
    public const string MatchArgument = "net";

    private readonly Func<string, ulong, bool> _hasMap;

    private NetGame(LiteNetTransport transport, MapEntry? map)
    {
        Name = "NetGame";
        Transport = transport;
        Map = map;
        var maps = MapCatalog.Load(MapPreviews.ReadMapFiles()).Maps;
        Maps = maps;
        _hasMap = (id, hash) => maps.Any(entry => entry.Map.Id == id && HashOf(entry) == hash);
    }

    /// <summary>The network game going on, if any.</summary>
    public static NetGame? Current { get; private set; }

    public LiteNetTransport Transport { get; }

    public HostSession? Host { get; private set; }

    public ClientSession? Client { get; private set; }

    public bool IsHost => Host is not null;

    /// <summary>The maps of this computer: the host picks from them, a client is checked against them.</summary>
    public List<MapEntry> Maps { get; }

    /// <summary>The map of the game: the host's choice; for a client, the one of its catalog the lobby names.</summary>
    public MapEntry? Map { get; private set; }

    public SessionPhase Phase => Host?.Phase ?? Client!.Phase;

    public LobbyState? Lobby => Host?.Lobby ?? Client?.Lobby;

    public StartGame? Start => Host?.Started ?? Client?.Start;

    public PlayerId? LocalPlayer => IsHost ? Host!.Host : Client!.Player;

    public IReadOnlyList<PlayerId> ActivePlayers => Host?.ActivePlayers ?? Client!.ActivePlayers;

    public Reject? Rejection => Client?.Rejection;

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
        var game = new NetGame(transport, map);
        var lobby = new LobbyState(map.Map.Id, HashOf(map), new MatchOptions(FogOfWar: true),
            [new LobbySlot(1, PlayerKind.Human, AiDifficulty.Medium, playerName, DataFiles.Current.Rules.DefaultFaction.Id, "", 0, 1, LobbySlot.RandomStart, false)]);
        game.Host = new HostSession(transport, Identity, lobby, new PlayerId(1),
            new HostSettings { MaxPlayers = map.Map.Players, DefaultFaction = DataFiles.Current.Rules.DefaultFaction.Id });
        Attach(tree, game);
        GD.Print($"[net] hosting {map.Map.Id} on port {transport.LocalPort}");
        return game;
    }

    /// <summary>Joins the game hosted at <paramref name="address"/>:<paramref name="port"/>.</summary>
    public static NetGame Join(SceneTree tree, string playerName, string address, int port)
    {
        Current?.Close();
        var transport = new LiteNetTransport();
        var game = new NetGame(transport, null);
        game.Client = new ClientSession(transport, Identity, playerName, game._hasMap);
        game.Client.LobbyChanged += lobby => game.Map = game.Maps.FirstOrDefault(entry => entry.Map.Id == lobby.MapId && HashOf(entry) == lobby.MapHash);
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

        Transport.Dispose();
        QueueFree();
    }
}
