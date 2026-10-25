using System.Collections.Immutable;
using Rts.Net.Desync;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Session;
using Rts.Net.Transport;
using Rts.Server.Relay;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.Server.Tests;

/// <summary>
/// Step 12 of M9 (docs/implementation/M9_network.md, 4.14): the rooms of the relay — their codes, who may join, the limits — what it
/// passes on and to whom, and a whole match of three through it: lobby, start, loading, lockstep, one leaving, the same checksums.
/// Over the in-memory network, every player connected to the relay only.
/// </summary>
public sealed class RelayServerTests
{
    private static readonly LinkConditions Internet = new() { LatencyMs = 40, JitterMs = 15 };
    private static readonly NetIdentity Identity = new("0.9.0+abc", 72, 0x0123_4567_89AB_CDEFUL);
    private const string MapId = "test_map";
    private const ulong MapHash = 0xFEED_F00DUL;

    [Fact]
    public void One_opens_a_room_and_hosts_it_others_join_it_by_its_code()
    {
        var relay = new Relay();
        var host = relay.Player("10.0.0.1");
        relay.Run(200);

        var code = host.RoomCode!;
        Assert.Equal(RelayServer.CodeLength, code.Length);
        Assert.All(code, letter => Assert.Contains(letter, RelayServer.CodeAlphabet));
        Assert.True(host.IsHost);
        Assert.Empty(host.Peers);

        // In lower case too; the host sees each come, a client sees only the host.
        var first = relay.Player("10.0.0.2", code.ToLowerInvariant());
        relay.Run(100);
        var second = relay.Player("10.0.0.3", code);
        relay.Run(200);

        Assert.Equal([2, 3], host.Peers.Select(peer => peer.Value));
        Assert.Equal([RelayTransport.HostMember], first.Peers);
        Assert.Equal((code, 3), (second.RoomCode, second.Member));
        Assert.Equal([1, 2, 3], relay.Server.MembersOf(code));
    }

    [Fact]
    public void Codes_are_different_and_have_no_letters_that_look_alike()
    {
        var relay = new Relay(new RelaySettings { MaxRoomsPerAddress = 100 });
        var hosts = Enumerable.Range(0, 60).Select(_ => relay.Player("10.0.0.1")).ToList();
        relay.Run(200);

        Assert.Equal(60, hosts.Select(host => host.RoomCode).Distinct().Count());
        Assert.DoesNotContain(string.Concat(hosts.Select(host => host.RoomCode)), letter => letter is '0' or 'O' or '1' or 'I');
    }

    [Fact]
    public void The_relay_refuses_an_unknown_room_a_full_one_and_too_many_rooms_of_one_address()
    {
        var relay = new Relay(new RelaySettings { MaxMembers = 2, MaxRoomsPerAddress = 2 });
        var host = relay.Player("10.0.0.1");
        relay.Player("10.0.0.1");
        relay.Run(200);
        var third = relay.Player("10.0.0.1");
        var nowhere = relay.Player("10.0.0.2", "ZZZZZZ");
        relay.Player("10.0.0.3", host.RoomCode!);
        relay.Run(200);
        var late = relay.Player("10.0.0.4", host.RoomCode!);
        relay.Run(1500);

        Assert.Equal(RejectReason.ServerBusy, third.Rejection!.Reason);
        Assert.Equal(RejectReason.RoomNotFound, nowhere.Rejection!.Reason);
        Assert.Equal(RejectReason.RoomFull, late.Rejection!.Reason);
        Assert.All([third, nowhere, late], refused => Assert.True(refused.Closed));
        Assert.Equal(2, relay.Server.Rooms.Count);
    }

    [Fact]
    public void A_refusal_reaches_a_joining_session_as_the_hosts()
    {
        var relay = new Relay();
        var transport = relay.Player("10.0.0.2", "ABCDEF");
        var session = new ClientSession(transport, Identity, "Guest", (_, _) => true, "ABCDEF");
        relay.Run(500, session.Update);

        Assert.Equal(SessionPhase.Rejected, session.Phase);
        Assert.Equal(RejectReason.RoomNotFound, session.Rejection!.Reason);
    }

