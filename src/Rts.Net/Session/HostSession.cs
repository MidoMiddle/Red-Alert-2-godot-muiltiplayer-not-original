using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Net.Session;

/// <summary>Where a network session is: in the lobby, loading the match, playing it, or over.</summary>
public enum SessionPhase : byte
{
    Connecting = 0,
    Lobby = 1,
    Loading = 2,
    Running = 3,
    Rejected = 4,
    Closed = 5,
}

/// <summary>The rules of a host's lobby and start.</summary>
public sealed record HostSettings
{
    /// <summary>The seats of the match: the starts of its map, at most 8.</summary>
    public int MaxPlayers { get; init; } = PlayerId.MaxPlayers;

    /// <summary>The faction a newcomer's slot gets until its player chooses.</summary>
    public string DefaultFaction { get; init; } = "union";

    /// <summary>How long the host waits for everyone to load before dropping the slow (4.9).</summary>
    public int LoadTimeoutMs { get; init; } = 120_000;

    /// <summary>
    /// How long a refused client keeps its connection for the refusal to reach it: it closes the connection itself on reading it, the
    /// host after this time. Closed at once, the refusal would be lost on the way.
    /// </summary>
    public int RefusalLingerMs { get; init; } = 1000;
}

/// <summary>
/// The host of a network match before it runs (docs/implementation/M9_network.md, 4.4, 4.8, 4.9): it takes the clients that pass the
/// <see cref="Handshake"/> into free seats of its <see cref="LobbyState"/> and refuses the others with the reason, keeps everyone's lobby
/// up to date, starts the match with <see cref="StartGame"/> and holds tick 0 until every person has loaded (<see cref="LoadingBarrier"/>),
/// dropping who has not within the time. Then it sends <see cref="AllLoaded"/> and leaves the network to the lockstep, read through a
/// <see cref="HostRelay"/>.
/// <para>
/// Single-threaded, as the transport: the game calls <see cref="Update"/> every frame until <see cref="Phase"/> is
/// <see cref="SessionPhase.Running"/>.
/// </para>
/// </summary>
public sealed class HostSession
{
    private readonly ITransport _transport;
    private readonly Dictionary<PeerId, int> _seats = new();
    private readonly HashSet<PeerId> _greeting = new();
    private readonly List<PlayerId> _dropped = new();
    private readonly Dictionary<PeerId, long> _closing = new();
    private LoadingBarrier? _barrier;

    /// <param name="lobby">The lobby before anyone comes: the map, the options, the host's own slot and any bots.</param>
    public HostSession(ITransport transport, NetIdentity identity, LobbyState lobby, PlayerId host, HostSettings? settings = null)
    {
        _transport = transport;
        Identity = identity;
        Lobby = lobby;
        Host = host;
        Settings = settings ?? new HostSettings();
        Phase = SessionPhase.Lobby;
    }

    /// <summary>The lobby changed: a client came, left or chose something.</summary>
    public event Action<LobbyState>? LobbyChanged;

    /// <summary>A client was refused, for the log of the host.</summary>
    public event Action<PeerId, Reject>? Refused;

    /// <summary>Everyone has loaded: tick 0 begins now.</summary>
    public event Action? AllLoaded;

    public NetIdentity Identity { get; }

    public PlayerId Host { get; }

    public HostSettings Settings { get; }

    public SessionPhase Phase { get; private set; }

    public LobbyState Lobby { get; private set; }

    /// <summary>The match as started; null before <see cref="Start"/>.</summary>
    public StartGame? Started { get; private set; }

    /// <summary>The seat of every client, by its connection.</summary>
    public IReadOnlyDictionary<PeerId, int> Seats => _seats;

    /// <summary>How far each person still awaited has loaded.</summary>
    public IReadOnlyDictionary<PlayerId, int> Progress => _barrier?.Progress ?? new Dictionary<PlayerId, int>();

    /// <summary>The people who left or were dropped before the start.</summary>
    public IReadOnlyList<PlayerId> Dropped => _dropped;

    /// <summary>The people whose bundles the lockstep waits for: the persons of the match who are still here. Bots run inside the simulation.</summary>
    public IReadOnlyList<PlayerId> ActivePlayers =>
        Started is { } start ? People(start.Setup).Where(player => !_dropped.Contains(player)).ToList() : [];

