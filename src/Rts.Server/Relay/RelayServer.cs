using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;

namespace Rts.Server.Relay;

/// <summary>The limits of the relay (docs/implementation/M9_network.md, 4.14).</summary>
public sealed record RelaySettings
{
    /// <summary>The most members of a room: the most players of a map.</summary>
    public int MaxMembers { get; init; } = 8;

    /// <summary>The most rooms open at once from one address.</summary>
    public int MaxRoomsPerAddress { get; init; } = 4;

    /// <summary>The most rooms open at once.</summary>
    public int MaxRooms { get; init; } = 1000;

    /// <summary>
    /// The most messages a second from one connection; what comes above it is dropped. A player of a match sends about 80 — 15 bundles
    /// and their 30 copies, 15 checksums, 4 pings and the answers to the others' — the host the same again for every client it passes
    /// the others' messages to.
    /// </summary>
    public int MessagesPerSecond { get; init; } = 600;

    /// <summary>A connection that has not asked for a room in this long is closed.</summary>
    public int JoinTimeoutMs { get; init; } = 10_000;

    /// <summary>How long a refused connection stays open, for the refusal to reach it.</summary>
    public int RefusalLingerMs { get; init; } = 1000;
}

/// <summary>
/// The relay of <c>Rts.Server</c> (docs/implementation/M9_network.md, 4.3, 4.14, step 12): rooms of players who cannot reach each other
/// directly. It computes nothing of the match and knows nothing of its rules; it checks that a message is one of the protocol, that its
/// sender is in a room, and how often it speaks.
/// <list type="bullet">
/// <item>A connection asks for a room (<see cref="RoomRequest"/>): an empty code opens a new one — it hosts its lobby and is member 1 —
/// a code joins that room as the next member. The answer is <see cref="RoomJoined"/>, or <see cref="Reject"/>: no such room
/// (<see cref="RejectReason.RoomNotFound"/>), the room full (<see cref="RejectReason.RoomFull"/>), too many rooms
/// (<see cref="RejectReason.ServerBusy"/>).</item>
/// <item>In a room it is the star of the direct mode: a client speaks to the host only, the host to one client or to all
/// (<see cref="Relayed"/>, on the channel it came on). The host hears of every client coming and going (<see cref="RoomMember"/>) and
/// may put one out; a client hears of the host leaving — then the room closes, for the match has no host.</item>
/// </list>
/// The server program calls <see cref="Update"/> in a loop; the tests run it over the in-memory network.
/// </summary>
public sealed class RelayServer
{
    /// <summary>The letters of a room code: no 0 and O, no 1 and I.</summary>
    public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const int CodeLength = 6;

    private readonly ITransport _transport;
    private readonly Func<PeerId, string> _addressOf;
    private readonly Random _random;
    private readonly Action<string> _log;
    private readonly Dictionary<PeerId, Connection> _connections = new();
    private readonly Dictionary<string, Room> _rooms = new();
    private readonly List<(PeerId Peer, long At)> _closing = new();
    private long _now;

    public RelayServer(ITransport transport, RelaySettings? settings = null, Func<PeerId, string>? addressOf = null, Random? random = null, Action<string>? log = null)
    {
        _transport = transport;
        Settings = settings ?? new RelaySettings();
        _addressOf = addressOf ?? (_ => "");
        _random = random ?? new Random();
        _log = log ?? (_ => { });
    }

    public RelaySettings Settings { get; }

    /// <summary>The codes of the rooms open now.</summary>
    public IReadOnlyCollection<string> Rooms => _rooms.Keys;

    /// <summary>The members of a room now, by number; empty for no such room.</summary>
    public IReadOnlyList<int> MembersOf(string code) =>
        _rooms.TryGetValue(code, out var room) ? room.Members.Keys.Order().ToList() : [];

    /// <summary>Messages passed on from one member to another.</summary>
    public long RelayedMessages { get; private set; }

    /// <summary>Messages dropped: over the limit of their connection, not of the protocol, or to nobody they may speak to.</summary>
    public long Dropped { get; private set; }