    [Fact]
    public void Messages_go_within_a_room_only_and_a_client_speaks_to_the_host_only()
    {
        var relay = new Relay();
        var a = relay.Player("10.0.0.1");
        var b = relay.Player("10.0.0.2");
        relay.Run(200);
        var a2 = relay.Player("10.0.0.3", a.RoomCode!);
        relay.Run(100);
        var a3 = relay.Player("10.0.0.4", a.RoomCode!);
        var b2 = relay.Player("10.0.0.5", b.RoomCode!);
        relay.Run(200);
        relay.DrainAll();

        a.SendToAll(MessageCodec.Write(new Ping(1)), DeliveryChannel.ReliableOrdered);
        a2.SendToAll(MessageCodec.Write(new Ping(2)), DeliveryChannel.Unreliable);
        a.Send(new PeerId(3), MessageCodec.Write(new Ping(3)), DeliveryChannel.ReliableOrdered);
        relay.Run(200);

        Assert.Equal([(1, 1L), (1, 3L)], relay.Received(a3));
        Assert.Equal([(1, 1L)], relay.Received(a2));
        Assert.Equal([(2, 2L)], relay.Received(a));
        Assert.Empty(relay.Received(b));
        Assert.Empty(relay.Received(b2));

        // A client addressing another client directly: dropped.
        relay.DrainAll();
        relay.Raw(a2).Send(relay.Raw(a2).Peers[0], Relayed.Frame(3, MessageCodec.Write(new Ping(4))), DeliveryChannel.ReliableOrdered);
        relay.Run(200);
        Assert.Empty(relay.Received(a3));
    }

    [Fact]
    public void What_is_not_a_message_of_the_protocol_is_dropped()
    {
        var relay = new Relay();
        var host = relay.Player("10.0.0.1");
        relay.Run(200);
        var client = relay.Player("10.0.0.2", host.RoomCode!);
        relay.Run(200);
        relay.DrainAll();
        var dropped = relay.Server.Dropped;

        client.SendToAll([1, 2, 3, 4], DeliveryChannel.ReliableOrdered);
        relay.Raw(client).SendToAll([200, 9, 0], DeliveryChannel.ReliableOrdered);
        relay.Run(200);

        Assert.Empty(relay.Received(host));
        Assert.Equal(dropped + 2, relay.Server.Dropped);
    }

    [Fact]
    public void One_who_speaks_too_often_is_cut_to_the_limit()
    {
        var relay = new Relay(new RelaySettings { MessagesPerSecond = 100 });
        var host = relay.Player("10.0.0.1");
        relay.Run(200);
        var client = relay.Player("10.0.0.2", host.RoomCode!);
        relay.Run(1200);
        relay.DrainAll();

        for (var i = 0; i < 500; i++)
        {
            client.SendToAll(MessageCodec.Write(new Ping(i)), DeliveryChannel.ReliableOrdered);
        }

        relay.Run(500);

        // A second's worth at once, and then what the half second gave.
        var passed = relay.Received(host).Count;
        Assert.InRange(passed, 100, 160);
        Assert.True(relay.Server.Dropped >= 340);
    }

    [Fact]
    public void The_host_leaving_closes_the_room_and_a_client_leaving_is_told_to_the_host()
    {
        var relay = new Relay();
        var host = relay.Player("10.0.0.1");
        relay.Run(200);
        var stays = relay.Player("10.0.0.2", host.RoomCode!);
        relay.Run(100);
        var goes = relay.Player("10.0.0.3", host.RoomCode!);
        relay.Run(200);
        relay.DrainAll();

        goes.Disconnect(RelayTransport.HostMember);
        relay.Run(200);
        Assert.Equal([2], host.Peers.Select(peer => peer.Value));
        Assert.Contains(relay.Events(host), e => e is { Kind: TransportEventKind.Disconnected, Peer.Value: 3 });

        relay.Raw(host).Disconnect(relay.Raw(host).Peers[0]);
        relay.Run(1500);
        Assert.Empty(stays.Peers);
        Assert.Contains(relay.Events(stays), e => e is { Kind: TransportEventKind.Disconnected, Peer.Value: 1 });
        Assert.Empty(relay.Server.Rooms);
        Assert.True(stays.Closed);
    }

    [Fact]
    public void The_host_puts_a_member_out()
    {
        var relay = new Relay();
        var host = relay.Player("10.0.0.1");
        relay.Run(200);
        var client = relay.Player("10.0.0.2", host.RoomCode!);
        relay.Run(200);
        relay.DrainAll();

        host.Disconnect(new PeerId(2));
        relay.Run(300);

        Assert.Empty(host.Peers);
        Assert.Contains(relay.Events(host), e => e is { Kind: TransportEventKind.Disconnected, Peer.Value: 2 });
        Assert.True(client.Closed);
        Assert.Equal([1], relay.Server.MembersOf(host.RoomCode!));
    }