    /// <summary>Takes what came from the network. Until the start: newcomers, choices, departures; while loading: progress and timeouts.</summary>
    public void Update(long elapsedMs)
    {
        if (Phase is not (SessionPhase.Lobby or SessionPhase.Loading))
        {
            return;
        }

        foreach (var (peer, left) in _closing.ToList())
        {
            if (left <= elapsedMs)
            {
                _closing.Remove(peer);
                _transport.Disconnect(peer);
            }
            else
            {
                _closing[peer] = left - elapsedMs;
            }
        }

        _transport.Poll();
        while (Phase is SessionPhase.Lobby or SessionPhase.Loading && _transport.TryReceive(out var received))
        {
            if (_closing.ContainsKey(received.Peer))
            {
                if (received.Kind == TransportEventKind.Disconnected)
                {
                    _closing.Remove(received.Peer);
                }

                continue;
            }

            switch (received.Kind)
            {
                case TransportEventKind.Connected:
                    _greeting.Add(received.Peer);
                    break;
                case TransportEventKind.Disconnected:
                    Gone(received.Peer, LeaveReason.Disconnected);
                    break;
                default:
                    Handle(received.Peer, received.Data);
                    break;
            }
        }

        if (Phase == SessionPhase.Loading && _barrier is { } barrier)
        {
            barrier.Advance(elapsedMs);
            if (barrier.TimedOut)
            {
                foreach (var slow in barrier.Waiting.Where(player => player != Host))
                {
                    var peer = _seats.First(seat => seat.Value == slow.Value).Key;
                    Refuse(peer, new Reject(RejectReason.LoadTimeout, $"Not loaded in {barrier.TimeoutMs / 1000} s."));
                    Gone(peer, LeaveReason.Timeout);
                }
            }

            Release();
        }
    }

    /// <summary>The host changes the lobby: its own slot, the bots, the options, the map. Everyone gets the new one.</summary>
    public void SetLobby(LobbyState lobby)
    {
        RequirePhase(SessionPhase.Lobby);
        Lobby = lobby;
        Broadcast(Lobby);
    }

    /// <summary>
    /// Starts the match: everyone gets <paramref name="start"/> and loads. Its setup holds a player for every slot of the lobby; the
    /// people among them are awaited, the host too (<see cref="ReportLoaded"/>).
    /// </summary>
    public void Start(StartGame start)
    {
        RequirePhase(SessionPhase.Lobby);
        var people = People(start.Setup).ToList();
        if (!people.Contains(Host) || people.Any(player => player != Host && !_seats.ContainsValue(player.Value)))
        {
            throw new ArgumentException("Every person of the match is the host or a client in the lobby.", nameof(start));
        }

        Started = start;
        _barrier = new LoadingBarrier(people, Settings.LoadTimeoutMs);
        Phase = SessionPhase.Loading;
        Broadcast(start);
    }

    /// <summary>How far the host has loaded, in percent.</summary>
    public void ReportLoaded(int percent)
    {
        RequirePhase(SessionPhase.Loading);
        if (_barrier!.Report(Host, percent))
        {
            Broadcast(new LoadProgress(Host, percent));
        }

        Release();
    }

    private void Handle(PeerId peer, byte[] data)
    {
        NetMessage message;
        try
        {
            message = MessageCodec.Read(data);
        }
        catch (MessageFormatException error)
        {
            if (_greeting.Contains(peer) && Handshake.Check(error) is { } reject)
            {
                Refuse(peer, reject);
            }

            return;
        }

        if (_greeting.Remove(peer))
        {
            if (message is Hello hello)
            {
                Greet(peer, hello);
            }
            else
            {
                _transport.Disconnect(peer);
            }

            return;
        }

        if (!_seats.TryGetValue(peer, out var seat))
        {
            return;
        }

        switch (message)
        {
            case LobbyChange change when Phase == SessionPhase.Lobby:
                Lobby = Lobby with { Slots = Lobby.Slots.Select(slot => slot.Slot == seat ? Changed(slot, change) : slot).ToImmutableArray() };
                Broadcast(Lobby);
                LobbyChanged?.Invoke(Lobby);
                break;
            case LoadProgress progress when Phase == SessionPhase.Loading && progress.Player.Value == seat:
                if (_barrier!.Report(progress.Player, progress.Percent))
                {
                    SendToOthers(peer, progress);
                }

                break;
        }
    }

