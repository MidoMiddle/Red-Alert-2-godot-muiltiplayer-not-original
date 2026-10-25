using Rts.Net.Lockstep;
using Rts.Net.Transport;
using Xunit.Abstractions;

namespace Rts.Net.Tests.Lockstep;

/// <summary>
/// Step 7 of M9 (docs/implementation/M9_network.md, 4.7): the input delay follows the network — the host measures the round trips,
/// moves the delay one tick at a time, and every computer switches on the same tick; the copies of the bundles over the unreliable
/// channel keep a lost packet from stopping the game.
/// </summary>
public sealed class InputDelayTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0, 0, 2)]
    [InlineData(20, 0, 2)]
    [InlineData(50, 0, 2)]
    [InlineData(100, 0, 3)]
    [InlineData(150, 20, 4)]
    [InlineData(300, 0, 6)]
    [InlineData(300, 50, 7)]
    [InlineData(2000, 0, 10)]
    public void The_delay_covers_the_longest_way_its_jitter_and_one_tick(int oneWayMs, int jitterMs, int delay) =>
        Assert.Equal(delay, InputDelayController.Required(oneWayMs, jitterMs, 15));

    [Fact]
    public void The_delay_moves_one_tick_at_a_time_and_at_most_every_two_seconds()
    {
        var controller = new InputDelayController(2);
        Assert.Equal(3, controller.Next(6, 0));
        Assert.Null(controller.Next(6, 1_999));
        Assert.Equal(4, controller.Next(6, 2_000));
        Assert.Null(controller.Next(4, 10_000));
        Assert.Equal(3, controller.Next(2, 10_000));
        Assert.Equal(10, new InputDelayController(99).Current);
        Assert.Equal(2, new InputDelayController(0).Current);
    }

    [Fact]
    public void The_pings_measure_the_round_trip_and_its_jitter()
    {
        var match = new LockstepMatch(2, new LinkConditions { LatencyMs = 100, JitterMs = 20 }, new LockstepSettings { InputDelay = 4 }, scriptTicks: 0);
        Watch(match);
        match.Start();
        match.Run(6_000);

        var latency = match[0].Monitor!.Of(match[0].Transport.Peers[0])!.Value;
        output.WriteLine($"rtt {latency.RttMs} max {latency.MaxRttMs} jitter {latency.JitterMs}");
        // 200 ms of the way there and back, ± 40 of jitter, and up to a frame of the other's to answer.
        Assert.InRange(latency.RttMs, 190, 240);
        Assert.InRange(latency.MaxRttMs, latency.RttMs, 280);
        Assert.InRange(latency.JitterMs, 10, 90);
    }

    [Fact]
    public void When_the_ping_grows_the_delay_grows_and_everyone_switches_on_the_same_tick()
    {
        var match = new LockstepMatch(2, new LinkConditions { LatencyMs = 50 }, new LockstepSettings { InputDelay = 2 }, scriptTicks: 2_400);
        Watch(match);
        var authority = new InputDelayAuthority(match[0].Engine, match[0].Monitor!, match[0].Transport, 15);
        match[0].Authority = authority;
        var decisions = new List<long>();
        authority.Decided += (now, _, _) => decisions.Add(now);
        var switches = match.Clients.Select(_ => new List<(int Tick, int Delay)>()).ToList();
        for (var i = 0; i < match.Clients.Count; i++)
        {
            var list = switches[i];
            match[i].Engine.InputDelayChanged += (tick, delay) => list.Add((tick, delay));
        }

        match.Start();
        match.RunUntil(450, limitMs: 60_000);
        Assert.All(match.Clients, client => Assert.Equal(2, client.Engine.InputDelay));

        // The way grows to 300 ms each direction.
        var slow = new LinkConditions { LatencyMs = 300 };
        match.Network.SetConditions(match[0].Transport, match[1].Transport, slow);
        match.Network.SetConditions(match[1].Transport, match[0].Transport, slow);
        match.RunUntil(2_400, limitMs: 400_000);

        output.WriteLine(string.Join(", ", switches[0].Select(s => $"{s.Tick}:{s.Delay}")));
        Assert.Equal(6, match[0].Engine.InputDelay);
        Assert.Equal(switches[0], switches[1]);
        Assert.Equal([3, 4, 5, 6], switches[0].Select(s => s.Delay));
        // One tick at a time, every two seconds at most.
        Assert.All(decisions.Zip(decisions.Skip(1)), pair => Assert.True(pair.Second - pair.First >= InputDelayController.MinIntervalMs));
        LockstepEngineTests.AssertSameMatch(match, 2_400);
    }

    [Fact]
    public void Over_150_ms_with_2_percent_losses_the_copies_keep_the_pace()
    {
        // The scenario of step 3 that went at 82 % of the pace: every lost packet stopped both up to 1.4 s.
        var internet = new LinkConditions { LatencyMs = 150, JitterMs = 50, LossPercent = 2 };
        var match = new LockstepMatch(2, internet, new LockstepSettings { InputDelay = 6 }, 5_000, networkSeed: 150, frameMs: [16, 33]);
        match.Start();

        match.RunUntil(5_000, limitMs: 600_000);

        var pace = 5_000 / 15.0 * 1000 / match.Network.NowMs;
        output.WriteLine($"{match.Network.NowMs} ms, pace {pace:P1}, longest wait {match.Clients.Max(c => c.LongestWaitMs)} ms");
        LockstepEngineTests.AssertSameMatch(match, 5_000);
        Assert.True(match.Network.Stats(match[0].Transport, match[1].Transport).Lost > 0);
        Assert.True(pace > 0.98, $"pace {pace:P1}");
        Assert.All(match.Clients, client => Assert.True(client.LongestWaitMs <= 6 * 1000 / 15, $"waited {client.LongestWaitMs} ms"));
    }

    private static void Watch(LockstepMatch match)
    {
        foreach (var client in match.Clients)
        {
            client.Monitor = new LatencyMonitor(client.Transport);
            client.Monitor.Attach(client.Engine);
            client.Clock = () => match.Network.NowMs;
        }
    }
}