    [Fact]
    public void A_connection_that_asks_for_no_room_is_closed()
    {
        var relay = new Relay(new RelaySettings { JoinTimeoutMs = 1000 });
        var silent = relay.Network.AddPeer();
        relay.Network.Connect(relay.ServerTransport, silent, Internet, Internet);
        relay.Run(1200);

        Assert.Empty(silent.Peers);
    }

    [Fact]
    public void A_match_of_three_goes_through_the_relay_as_through_a_host_and_one_who_leaves_leaves_on_one_tick()
    {
        var relay = new Relay();
        var hostTransport = relay.Player("10.0.0.1");
        relay.Run(300);
        var lobby = new LobbyState(MapId, MapHash, new MatchOptions(FogOfWar: false),
            [new LobbySlot(1, PlayerKind.Human, AiDifficulty.Medium, "Host", "union", "", 0, 1, LobbySlot.RandomStart, true)]);
        var host = new HostSession(hostTransport, Identity, lobby, new PlayerId(1));
        var clientTransports = new[] { relay.Player("10.0.0.2", hostTransport.RoomCode!), relay.Player("10.0.0.3", hostTransport.RoomCode!) };
        // The seats go in the order the clients come, which the network decides.
        var clients = clientTransports.Select((transport, i) => new ClientSession(transport, Identity, $"Player {i + 2}", (id, hash) => id == MapId && hash == MapHash, hostTransport.RoomCode!)).ToList();
        void Sessions()
        {
            host.Update(1);
            clients.ForEach(client => client.Update());
        }

        relay.Run(500, Sessions);
        Assert.All(clients, client => Assert.Equal(SessionPhase.Lobby, client.Phase));
        Assert.Equal(["Host", "Player 2", "Player 3"], host.Lobby.Slots.Select(slot => slot.Name).Order());

        var start = Start(host);
        host.Start(start);
        relay.Run(300, Sessions);
        var computers = new List<Computer>();
        host.AllLoaded += () => computers.Add(new Computer(new HostRelay(hostTransport), new PlayerId(1), host.ActivePlayers, start,
            host.Seats.ToDictionary(seat => seat.Key, seat => new PlayerId(seat.Value))));
        for (var i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            var transport = clientTransports[i];
            client.AllLoaded += () => computers.Add(new Computer(transport, client.Player!.Value, client.ActivePlayers, start, null));
        }

        host.ReportLoaded(100);
        clients.ForEach(client => client.ReportLoaded(100));
        relay.Run(4000, () =>
        {
            Sessions();
            if (relay.Network.NowMs % 16 == 0)
            {
                computers.ForEach(computer => computer.Frame(16));
            }
        });

        Assert.Equal(3, computers.Count);
        Assert.All(computers, computer => Assert.True(computer.Engine.CurrentTick > 30, $"tick {computer.Engine.CurrentTick}"));
        Assert.All(computers, computer => Assert.Equal(3, computer.Simulation.ExecutedCommands.Select(command => command.Player).Distinct().Count()));

        // Player 3 goes: out on one tick at the host and the other client, and the match goes on the same.
        var leaver = computers.Single(computer => computer.Engine.LocalPlayer.Value == 3);
        var leaving = clientTransports[clients.FindIndex(client => client.Player!.Value.Value == 3)];
        relay.Raw(leaving).Disconnect(relay.Raw(leaving).Peers[0]);
        computers.Remove(leaver);
        relay.Run(3000, () =>
        {
            if (relay.Network.NowMs % 16 == 0)
            {
                computers.ForEach(computer => computer.Frame(16));
            }
        });

        var left = computers.Select(computer => computer.Simulation.ExecutedCommands.OfType<PlayerLeftCommand>().Single()).ToList();
        Assert.Equal(left[0], left[1]);
        Assert.All(computers, computer => Assert.True(computer.Engine.CurrentTick > left[0].ExecuteTick + 20));
        var sums = computers.Select(computer => computer.Checksums.OwnChecksums.ToDictionary(sum => sum.Tick, sum => sum.Value)).ToList();
        var shared = sums[0].Keys.Intersect(sums[1].Keys).ToList();
        Assert.True(shared.Count > 4);
        Assert.All(shared, tick => Assert.Equal(sums[0][tick], sums[1][tick]));
        Assert.All(computers, computer => Assert.Null(computer.Checksums.Detector.Found));
    }