    private void Greet(PeerId peer, Hello hello)
    {
        var reject = Handshake.Check(hello, Identity)
            ?? (Phase != SessionPhase.Lobby ? new Reject(RejectReason.GameInProgress, "The match has started.") : null);
        var seat = FreeSeat();
        if (reject is null && seat is null)
        {
            reject = new Reject(RejectReason.RoomFull, $"All {Settings.MaxPlayers} seats are taken.");
        }

        if (reject is not null)
        {
            Refuse(peer, reject);
            return;
        }

        _seats[peer] = seat!.Value;
        var slot = new LobbySlot(seat.Value, PlayerKind.Human, AiDifficulty.Medium, hello.PlayerName, Settings.DefaultFaction, "",
            Color: (seat.Value - 1) % PlayerId.MaxPlayers, Team: seat.Value, LobbySlot.RandomStart, Ready: false);
        Lobby = Lobby with { Slots = Lobby.Slots.Add(slot).Sort((a, b) => a.Slot.CompareTo(b.Slot)) };
        _transport.Send(peer, MessageCodec.Write(new Welcome(seat.Value)), DeliveryChannel.ReliableOrdered);
        Broadcast(Lobby);
        LobbyChanged?.Invoke(Lobby);
    }

    // The lowest seat no slot takes, within the seats of the match.
    private int? FreeSeat()
    {
        for (var seat = 1; seat <= Math.Min(Settings.MaxPlayers, PlayerId.MaxPlayers); seat++)
        {
            if (Lobby.Slots.All(slot => slot.Slot != seat))
            {
                return seat;
            }
        }

        return null;
    }

    // A client left — closed, lost, or dropped for loading too long: its seat is free in the lobby, a person gone while loading.
    private void Gone(PeerId peer, LeaveReason reason)
    {
        _greeting.Remove(peer);
        if (!_seats.Remove(peer, out var seat))
        {
            return;
        }

        if (Phase == SessionPhase.Lobby)
        {
            Lobby = Lobby with { Slots = Lobby.Slots.RemoveAll(slot => slot.Slot == seat) };
            Broadcast(Lobby);
            LobbyChanged?.Invoke(Lobby);
            return;
        }

        var player = new PlayerId(seat);
        _dropped.Add(player);
        _barrier?.Drop(player);
        Broadcast(new PlayerLeft(player, reason, 0));
    }

    // Everyone loaded: tick 0 for all; the network is the lockstep's from here.
    private void Release()
    {
        if (Phase != SessionPhase.Loading || _barrier is not { Done: true })
        {
            return;
        }

        Phase = SessionPhase.Running;
        Broadcast(new Protocol.Messages.AllLoaded());
        AllLoaded?.Invoke();
    }

    // The refusal goes, and the connection closes once it has arrived (RefusalLingerMs).
    private void Refuse(PeerId peer, Reject reject)
    {
        _transport.Send(peer, MessageCodec.Write(reject), DeliveryChannel.ReliableOrdered);
        _closing[peer] = Settings.RefusalLingerMs;
        _greeting.Remove(peer);
        Refused?.Invoke(peer, reject);
    }

    private static LobbySlot Changed(LobbySlot slot, LobbyChange change) => slot with
    {
        Faction = change.Faction, Country = change.Country, Color = change.Color, Team = change.Team, StartIndex = change.StartIndex,
        Ready = change.Ready,
    };

    private static IEnumerable<PlayerId> People(MatchSetup setup) =>
        setup.Players.Where(player => player.Kind == PlayerKind.Human).Select(player => player.Id);

    // To every client in a seat; those still greeting get nothing of the lobby.
    private void Broadcast(NetMessage message)
    {
        var bytes = MessageCodec.Write(message);
        foreach (var peer in _seats.Keys)
        {
            _transport.Send(peer, bytes, DeliveryChannel.ReliableOrdered);
        }
    }

    private void SendToOthers(PeerId sender, NetMessage message)
    {
        var bytes = MessageCodec.Write(message);
        foreach (var peer in _seats.Keys.Where(peer => peer != sender))
        {
            _transport.Send(peer, bytes, DeliveryChannel.ReliableOrdered);
        }
    }

    private void RequirePhase(SessionPhase phase)
    {
        if (Phase != phase)
        {
            throw new InvalidOperationException($"The session is {Phase}, not {phase}.");
        }
    }
}