    /// <summary>Takes what came from the network and passes it on; closes the connections whose time is up.</summary>
    public void Update(long elapsedMs)
    {
        _now += elapsedMs;
        _transport.Poll();
        while (_transport.TryReceive(out var received))
        {
            switch (received.Kind)
            {
                case TransportEventKind.Connected:
                    _connections[received.Peer] = new Connection(received.Peer, _addressOf(received.Peer), _now, Settings.MessagesPerSecond);
                    break;
                case TransportEventKind.Disconnected:
                    Leave(received.Peer);
                    break;
                case TransportEventKind.Message when _connections.TryGetValue(received.Peer, out var connection):
                    Handle(connection, received);
                    break;
            }
        }

        foreach (var connection in _connections.Values.Where(connection => connection.Room is null && !connection.Refused
                     && _now - connection.Since >= Settings.JoinTimeoutMs).ToList())
        {
            _log($"{connection.Address}: no room asked for in {Settings.JoinTimeoutMs / 1000} s, closed");
            connection.Refused = true;
            _transport.Disconnect(connection.Peer);
        }

        foreach (var (peer, at) in _closing.Where(closing => closing.At <= _now).ToList())
        {
            _closing.Remove((peer, at));
            _transport.Disconnect(peer);
        }
    }

    private void Handle(Connection connection, TransportEvent received)
    {
        if (!connection.Take(_now, Settings.MessagesPerSecond))
        {
            Dropped++;
            if (connection.DroppedSinceLog++ == 0)
            {
                _log($"{connection.Address}: more than {Settings.MessagesPerSecond} messages a second, dropping");
            }

            return;
        }

        NetMessage message;
        try
        {
            message = MessageCodec.Read(received.Data);
        }
        catch (MessageFormatException error) when (error.Error == MessageError.WrongProtocolVersion && connection.Room is null)
        {
            Refuse(connection, new Reject(RejectReason.ProtocolVersion, $"The relay speaks the network protocol {NetProtocol.Version}, the game {error.ProtocolVersion}."));
            return;
        }
        catch (MessageFormatException)
        {
            Dropped++;
            return;
        }

        switch (message)
        {
            case RoomRequest request when connection.Room is null && !connection.Refused:
                Join(connection, request.Code.Trim().ToUpperInvariant());
                break;
            case Relayed relayed when connection.Room is { } room:
                Pass(room, connection, relayed, received.Channel);
                break;
            case RoomMember { Here: false } member when connection.Room is { } room && connection.Member == 1 && member.Member != 1:
                if (room.Members.TryGetValue(member.Member, out var leaver))
                {
                    _log($"room {room.Code}: the host puts member {member.Member} out");
                    Leave(leaver.Peer);
                    _transport.Disconnect(leaver.Peer);
                }

                break;
            default:
                Dropped++;
                break;
        }
    }

    private void Join(Connection connection, string code)
    {
        if (code == "")
        {
            var mine = _rooms.Values.Count(room => room.Address == connection.Address);
            if (_rooms.Count >= Settings.MaxRooms || mine >= Settings.MaxRoomsPerAddress)
            {
                Refuse(connection, new Reject(RejectReason.ServerBusy, _rooms.Count >= Settings.MaxRooms
                    ? "The relay is full; try later."
                    : $"{mine} rooms are open from this address already."));
                return;
            }

            var room = new Room(NewCode(), connection.Address);
            _rooms.Add(room.Code, room);
            Seat(room, connection);
            _log($"room {room.Code} opened by {connection.Address}");
            return;
        }

        if (!_rooms.TryGetValue(code, out var joined))
        {
            Refuse(connection, new Reject(RejectReason.RoomNotFound, $"There is no room {code}."));
            return;
        }

        if (joined.Members.Count >= Settings.MaxMembers)
        {
            Refuse(connection, new Reject(RejectReason.RoomFull, $"All {Settings.MaxMembers} places of room {code} are taken."));
            return;
        }

        Seat(joined, connection);
        Send(joined.Host, new RoomMember(connection.Member, true));
        _log($"room {code}: member {connection.Member} from {connection.Address}");
    }