    private static StartGame Start(HostSession host)
    {
        var starts = ImmutableArray.Create(new CellPos(10, 10), new CellPos(50, 50), new CellPos(10, 50));
        var setup = new MatchSetup(MapId, 77, new MatchOptions(FogOfWar: false),
            host.Lobby.Slots.Select(slot => new MatchPlayer(slot.Slot, slot.Kind, slot.Difficulty, "union", slot.Color, slot.Slot, slot.Slot - 1, starts[slot.Slot - 1])).ToImmutableArray(),
            starts);
        return new StartGame(setup, 15, 15, MapHash, 3);
    }

    /// <summary>The relay and the players connected to it, each with its own address.</summary>
    private sealed class Relay
    {
        private readonly Dictionary<PeerId, string> _addresses = new();
        private readonly Dictionary<RelayTransport, SimulatedTransport> _raw = new();
        private readonly Dictionary<RelayTransport, List<TransportEvent>> _events = new();

        public Relay(RelaySettings? settings = null)
        {
            Network = new SimulatedNetwork(12);
            ServerTransport = Network.AddPeer();
            Server = new RelayServer(ServerTransport, settings, peer => _addresses.GetValueOrDefault(peer, ""), new Random(5));
        }

        public SimulatedNetwork Network { get; }

        public SimulatedTransport ServerTransport { get; }

        public RelayServer Server { get; }

        /// <summary>A player connecting from <paramref name="address"/>: to open a room, or to join <paramref name="code"/>.</summary>
        public RelayTransport Player(string address, string code = "")
        {
            var raw = Network.AddPeer();
            var transport = new RelayTransport(raw, code);
            _raw[transport] = raw;
            _events[transport] = new List<TransportEvent>();
            Network.Connect(ServerTransport, raw, Internet, Internet);
            // The server sees the connection as its peer of the same number the network gave the player.
            _addresses[raw.Id] = address;
            return transport;
        }

        public SimulatedTransport Raw(RelayTransport transport) => _raw[transport];

        /// <summary>Runs the network and the relay millisecond by millisecond; <paramref name="everyMs"/> is the players' turn, else their transports are read.</summary>
        public void Run(long milliseconds, Action? everyMs = null)
        {
            for (var ms = 0; ms < milliseconds; ms++)
            {
                Network.Advance(1);
                Server.Update(1);
                if (everyMs is not null)
                {
                    everyMs();
                    continue;
                }

                foreach (var (transport, events) in _events)
                {
                    transport.Poll();
                    while (transport.TryReceive(out var received))
                    {
                        events.Add(received);
                    }
                }
            }
        }

        public List<TransportEvent> Events(RelayTransport transport) => _events[transport];

        /// <summary>The pings that came to <paramref name="transport"/>: from which member, with which stamp.</summary>
        public List<(int, long)> Received(RelayTransport transport) =>
            _events[transport].Where(e => e.Kind == TransportEventKind.Message).Select(e => (e.Peer.Value, ((Ping)MessageCodec.Read(e.Data)).Timestamp)).ToList();

        public void DrainAll()
        {
            Run(1);
            foreach (var events in _events.Values)
            {
                events.Clear();
            }
        }
    }

    /// <summary>One computer of the match through the relay: its simulation and lockstep, and on the host the referee.</summary>
    private sealed class Computer
    {
        private readonly MatchReferee? _referee;
        private bool _ordered;

        public Computer(ITransport transport, PlayerId player, IReadOnlyList<PlayerId> players, StartGame start, Dictionary<PeerId, PlayerId>? seats)
        {
            Simulation = new GameSimulation(SimulationSettings.ForMatch(start.Setup, new MapGrid(64, 64)), TestRules.Standard);
            Engine = new LockstepEngine(Simulation, transport, player, players, new LockstepSettings { InputDelay = start.InputDelay });
            Checksums = new ChecksumExchange(Engine, Simulation, transport, players);
            _referee = seats is null ? null : new MatchReferee(Engine, transport, seats);
            Engine.Start();
        }

        public GameSimulation Simulation { get; }

        public LockstepEngine Engine { get; }

        public ChecksumExchange Checksums { get; }

        public void Frame(long elapsedMs)
        {
            if (!_ordered && Engine.CurrentTick >= 10)
            {
                var units = Simulation.World.Entities.Where(entity => entity is Unit && entity.Owner == Engine.LocalPlayer).Select(entity => entity.Id).ToImmutableArray();
                Engine.Issue(new MoveCommand(Engine.LocalPlayer, 0, 0, units, new CellPos(32, 32).Center, false));
                _ordered = true;
            }

            _referee?.Update(elapsedMs);
            Engine.Update(elapsedMs);
        }
    }
}
