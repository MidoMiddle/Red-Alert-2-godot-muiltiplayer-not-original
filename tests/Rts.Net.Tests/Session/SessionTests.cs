using System.Collections.Immutable;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Session;
using Rts.Net.Transport;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Session;

/// <summary>
/// Step 4 of M9 (docs/implementation/M9_network.md, 4.8, 4.9): the handshake and the synchronous start. Every check that fails refuses
/// with its own reason; the people of a match begin tick 0 together once all have loaded, the slow one dropped, and compute the same
/// match. The clients are connected to the host only, as in the direct mode: the host passes on what each says to everyone.
/// </summary>
public sealed class SessionTests
{
    private static readonly NetIdentity Identity = new("0.9.0+abc", 71, 0x0123_4567_89AB_CDEFUL);
    private const string MapId = "test_map";
    private const ulong MapHash = 0xFEED_F00DUL;

    private static readonly LinkConditions Lan = new() { LatencyMs = 20, JitterMs = 10 };

    /// <summary>A host and clients over an in-memory network, each client connected to the host only.</summary>
    private sealed class Star
    {
        public Star(int clients, NetIdentity? clientIdentity = null, bool clientsHaveMap = true, HostSettings? settings = null)
        {
            Network = new SimulatedNetwork(42);
            HostTransport = Network.AddPeer();
            var lobby = new LobbyState(MapId, MapHash, new MatchOptions(FogOfWar: false),
                [new LobbySlot(1, PlayerKind.Human, AiDifficulty.Medium, "Host", "union", "", 0, 1, LobbySlot.RandomStart, true)]);
            Host = new HostSession(HostTransport, Identity, lobby, new PlayerId(1), settings);
            for (var i = 0; i < clients; i++)
            {
                var transport = Network.AddPeer();
                ClientTransports.Add(transport);
                Clients.Add(new ClientSession(transport, clientIdentity ?? Identity, $"Player {i + 2}",
                    (id, hash) => clientsHaveMap && id == MapId && hash == MapHash));
                Network.Connect(HostTransport, transport, Lan, Lan);
            }
        }

        public SimulatedNetwork Network { get; }

        public SimulatedTransport HostTransport { get; }

        public HostSession Host { get; }

        public List<SimulatedTransport> ClientTransports { get; } = new();

        public List<ClientSession> Clients { get; } = new();

        public void Run(long milliseconds, Action? everyMs = null)
        {
            for (var ms = 0; ms < milliseconds; ms++)
            {
                Network.Advance(1);
                Host.Update(1);
                foreach (var client in Clients)
                {
                    client.Update();
                }

                everyMs?.Invoke();
            }
        }
    }

    [Fact]
    public void A_client_of_the_same_build_takes_the_next_seat_and_sees_the_lobby()
    {
        var star = new Star(2);
        star.Run(200);

        Assert.All(star.Clients, client => Assert.Equal(SessionPhase.Lobby, client.Phase));
        Assert.Equal([2, 3], star.Clients.Select(client => client.Player!.Value.Value));
        Assert.Equal([1, 2, 3], star.Host.Lobby.Slots.Select(slot => slot.Slot));
        Assert.All(star.Clients, client => Assert.Equal(star.Host.Lobby, client.Lobby));
        Assert.Equal("Player 3", star.Host.Lobby.Slots[2].Name);

        // A choice of a client is the host's lobby and everyone's.
        star.Clients[0].ChangeSlot(new LobbyChange("alliance", "usa", 4, 2, 1, Ready: true));
        star.Run(200);
        var changed = star.Host.Lobby.Slots[1];
        Assert.Equal(("alliance", "usa", 4, 2, 1, true), (changed.Faction, changed.Country, changed.Color, changed.Team, changed.StartIndex, changed.Ready));
        Assert.All(star.Clients, client => Assert.Equal(star.Host.Lobby, client.Lobby));

        // One who leaves frees the seat.
        star.ClientTransports[1].Disconnect(star.ClientTransports[1].Peers[0]);
        star.Run(200);
        Assert.Equal([1, 2], star.Host.Lobby.Slots.Select(slot => slot.Slot));
        Assert.Equal(star.Host.Lobby, star.Clients[0].Lobby);
    }

    [Theory]
    [InlineData("0.9.1+def", 71u, 0x0123_4567_89AB_CDEFUL, RejectReason.GameVersion)]
    [InlineData("0.9.0+abc", 70u, 0x0123_4567_89AB_CDEFUL, RejectReason.SimVersion)]
    [InlineData("0.9.0+abc", 71u, 0x0000_0000_0000_0001UL, RejectReason.DataHash)]
    public void Every_check_of_the_build_refuses_with_its_own_reason(string game, uint sim, ulong data, RejectReason reason)
    {
        var star = new Star(1, new NetIdentity(game, sim, data));
        var refused = new List<Reject>();
        star.Host.Refused += (_, reject) => refused.Add(reject);
        star.Run(200);

        var client = star.Clients[0];
        Assert.Equal(SessionPhase.Rejected, client.Phase);
        Assert.Equal(reason, client.Rejection!.Reason);
        Assert.Equal(reason, Assert.Single(refused).Reason);
        Assert.Empty(star.HostTransport.Peers);
        Assert.Single(star.Host.Lobby.Slots);
    }

