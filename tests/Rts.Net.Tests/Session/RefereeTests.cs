using System.Collections.Immutable;
using Rts.Net.Desync;
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
using Rts.Simulation.Replays;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Session;

/// <summary>
/// Step 10 of M9 (docs/implementation/M9_network.md, 4.11, 4.12): a player who leaves — the connection lost, silent too long, voted
/// out — is out on the same tick on every computer, and the match goes on without it; a pause stops everyone on the same tick, as many
/// times and as long as the rules let. Three computers of a match, the clients connected to the host only, as in the direct mode.
/// Step 11 (4.13): the replay every computer records plays back on one computer with the same checksums.
/// </summary>
public sealed class RefereeTests
{
    private static readonly LinkConditions Lan = new() { LatencyMs = 20, JitterMs = 10 };
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);
    private static readonly PlayerId P3 = new(3);

    [Fact]
    public void One_whose_connection_is_lost_leaves_on_the_same_tick_everywhere_and_the_match_goes_on()
    {
        var match = new Match(new RefereeSettings());
        match.Run(2000);
        var unitsOfP3 = match[0].Simulation.World.Entities.Count(entity => entity is Unit { IsAlive: true } && entity.Owner == P3);
        Assert.True(unitsOfP3 > 0);

        match.Network.Disconnect(match[2].Transport, match[2].Transport.Peers[0]);
        match.Run(3000);

        // Out on one tick, its command executed there by both who stay; the match agrees and goes on.
        var left = match[0].Simulation.ExecutedCommands.OfType<PlayerLeftCommand>().Single();
        Assert.Equal(P3, left.Player);
        Assert.Equal(left, match[1].Simulation.ExecutedCommands.OfType<PlayerLeftCommand>().Single());
        Assert.Equal(LeaveReason.Disconnected, match[1].Status.Left[P3]);
        Assert.Equal(LeaveReason.Disconnected, match[0].Status.Left[P3]);
        Assert.All(match.Computers.Take(2), computer => Assert.True(computer.Engine.CurrentTick > left.ExecuteTick + 30));
        match.AssertSameChecksums(match[0], match[1]);
        Assert.Null(match[0].Checksums.Detector.Found);

        // Defeated, but what is theirs stays, and the match is not over: two players of two teams are left.
        var world = match[1].Simulation.World;
        Assert.Equal(left.ExecuteTick, world.Player(P3).DefeatedTick);
        Assert.Equal(unitsOfP3, world.Entities.Count(entity => entity is Unit { IsAlive: true } && entity.Owner == P3));
        Assert.Null(world.Result);
        Assert.Equal([P1, P2], match[0].Engine.RemainingPlayers());
    }

    [Fact]
    public void One_silent_too_long_is_dropped_and_learns_it()
    {
        var match = new Match(new RefereeSettings { DropAfterMs = 3000 });
        match.Run(1500);
        match[2].Running = false;
        match.Run(2000);

        // Waited for, not yet out.
        Assert.True(match[0].Engine.IsWaitingForPlayers);
        Assert.Equal([P3], match[0].Engine.MissingPlayers());
        Assert.Empty(match[0].Status.Left);

        match.Run(3000);
        var left = match[0].Simulation.ExecutedCommands.OfType<PlayerLeftCommand>().Single();
        Assert.Equal(LeaveReason.Timeout, match[1].Status.Left[P3]);
        Assert.Equal(left, match[1].Simulation.ExecutedCommands.OfType<PlayerLeftCommand>().Single());
        match.AssertSameChecksums(match[0], match[1]);

        // Woken up, the silent one finds itself out: it stops on the tick it left on, and the host has closed its connection.
        match[2].Running = true;
        match.Run(1500);
        Assert.Equal(LeaveReason.Timeout, match[2].Status.Left[P3]);
        Assert.Equal(left.ExecuteTick, match[2].Engine.StopTick);
        Assert.True(match[2].Engine.CurrentTick <= left.ExecuteTick);
        Assert.Empty(match[2].Transport.Peers);
    }

    [Fact]
    public void The_others_vote_out_one_who_keeps_them_waiting()
    {
        var match = new Match(new RefereeSettings { KickVoteAfterMs = 2000 });
        match.Run(1500);
        match[2].Running = false;
        match.Run(1000);

        // Too early: nobody may vote yet.
        Assert.False(match.Referee.CanVoteOut(P3));
        Assert.False(match.Referee.Vote(P1, P3));

        match.Run(1500);
        Assert.True(match.Referee.CanVoteOut(P3));
        Assert.False(match.Referee.CanVoteOut(P1));

        // One of the two who stay is not more than half; the second is.
        match[1].Send(new KickVote(P2, P3));
        match.Run(200);
        Assert.Empty(match[0].Status.Left);
        Assert.True(match.Referee.Vote(P1, P3));
        match.Run(500);

        Assert.Equal(LeaveReason.Kicked, match[1].Status.Left[P3]);
        Assert.Equal([P1, P2], match[1].Engine.RemainingPlayers());
        Assert.False(match[0].Engine.IsWaitingForPlayers);
    }

    [Fact]
    public void A_vote_in_another_players_name_does_not_count()
    {
        var match = new Match(new RefereeSettings { KickVoteAfterMs = 1000 });
        match.Run(1500);
        match[2].Running = false;
        match.Run(2500);

        // The client of P2 speaks as P1: the host does not take it; nor two votes of one player.
        match[1].Send(new KickVote(P1, P3));
        match[1].Send(new KickVote(P2, P3));
        match[1].Send(new KickVote(P2, P3));
        match.Run(300);

        Assert.Empty(match[0].Status.Left);
    }

    [Fact]
    public void A_pause_stops_everyone_on_one_tick_until_its_player_takes_it_off()
    {
        var match = new Match(new RefereeSettings());
        match.Run(1500);

        match[1].Send(new PauseRequest(P2, true));
        match.Run(500);
        var ticks = match.Computers.Select(computer => computer.Engine.CurrentTick).ToList();
        Assert.Single(ticks.Distinct());
        Assert.All(match.Computers, computer => Assert.True(computer.Engine.IsPaused));
        Assert.Equal(P2, match[2].Status.PausedBy);
        Assert.Equal(2, match[0].Status.PausesLeft(P2));

        // Paused, nobody waits for anybody: no drop, no vote.
        match.Run(3000);
        Assert.Equal(ticks, match.Computers.Select(computer => computer.Engine.CurrentTick));
        Assert.All(match.Computers, computer => Assert.False(computer.Engine.IsWaitingForPlayers));
        Assert.False(match.Referee.CanVoteOut(P3));

        // Another cannot take it off; its player can.
        Assert.False(match.Referee.RequestPause(P1, false));
        match[2].Send(new PauseRequest(P3, false));
        match.Run(300);
        Assert.Equal(P2, match.Referee.PausedBy);
        match[1].Send(new PauseRequest(P2, false));
        match.Run(1000);

        Assert.Null(match[2].Status.PausedBy);
        Assert.All(match.Computers, computer => Assert.True(computer.Engine.CurrentTick > ticks[0] + 5));
        match.AssertSameChecksums(match[0], match[1], match[2]);
    }

    [Fact]
    public void A_pause_ends_by_itself_and_each_player_has_only_so_many()
    {
        var match = new Match(new RefereeSettings { PauseMs = 1000, PausesPerPlayer = 2 });
        match.Run(1000);

        for (var i = 0; i < 2; i++)
        {
            Assert.True(match.Referee.RequestPause(P1, true));
            // One pause at a time.
            Assert.False(match.Referee.RequestPause(P2, true));
            match.Run(600);
            Assert.All(match.Computers, computer => Assert.True(computer.Engine.IsPaused));
            match.Run(700);
            Assert.All(match.Computers, computer => Assert.False(computer.Engine.IsPaused));
        }

        Assert.Equal(0, match.Referee.PausesLeft(P1));
        Assert.False(match.Referee.RequestPause(P1, true));
        Assert.True(match.Referee.RequestPause(P2, true));
    }

    [Fact]
    public void A_player_who_leaves_while_paused_ends_the_pause()
    {
        var match = new Match(new RefereeSettings());
        match.Run(1000);
        match[2].Send(new PauseRequest(P3, true));
        match.Run(300);
        Assert.Equal(P3, match.Referee.PausedBy);

        match.Network.Disconnect(match[2].Transport, match[2].Transport.Peers[0]);
        match.Run(1000);

        Assert.Null(match.Referee.PausedBy);
        Assert.False(match[1].Engine.IsPaused);
        Assert.Equal(LeaveReason.Disconnected, match[1].Status.Left[P3]);
    }

    [Fact]
    public void The_replay_of_every_computer_plays_back_alone_with_the_same_checksums()
    {
        var match = new Match(new RefereeSettings());
        match.Run(2000);
        match[1].Engine.Issue(new MoveCommand(P2, 0, 0, match[1].Own(), new CellPos(32, 32).Center, false));
        match.Run(1000);
        match.Network.Disconnect(match[2].Transport, match[2].Transport.Peers[0]);
        match.Run(2000);

        var replays = match.Computers.Select(computer => ReplayFormat.Read(ReplayFormat.Write(computer.Recorder.Build()), TestRules.Standard.MapTypes)).ToList();

        // The one who left has its match up to where it stopped; the others theirs with the leaving in it.
        Assert.All(replays, replay => Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status));
        Assert.All(replays, replay => Assert.True(replay.Checksums.Count >= 3, $"{replay.Checksums.Count} checksums"));
        Assert.Contains(replays[0].Commands, command => command is PlayerLeftCommand { Player.Value: 3 });
        Assert.Contains(replays[2].Commands, command => command is MoveCommand { Player.Value: 2 });
        var shared = Math.Min(replays[0].TickCount, replays[1].TickCount);
        Assert.Equal(replays[0].Commands.Where(command => command.ExecuteTick < shared), replays[1].Commands.Where(command => command.ExecuteTick < shared));
    }

    [Fact]
    public void A_client_that_loses_the_host_hears_of_it()
    {
        var match = new Match(new RefereeSettings());
        match.Run(1000);
        var lost = new List<PeerId>();
        match[1].Engine.PeerDisconnected += lost.Add;

        match.Network.Disconnect(match[0].Transport, match[1].Transport.Id);
        match.Run(100);

        Assert.Equal([match[0].Transport.Id], lost);
    }

    /// <summary>The host (player 1) and two clients of a match of three persons, each on its team, every computer at 60 frames a second.</summary>
    private sealed class Match
    {
        public Match(RefereeSettings settings)
        {
            Network = new SimulatedNetwork(7);
            var transports = Enumerable.Range(0, 3).Select(_ => Network.AddPeer()).ToList();
            for (var i = 1; i < transports.Count; i++)
            {
                Network.Connect(transports[0], transports[i], Lan, Lan);
            }

            var starts = ImmutableArray.Create(new CellPos(10, 10), new CellPos(50, 50), new CellPos(10, 50));
            var setup = new MatchSetup("test_map", 77, new MatchOptions(FogOfWar: false),
                Enumerable.Range(1, 3).Select(id => new MatchPlayer(id, PlayerKind.Human, AiDifficulty.Medium, "union", id - 1, id, id - 1, starts[id - 1])).ToImmutableArray(),
                starts);
            var players = new[] { P1, P2, P3 };
            var relay = new HostRelay(transports[0]);
            Computers.Add(new Computer(relay, transports[0], P1, players, setup, settings));
            Computers.Add(new Computer(transports[1], transports[1], P2, players, setup, settings));
            Computers.Add(new Computer(transports[2], transports[2], P3, players, setup, settings));
            Referee = new MatchReferee(this[0].Engine, relay, new Dictionary<PeerId, PlayerId> { [transports[1].Id] = P2, [transports[2].Id] = P3 }, settings);
            Referee.Announced += this[0].Status.Apply;
            foreach (var computer in Computers.Skip(1))
            {
                computer.Engine.MessageReceived += (_, message) => computer.Status.Apply(message);
            }

            foreach (var computer in Computers)
            {
                computer.Engine.Start();
            }
        }

        public SimulatedNetwork Network { get; }

        public List<Computer> Computers { get; } = new();

        public MatchReferee Referee { get; }

        public Computer this[int index] => Computers[index];

        public void Run(long milliseconds)
        {
            for (var ms = 0; ms < milliseconds; ms++)
            {
                Network.Advance(1);
                if (Network.NowMs % 16 != 0)
                {
                    continue;
                }

                Referee.Update(16);
                foreach (var computer in Computers.Where(computer => computer.Running))
                {
                    computer.Engine.Update(16);
                    computer.Status.Update(16);
                }
            }
        }

        public void AssertSameChecksums(params Computer[] computers)
        {
            var own = computers.Select(computer => computer.Checksums.OwnChecksums.ToDictionary(checksum => checksum.Tick, checksum => checksum.Value)).ToList();
            var shared = own.Select(sums => (IEnumerable<int>)sums.Keys).Aggregate((a, b) => a.Intersect(b)).ToList();
            Assert.True(shared.Count >= 2);
            Assert.All(shared, tick => Assert.Single(own.Select(sums => sums[tick]).Distinct()));
        }
    }

    private sealed class Computer
    {
        private readonly ITransport _transport;

        public Computer(ITransport transport, SimulatedTransport raw, PlayerId player, IReadOnlyList<PlayerId> players, MatchSetup setup, RefereeSettings settings)
        {
            _transport = transport;
            Transport = raw;
            Simulation = new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard);
            Engine = new LockstepEngine(Simulation, transport, player, players, new LockstepSettings { InputDelay = 3, WaitNoticeMs = 1000 });
            Checksums = new ChecksumExchange(Engine, Simulation, transport, players);
            Status = new MatchStatus(settings);
            Recorder = new ReplayRecorder(Simulation);
            Engine.Stepped += _ => Recorder.AfterStep();
        }

        public ReplayRecorder Recorder { get; }

        /// <summary>The living units of its player.</summary>
        public ImmutableArray<EntityId> Own() =>
            Simulation.World.Entities.Where(entity => entity is Unit { IsAlive: true } && entity.Owner == Engine.LocalPlayer).Select(entity => entity.Id).ToImmutableArray();

        public SimulatedTransport Transport { get; }

        public GameSimulation Simulation { get; }

        public LockstepEngine Engine { get; }

        public ChecksumExchange Checksums { get; }

        public MatchStatus Status { get; }

        /// <summary>When false, the computer is frozen: no frames, the network still runs.</summary>
        public bool Running { get; set; } = true;

        /// <summary>A message to everyone it is connected to: the host, for a client.</summary>
        public void Send(NetMessage message) => _transport.SendToAll(MessageCodec.Write(message), DeliveryChannel.ReliableOrdered);
    }
}
