using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Lockstep;

/// <summary>
/// The lockstep of two computers (docs/implementation/M9_network.md, 4.5, 4.6, step 3, section 6): no tick without the bundles of
/// everyone, a command D ticks later on both, the frame limit, the catch-up, duplicates and strangers.
/// </summary>
public sealed class LockstepEngineTests
{
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);

    [Fact]
    public void The_start_sends_an_empty_bundle_for_each_of_the_first_D_ticks()
    {
        var match = new LockstepMatch(2, LinkConditions.Perfect, new LockstepSettings { InputDelay = 3 }, scriptTicks: 0);

        match[0].Engine.Start();
        match.Network.Advance(0);

        var bundles = Drain(match[1].Transport).Select(e => MessageCodec.Read(e.Data)).Cast<TickBundle>().ToList();
        Assert.Equal([TickBundle.Empty(P1, 0), TickBundle.Empty(P1, 1), TickBundle.Empty(P1, 2)], bundles);
    }

    [Fact]
    public void No_tick_is_computed_without_the_bundles_of_every_player()
    {
        var match = new LockstepMatch(2, LinkConditions.Perfect, new LockstepSettings(), scriptTicks: 0);
        match[0].Engine.Start();
        match[1].Running = false;

        match.Run(2_999);
        Assert.Equal(0, match[0].Engine.CurrentTick);
        Assert.Equal([P2], match[0].Engine.MissingPlayers());
        Assert.False(match[0].Engine.IsWaitingForPlayers);

        match.Run(100);
        Assert.True(match[0].Engine.IsWaitingForPlayers);

        match[1].Engine.Start();
        match[1].Running = true;
        match.Run(1_000);
        Assert.InRange(match[0].Engine.CurrentTick, 13, 17);
        Assert.Equal(0, match[0].Engine.WaitingMs);
        Assert.Empty(match[0].Engine.MissingPlayers());
    }

    [Fact]
    public void A_command_runs_D_ticks_after_the_next_tick_on_every_computer()
    {
        var match = new LockstepMatch(2, new LinkConditions { LatencyMs = 40 }, new LockstepSettings { InputDelay = 3 }, scriptTicks: 0);
        match.Start();
        match.RunUntil(20, limitMs: 5_000);

        var next = match[1].Engine.CurrentTick;
        var given = match[1].Engine.Issue(new SpawnTestUnitCommand(P2, 0, 0, UnitType.HeavyTank, new CellPos(20, 20).Center));
        match.RunUntil(next + 10, limitMs: 5_000);

        Assert.Equal(next + 3, given.ExecuteTick);
        foreach (var client in match.Clients)
        {
            var executed = Assert.Single(client.Simulation.ExecutedCommands);
            Assert.Equal(given, executed);
            Assert.Single(client.Simulation.World.Entities.OfType<Unit>(), unit => unit.Owner == P2);
        }
    }

    [Fact]
    public void A_computer_left_behind_catches_up_within_the_frame_limit()
    {
        var match = new LockstepMatch(2, new LinkConditions { LatencyMs = 20 }, new LockstepSettings { InputDelay = 15 }, scriptTicks: 3_000);
        match.Start();
        match.RunUntil(100, limitMs: 10_000);

        // The second computer freezes for 4.5 s; the first goes on while the bundles of the frozen one last, D ticks, then waits.
        var frozenAt = match[1].Engine.CurrentTick;
        match[1].Running = false;
        match.Run(4_500);
        Assert.Equal(frozenAt, match[1].Engine.CurrentTick);
        Assert.Equal(frozenAt + 15, match[0].Engine.CurrentTick);
        Assert.True(match[0].Engine.IsWaitingForPlayers);

        // Back after the freeze: its first frame lasted 4.5 s, but it computes 10 ticks at most, then catches up in the next frames.
        match[1].Engine.Update(4_500);
        Assert.Equal(10, match[1].Engine.TicksLastFrame);
        match[1].Running = true;
        match.Run(500);
        Assert.InRange(match[1].Engine.CurrentTick - match[0].Engine.CurrentTick, -2, 2);
        Assert.InRange(match[1].MostTicksInFrame, 2, 10);

        match.RunUntil(400, limitMs: 30_000);
        AssertSameMatch(match, 400);
    }

    [Fact]
    public void Bundles_count_once_in_any_order_and_strangers_are_refused()
    {
        var match = new LockstepMatch(3, LinkConditions.Perfect, new LockstepSettings { InputDelay = 2 }, scriptTicks: 0);
        var engine = match[0].Engine;
        engine.Start();
        var p3 = new PlayerId(3);

        Assert.True(engine.Receive(TickBundle.Empty(P2, 1)));
        Assert.True(engine.Receive(TickBundle.Empty(P2, 0)));
        Assert.False(engine.Receive(TickBundle.Empty(P2, 0)));
        Assert.Equal(1, engine.DuplicateBundles);
        Assert.Equal([p3], engine.MissingPlayers());

        // A player not in the match, our own name, a tick far ahead: refused.
        var twoPlayers = new LockstepEngine(match[1].Simulation, match[1].Transport, P2, [P1, P2]);
        Assert.False(twoPlayers.Receive(TickBundle.Empty(p3, 0)));
        Assert.False(engine.Receive(TickBundle.Empty(P1, 5)));
        Assert.False(engine.Receive(TickBundle.Empty(p3, 2 + 256 + 1)));
        Assert.Equal(2, engine.RejectedMessages);
        Assert.Equal(1, twoPlayers.RejectedMessages);

        Assert.True(engine.Receive(TickBundle.Empty(p3, 0)));
        Assert.True(engine.Receive(TickBundle.Empty(p3, 1)));
        engine.Update(200);
        Assert.Equal(2, engine.CurrentTick);
        Assert.False(engine.Receive(TickBundle.Empty(P2, 1)));
        Assert.Equal(2, engine.DuplicateBundles);
    }

    [Fact]
    public void Garbage_is_counted_and_other_messages_go_up_to_the_session()
    {
        var match = new LockstepMatch(2, LinkConditions.Perfect, new LockstepSettings(), scriptTicks: 0);
        var engine = match[0].Engine;
        var others = new List<(PeerId, NetMessage)>();
        engine.MessageReceived += (peer, message) => others.Add((peer, message));
        engine.Start();

        match[1].Transport.Send(match[0].Transport.Id, [1, 2, 3], DeliveryChannel.ReliableOrdered);
        match[1].Transport.Send(match[0].Transport.Id, MessageCodec.Write(new Ping(77)), DeliveryChannel.Unreliable);
        match.Network.Advance(0);
        engine.Update(16);

        Assert.Equal(1, engine.RejectedMessages);
        Assert.Equal([(match[1].Transport.Id, (NetMessage)new Ping(77))], others);
    }

    [Fact]
    public void A_command_of_another_player_cannot_be_given()
    {
        var match = new LockstepMatch(2, LinkConditions.Perfect, new LockstepSettings(), scriptTicks: 0);

        Assert.Throws<ArgumentException>(() => match[0].Engine.Issue(new SurrenderCommand(P2, 0, 0)));
    }

    /// <summary>Every computer has the same checksums and the same commands up to <paramref name="ticks"/>.</summary>
    internal static void AssertSameMatch(LockstepMatch match, int ticks)
    {
        var first = match[0];
        var expected = first.Checksums.Where(pair => pair.Key <= ticks).ToList();
        Assert.Equal(ticks / 15, expected.Count);
        foreach (var client in match.Clients.Skip(1))
        {
            Assert.Equal(expected, client.Checksums.Where(pair => pair.Key <= ticks));
            Assert.Equal(first.ExecutedBefore(ticks), client.ExecutedBefore(ticks));
        }
    }

    private static List<TransportEvent> Drain(SimulatedTransport peer)
    {
        var events = new List<TransportEvent>();
        while (peer.TryReceive(out var e))
        {
            if (e.Kind == TransportEventKind.Message)
            {
                events.Add(e);
            }
        }

        return events;
    }
}
