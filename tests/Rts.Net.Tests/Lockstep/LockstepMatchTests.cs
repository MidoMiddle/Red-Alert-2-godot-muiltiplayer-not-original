using Rts.Net.Lockstep;
using Rts.Net.Transport;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.Net.Tests.Lockstep;

/// <summary>
/// The scenario of step 3 (docs/implementation/M9_network.md, section 5): two simulations in one process over the in-memory network
/// play 5000 ticks of the reference match — each giving the commands of its own player — and agree on every checksum, without latency
/// and over 150 ± 50 ms with 2 % losses.
/// </summary>
public sealed class LockstepMatchTests(ITestOutputHelper output)
{
    private const int Ticks = 5_000;

    [Fact]
    public void Two_players_without_latency_agree_for_5000_ticks()
    {
        var match = new LockstepMatch(2, LinkConditions.Perfect, new LockstepSettings { InputDelay = 2 }, Ticks, frameMs: [16, 33]);
        match.Start();

        // 5000 ticks are 333 s of play: without latency the match goes at full pace.
        match.RunUntil(Ticks, limitMs: 340_000);

        LockstepEngineTests.AssertSameMatch(match, Ticks);
        Assert.True(match[0].ExecutedBefore(Ticks).Count > 1_000);
        Assert.All(match.Clients, client => Assert.Equal(0, client.LongestWaitMs));
        Report(match);
    }

    [Fact]
    public void Two_players_over_150_ms_with_jitter_and_2_percent_losses_agree_for_5000_ticks()
    {
        var internet = new LinkConditions { LatencyMs = 150, JitterMs = 50, LossPercent = 2 };
        var match = new LockstepMatch(2, internet, new LockstepSettings { InputDelay = 4 }, Ticks, networkSeed: 150, frameMs: [16, 33]);
        match.Start();

        match.RunUntil(Ticks, limitMs: 600_000);

        LockstepEngineTests.AssertSameMatch(match, Ticks);
        Assert.True(match[0].ExecutedBefore(Ticks).Count > 1_000);
        Assert.True(match.Network.Stats(match[0].Transport, match[1].Transport).Lost > 0);
        Assert.All(match.Clients, client => Assert.Equal(0, client.Engine.RejectedMessages));
        Report(match);
    }

    [Fact]
    public void A_command_that_bypasses_the_lockstep_shows_in_the_checksums()
    {
        var match = new LockstepMatch(2, new LinkConditions { LatencyMs = 50 }, new LockstepSettings { InputDelay = 3 }, 300);
        match.Start();
        match.RunUntil(100, limitMs: 20_000);

        // A unit that only the first computer knows about: the next checksum differs.
        var tick = match[0].Engine.CurrentTick + 1;
        match[0].Simulation.Enqueue(new SpawnTestUnitCommand(new PlayerId(1), tick, 0x4000_0000, UnitType.HeavyTank, new CellPos(5, 5).Center));
        match.RunUntil(300, limitMs: 20_000);

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => LockstepEngineTests.AssertSameMatch(match, 300));
        Assert.Equal(match[0].Checksums.Where(pair => pair.Key < tick), match[1].Checksums.Where(pair => pair.Key < tick));
    }

    private void Report(LockstepMatch match)
    {
        output.WriteLine($"{Ticks} ticks in {match.Network.NowMs} ms of the network clock.");
        foreach (var client in match.Clients)
        {
            output.WriteLine(
                $"{client.Engine.LocalPlayer}: frame {client.FrameMs} ms, longest wait {client.LongestWaitMs} ms, at most {client.MostTicksInFrame} ticks a frame, " +
                $"{client.Engine.DuplicateBundles} duplicates, {client.Engine.RejectedMessages} refused");
        }

        var stats = match.Network.Stats(match[0].Transport, match[1].Transport);
        output.WriteLine($"P1 → P2: {stats.Sent} sent, {stats.Lost} lost and sent again, {stats.Delivered} delivered.");
    }
}
