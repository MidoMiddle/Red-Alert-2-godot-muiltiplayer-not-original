using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Net.Session;

/// <summary>
/// A client of a network match before it runs (docs/implementation/M9_network.md, 4.4, 4.8, 4.9): once connected it says
/// <see cref="Hello"/>, takes the seat the host gives it or the refusal, follows the lobby — and leaves it with
/// <see cref="RejectReason.MapHash"/> when it has not the map of the lobby — loads when the match starts and begins tick 0 on
/// <see cref="AllLoaded"/>. Then the network is the lockstep's.
/// <para>Single-threaded, as the transport: the game calls <see cref="Update"/> every frame until <see cref="Phase"/> is
/// <see cref="SessionPhase.Running"/>.</para>
/// </summary>
public sealed class ClientSession
{
    private readonly ITransport _transport;
    private readonly Func<string, ulong, bool> _hasMap;
    private readonly Dictionary<PlayerId, int> _progress = new();
    private readonly List<PlayerId> _dropped = new();
    private PeerId? _host;

    /// <param name="hasMap">Whether this computer has the map of the given id and hash.</param>
    public ClientSession(ITransport transport, NetIdentity identity, string playerName, Func<string, ulong, bool> hasMap, string roomCode = "")
    {
        _transport = transport;
        _hasMap = hasMap;
        Identity = identity;
        PlayerName = playerName;
        RoomCode = roomCode;
        Phase = SessionPhase.Connecting;
    }

    public event Action<LobbyState>? LobbyChanged;

    /// <summary>The match started: load it.</summary>
    public event Action<StartGame>? Started;

    /// <summary>Everyone has loaded: tick 0 begins now.</summary>
    public event Action? AllLoaded;

    /// <summary>The host refused us, or we left a lobby we cannot play (<see cref="Rejection"/> says why).</summary>
    public event Action<Reject>? Rejected;

    public NetIdentity Identity { get; }

    public string PlayerName { get; }

    public string RoomCode { get; }

    public SessionPhase Phase { get; private set; }

    /// <summary>Our seat, once the host has given it.</summary>
    public PlayerId? Player { get; private set; }

    public LobbyState? Lobby { get; private set; }

    public StartGame? Start { get; private set; }

    public Reject? Rejection { get; private set; }

    /// <summary>How far every person has loaded, as the host passes it on.</summary>
    public IReadOnlyDictionary<PlayerId, int> Progress => _progress;

    /// <summary>The people who left or were dropped before the start.</summary>
    public IReadOnlyList<PlayerId> Dropped => _dropped;

    /// <summary>The people whose bundles the lockstep waits for.</summary>
    public IReadOnlyList<PlayerId> ActivePlayers =>
        Start is { } start
            ? start.Setup.Players.Where(player => player.Kind == PlayerKind.Human && !_dropped.Contains(player.Id)).Select(player => player.Id).ToList()
            : [];

    public void Update()
    {
        if (Phase is SessionPhase.Running or SessionPhase.Rejected or SessionPhase.Closed)
        {
            return;
        }

        _transport.Poll();
        while (Phase is SessionPhase.Connecting or SessionPhase.Lobby or SessionPhase.Loading && _transport.TryReceive(out var received))
        {
            switch (received.Kind)
            {
                case TransportEventKind.Connected when _host is null:
                    _host = received.Peer;
                    Send(Identity.Hello(PlayerName, RoomCode));
                    break;
                case TransportEventKind.Disconnected when received.Peer == _host:
                    Phase = Rejection is null ? SessionPhase.Closed : SessionPhase.Rejected;
                    break;
                case TransportEventKind.Message when received.Peer == _host:
                    Handle(received.Data);
                    break;
            }
        }
    }

    /// <summary>Our choices in the lobby: faction, country, colour, team, start, ready.</summary>
    public void ChangeSlot(LobbyChange change)
    {
        if (Phase != SessionPhase.Lobby)
        {
            throw new InvalidOperationException($"The session is {Phase}, not in the lobby.");
        }

        Send(change);
    }

    /// <summary>How far we have loaded, in percent; everyone sees it.</summary>
    public void ReportLoaded(int percent)
    {
        if (Phase != SessionPhase.Loading || Player is not { } player)
        {
            throw new InvalidOperationException($"The session is {Phase}, not loading.");
        }

        _progress[player] = percent;
        Send(new LoadProgress(player, percent));
    }

    private void Handle(byte[] data)
    {
        NetMessage message;
        try
        {
            message = MessageCodec.Read(data);
        }
        catch (MessageFormatException error) when (error.Error == MessageError.WrongProtocolVersion)
        {
            Leave(new Reject(RejectReason.ProtocolVersion, $"The host speaks the network protocol {error.ProtocolVersion}, we {NetProtocol.Version}."));
            return;
        }
        catch (MessageFormatException)
        {
            return;
        }

        switch (message)
        {
            case Welcome welcome:
                Player = new PlayerId(welcome.PlayerSlot);
                Phase = SessionPhase.Lobby;
                break;
            case Reject reject:
                Leave(reject);
                break;
            case LobbyState lobby when Phase == SessionPhase.Lobby:
                Lobby = lobby;
                if (!_hasMap(lobby.MapId, lobby.MapHash))
                {
                    Leave(new Reject(RejectReason.MapHash, $"There is no map {lobby.MapId} ({lobby.MapHash:x16}) on this computer."));
                    return;
                }

                LobbyChanged?.Invoke(lobby);
                break;
            case StartGame start when Phase == SessionPhase.Lobby:
                if (!_hasMap(start.Setup.MapId, start.MapHash))
                {
                    Leave(new Reject(RejectReason.MapHash, $"There is no map {start.Setup.MapId} ({start.MapHash:x16}) on this computer."));
                    return;
                }

                Start = start;
                Phase = SessionPhase.Loading;
                Started?.Invoke(start);
                break;
            case LoadProgress progress:
                _progress[progress.Player] = progress.Percent;
                break;
            case PlayerLeft left when Phase == SessionPhase.Loading:
                _dropped.Add(left.Player);
                break;
            case Protocol.Messages.AllLoaded when Phase == SessionPhase.Loading:
                Phase = SessionPhase.Running;
                AllLoaded?.Invoke();
                break;
        }
    }

    // We cannot play here: we say so to ourselves and close the connection.
    private void Leave(Reject reject)
    {
        Rejection = reject;
        Phase = SessionPhase.Rejected;
        if (_host is { } host)
        {
            _transport.Disconnect(host);
        }

        Rejected?.Invoke(reject);
    }

    private void Send(NetMessage message)
    {
        if (_host is { } host)
        {
            _transport.Send(host, MessageCodec.Write(message), DeliveryChannel.ReliableOrdered);
        }
    }
}