    [Fact]
    public void A_client_of_another_protocol_is_refused_with_the_protocol_reason()
    {
        // The Hello of a client one protocol version ahead: the host cannot read it, and says why.
        var star = new Star(0);
        var stranger = star.Network.AddPeer();
        star.Network.Connect(star.HostTransport, stranger, Lan, Lan);
        star.Run(50);
        var hello = MessageCodec.Write(Identity.Hello("Future"));
        BitConverter.TryWriteBytes(hello.AsSpan(1, 2), (ushort)(NetProtocol.Version + 1));
        stranger.SendToAll(hello, DeliveryChannel.ReliableOrdered);
        star.Run(1200);

        stranger.Poll();
        var messages = new List<NetMessage>();
        while (stranger.TryReceive(out var received))
        {
            if (received.Kind == TransportEventKind.Message)
            {
                messages.Add(MessageCodec.Read(received.Data));
            }
        }

        Assert.Equal(RejectReason.ProtocolVersion, Assert.IsType<Reject>(Assert.Single(messages)).Reason);
        Assert.Empty(star.HostTransport.Peers);
    }

    [Fact]
    public void A_client_without_the_map_leaves_the_lobby_with_the_map_reason()
    {
        var star = new Star(1, clientsHaveMap: false);
        star.Run(300);

        var client = star.Clients[0];
        Assert.Equal(SessionPhase.Rejected, client.Phase);
        Assert.Equal(RejectReason.MapHash, client.Rejection!.Reason);
        Assert.Empty(star.HostTransport.Peers);
        Assert.Single(star.Host.Lobby.Slots);
    }

    [Fact]
    public void A_full_room_and_a_started_match_refuse_newcomers()
    {
        var full = new Star(2, settings: new HostSettings { MaxPlayers = 2 });
        full.Run(300);
        Assert.Equal(SessionPhase.Lobby, full.Clients[0].Phase);
        Assert.Equal(RejectReason.RoomFull, full.Clients[1].Rejection!.Reason);

        var started = new Star(1);
        started.Run(200);
        started.Host.Start(Start(started.Host, 2));
        var lateTransport = started.Network.AddPeer();
        var late = new ClientSession(lateTransport, Identity, "Late", (_, _) => true);
        started.Clients.Add(late);
        started.Network.Connect(started.HostTransport, lateTransport, Lan, Lan);
        started.Run(200);
        Assert.Equal(RejectReason.GameInProgress, late.Rejection!.Reason);
    }

    [Fact]
    public void Everyone_begins_tick_zero_together_once_all_have_loaded_and_computes_the_same_match()
    {
        var star = new Star(2);
        star.Run(200);
        var start = Start(star.Host, 3);
        star.Host.Start(start);
        star.Run(100);
        Assert.All(star.Clients, client => Assert.Equal(SessionPhase.Loading, client.Phase));
        Assert.Equal(start, star.Clients[1].Start);

        // Loading takes each its own time; the host and the first client are long done before the second.
        star.Host.ReportLoaded(100);
        star.Clients[0].ReportLoaded(50);
        star.Run(500);
        star.Clients[0].ReportLoaded(100);
        star.Run(2000);
        Assert.Equal(SessionPhase.Loading, star.Host.Phase);
        Assert.All(star.Clients, client => Assert.Equal(SessionPhase.Loading, client.Phase));
        Assert.Equal(100, star.Clients[1].Progress[new PlayerId(2)]);

        star.Clients[1].ReportLoaded(100);
        var match = new NetMatch(star, start);
        star.Run(4000, match.Frame);

        Assert.Equal(SessionPhase.Running, star.Host.Phase);
        Assert.All(match.Computers, computer => Assert.True(computer.Engine.CurrentTick > 40, $"tick {computer.Engine.CurrentTick}"));
        // Nobody began before the last one had loaded: the first tick of each came after the release.
        Assert.All(match.Computers, computer => Assert.True(computer.FirstTickAtMs >= match.ReleasedAtMs, $"{computer.FirstTickAtMs} < {match.ReleasedAtMs}"));
        // The same match everywhere: every checksum the computers share is the same, and each one gave its commands through the host.
        var shared = match.Computers.Select(computer => (IEnumerable<int>)computer.Checksums.Keys).Aggregate((a, b) => a.Intersect(b)).ToList();
        Assert.True(shared.Count >= 2);
        Assert.All(shared, tick => Assert.Single(match.Computers.Select(computer => computer.Checksums[tick]).Distinct()));
        Assert.All(match.Computers, computer => Assert.Equal(3, computer.Simulation.ExecutedCommands.Select(command => command.Player).Distinct().Count()));
    }