    private void Seat(Room room, Connection connection)
    {
        connection.Room = room;
        connection.Member = room.NextMember++;
        room.Members.Add(connection.Member, connection);
        Send(connection, new RoomJoined(room.Code, connection.Member));
    }

    // The star: a client to the host, the host to one client or all of them.
    private void Pass(Room room, Connection from, Relayed relayed, DeliveryChannel channel)
    {
        try
        {
            MessageCodec.Read(relayed.Payload);
        }
        catch (MessageFormatException)
        {
            Dropped++;
            return;
        }

        IEnumerable<Connection> to = from.Member == 1
            ? relayed.Member == 0 ? room.Members.Values.Where(member => member != from) : room.Members.TryGetValue(relayed.Member, out var one) && one != from ? [one] : []
            : relayed.Member is 0 or 1 ? [room.Host] : [];
        var frame = Relayed.Frame(from.Member, relayed.Payload);
        var any = false;
        foreach (var member in to)
        {
            _transport.Send(member.Peer, frame, channel);
            RelayedMessages++;
            any = true;
        }

        if (!any)
        {
            Dropped++;
        }
    }

    private void Leave(PeerId peer)
    {
        if (!_connections.Remove(peer, out var connection) || connection.Room is not { } room)
        {
            return;
        }

        room.Members.Remove(connection.Member);
        if (connection.Member != 1)
        {
            Send(room.Host, new RoomMember(connection.Member, false));
            _log($"room {room.Code}: member {connection.Member} left");
            return;
        }

        // Without its host the match cannot go on (4.11): everyone hears of it and the room closes.
        _rooms.Remove(room.Code);
        foreach (var member in room.Members.Values.ToList())
        {
            Send(member, new RoomMember(1, false));
            member.Room = null;
            member.Refused = true;
            _closing.Add((member.Peer, _now + Settings.RefusalLingerMs));
        }

        _log($"room {room.Code} closed: the host left");
    }

    private void Refuse(Connection connection, Reject reject)
    {
        connection.Refused = true;
        Send(connection, reject);
        _closing.Add((connection.Peer, _now + Settings.RefusalLingerMs));
        _log($"{connection.Address}: refused, {reject.Reason} — {reject.Detail}");
    }

    private void Send(Connection connection, NetMessage message) =>
        _transport.Send(connection.Peer, MessageCodec.Write(message), DeliveryChannel.ReliableOrdered);

    private string NewCode()
    {
        while (true)
        {
            var code = new string(Enumerable.Range(0, CodeLength).Select(_ => CodeAlphabet[_random.Next(CodeAlphabet.Length)]).ToArray());
            if (!_rooms.ContainsKey(code))
            {
                return code;
            }
        }
    }

    private sealed class Room
    {
        public Room(string code, string address)
        {
            Code = code;
            Address = address;
        }

        public string Code { get; }

        /// <summary>Where the host connects from: the rooms of an address are counted.</summary>
        public string Address { get; }

        public SortedDictionary<int, Connection> Members { get; } = new();

        public int NextMember { get; set; } = 1;

        public Connection Host => Members[1];
    }

    private sealed class Connection
    {
        private double _tokens;
        private long _filledAt;

        public Connection(PeerId peer, string address, long since, int perSecond)
        {
            Peer = peer;
            Address = address;
            Since = since;
            _tokens = perSecond;
            _filledAt = since;
        }

        public PeerId Peer { get; }

        public string Address { get; }

        public long Since { get; }

        public Room? Room { get; set; }

        public int Member { get; set; }

        /// <summary>Refused or its room closed: it only waits to be disconnected.</summary>
        public bool Refused { get; set; }

        public int DroppedSinceLog { get; set; }

        /// <summary>One message more, if its second allows: a bucket of a second's messages that fills up as time goes.</summary>
        public bool Take(long now, int perSecond)
        {
            _tokens = Math.Min(perSecond, _tokens + (now - _filledAt) * perSecond / 1000.0);
            _filledAt = now;
            if (_tokens < 1)
            {
                return false;
            }

            _tokens--;
            if (_tokens >= perSecond / 2.0)
            {
                DroppedSinceLog = 0;
            }

            return true;
        }
    }
}