    [Fact]
    public void One_who_does_not_load_in_time_is_dropped_and_the_others_begin_without_him()
    {
        var star = new Star(2, settings: new HostSettings { LoadTimeoutMs = 3000 });
        star.Run(200);
        var start = Start(star.Host, 3);
        star.Host.Start(start);
        star.Run(100);
        star.Host.ReportLoaded(100);
        star.Clients[0].ReportLoaded(100);
        star.Run(3500);

        Assert.Equal(SessionPhase.Running, star.Host.Phase);
        Assert.Equal(SessionPhase.Running, star.Clients[0].Phase);
        Assert.Equal(SessionPhase.Rejected, star.Clients[1].Phase);
        Assert.Equal(RejectReason.LoadTimeout, star.Clients[1].Rejection!.Reason);
        Assert.Equal([new PlayerId(3)], star.Host.Dropped);
        Assert.Equal([new PlayerId(3)], star.Clients[0].Dropped);
        Assert.Equal([new PlayerId(1), new PlayerId(2)], star.Host.ActivePlayers);
        Assert.Equal(star.Host.ActivePlayers, star.Clients[0].ActivePlayers);
    }

    /// <summary>The start of a match of every slot of the host's lobby on a 64 × 64 map, everyone of the Union.</summary>
    private static StartGame Start(HostSession host, int players)
    {
        var starts = ImmutableArray.Create(new CellPos(10, 10), new CellPos(50, 50), new CellPos(10, 50), new CellPos(50, 10));
        var setup = new MatchSetup(MapId, 77, new MatchOptions(FogOfWar: false),
            host.Lobby.Slots.Take(players).Select(slot => new MatchPlayer(slot.Slot, slot.Kind, slot.Difficulty, "union", slot.Color, slot.Slot, slot.Slot - 1, starts[slot.Slot - 1])).ToImmutableArray(),
            starts);
        return new StartGame(setup, 15, 15, MapHash, 3);
    }

    /// <summary>The match of the start on every computer: built when its session lets go, its lockstep then run frame by frame.</summary>
    private sealed class NetMatch
    {
        private readonly Star _star;
        private readonly StartGame _start;

        public NetMatch(Star star, StartGame start)
        {
            _star = star;
            _start = start;
            star.Host.AllLoaded += () =>
            {
                ReleasedAtMs = star.Network.NowMs;
                Computers.Add(new Computer(this, new HostRelay(star.HostTransport), star.Host.Host, star.Host.ActivePlayers));
            };
            for (var i = 0; i < star.Clients.Count; i++)
            {
                var client = star.Clients[i];
                var transport = star.ClientTransports[i];
                client.AllLoaded += () => Computers.Add(new Computer(this, transport, client.Player!.Value, client.ActivePlayers));
            }
        }

        public long ReleasedAtMs { get; private set; } = long.MaxValue;

        public List<Computer> Computers { get; } = new();

        public long NowMs => _star.Network.NowMs;

        public void Frame()
        {
            if (NowMs % 16 != 0)
            {
                return;
            }

            foreach (var computer in Computers.ToList())
            {
                computer.Frame(16);
            }
        }

        public sealed class Computer
        {
            private readonly NetMatch _match;
            private bool _ordered;

            public Computer(NetMatch match, ITransport transport, PlayerId player, IReadOnlyList<PlayerId> players)
            {
                _match = match;
                Player = player;
                Simulation = new GameSimulation(SimulationSettings.ForMatch(match._start.Setup, new MapGrid(64, 64)), TestRules.Standard);
                Engine = new LockstepEngine(Simulation, transport, player, players, new LockstepSettings { InputDelay = match._start.InputDelay });
                Engine.Stepped += tick =>
                {
                    FirstTickAtMs = Math.Min(FirstTickAtMs, _match.NowMs);
                    if (Simulation.LastChecksum is { } checksum && checksum.Tick == Simulation.CurrentTick)
                    {
                        Checksums[checksum.Tick] = checksum.Value;
                    }
                };
                Engine.Start();
            }

            public PlayerId Player { get; }

            public GameSimulation Simulation { get; }

            public LockstepEngine Engine { get; }

            public long FirstTickAtMs { get; private set; } = long.MaxValue;

            public SortedDictionary<int, ulong> Checksums { get; } = new();

            public void Frame(long elapsedMs)
            {
                // Each player sends its units somewhere once the match is under way.
                if (!_ordered && Engine.CurrentTick >= 10)
                {
                    var units = Simulation.World.Entities.Where(entity => entity is Unit && entity.Owner == Player).Select(entity => entity.Id).ToImmutableArray();
                    Engine.Issue(new MoveCommand(Player, 0, 0, units, new CellPos(32, 32).Center, false));
                    _ordered = true;
                }

                Engine.Update(elapsedMs);
            }
        }
    }
}
