using Rts.Simulation.Tests;
using System.Reflection;
using Rts.Net.Desync;
using Rts.Net.Lockstep;
using Rts.Net.Tests.Lockstep;
using Rts.Net.Transport;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Desync;

/// <summary>
/// Step 5 of M9 (docs/implementation/M9_network.md, 4.10): the checksums of the computers are compared every 15 ticks; a state
/// spoilt on one computer is found on the next check, every computer stops on the same tick, and the dumps of their states differ
/// exactly where the spoilt field is.
/// </summary>
public sealed class DesyncTests
{
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);
    private static readonly PlayerId P3 = new(3);

    [Fact]
    public void Checksums_that_agree_are_no_desync_whatever_their_order()
    {
        var detector = new DesyncDetector([P1, P2, P3]);
        Assert.Null(detector.Report(P2, 30, 7));
        Assert.Null(detector.Report(P1, 15, 5));
        Assert.Null(detector.Report(P3, 15, 5));
        Assert.Equal(-1, detector.LastAgreedTick);
        Assert.Null(detector.Report(P2, 15, 5));
        Assert.Equal(15, detector.LastAgreedTick);
        Assert.Null(detector.Report(P1, 30, 7));
        Assert.Null(detector.Report(P3, 30, 7));
        Assert.Equal(30, detector.LastAgreedTick);
        Assert.Null(detector.Found);
    }

    [Fact]
    public void One_checksum_apart_is_the_desync_of_its_tick_and_it_stays()
    {
        var detector = new DesyncDetector([P1, P2, P3]);
        detector.Report(P1, 15, 5);
        detector.Report(P2, 15, 5);
        var desync = detector.Report(P3, 15, 6);

        Assert.NotNull(desync);
        Assert.Equal(15, desync.Tick);
        Assert.Equal([5UL, 5UL, 6UL], desync.Hashes.Values);
        Assert.Same(desync, detector.Found);
        Assert.Null(detector.Report(P1, 30, 1));
    }

    [Fact]
    public void One_who_leaves_is_no_longer_awaited()
    {
        var detector = new DesyncDetector([P1, P2, P3]);
        detector.Report(P1, 15, 5);
        detector.Report(P2, 15, 5);
        Assert.Equal(-1, detector.LastAgreedTick);

        Assert.Null(detector.Drop(P3));
        Assert.Equal(15, detector.LastAgreedTick);
        Assert.Null(detector.Report(P3, 30, 9));
    }

    [Fact]
    public void A_spoilt_state_is_found_on_the_next_check_and_both_stop_on_one_tick_with_dumps_that_show_it()
    {
        var match = new LockstepMatch(2, new LinkConditions { LatencyMs = 60, JitterMs = 20 }, new LockstepSettings { InputDelay = 3 }, scriptTicks: 600);
        var exchanges = match.Clients.Select(client => new ChecksumExchange(client.Engine, client.Simulation, client.Transport, [P1, P2])).ToList();
        match.Start();
        match.RunUntil(100, limitMs: 20_000);
        Assert.All(exchanges, exchange => Assert.Null(exchange.Detector.Found));

        // The second computer's state goes wrong: one unit has other hit points than everywhere else.
        var world = match[1].Simulation.World;
        var unit = world.Entities.OfType<Unit>().First(u => u.IsAlive);
        var spoiltAt = match[1].Engine.CurrentTick;
        typeof(Unit).GetProperty(nameof(Unit.Hp))!.SetValue(unit, unit.Hp - 1);

        match.Run(5_000);

        // Found on the first check after it, by both, and both stopped on the same tick.
        var expected = (spoiltAt / 15 + 1) * 15;
        Assert.All(exchanges, exchange => Assert.Equal(expected, exchange.Detector.Found!.Tick));
        Assert.All(exchanges, exchange => Assert.True(exchange.Stopped));
        Assert.All(match.Clients, client => Assert.Equal(expected + ChecksumExchange.StopAfterTicks, client.Engine.CurrentTick));
        Assert.Equal(2, exchanges[0].Detector.Found!.Hashes.Values.Distinct().Count());

        // The dumps of the same tick differ only in the spoilt unit, its hit points among them.
        var dumps = match.Clients.Select(client => StateDump.Write(client.Simulation.World).Split('\n')).ToList();
        var differing = dumps[0].Except(dumps[1]).ToList();
        Assert.NotEmpty(differing);
        Assert.All(differing, line => Assert.StartsWith($"entity[{unit.Id.Value}].", line));
        Assert.Contains(differing, line => line.StartsWith($"entity[{unit.Id.Value}].Hp = ", StringComparison.Ordinal));
        Assert.Equal(dumps[0].Length, dumps[1].Length);

        // Each keeps its dump, what it knows of the desync and the match up to there, in one folder they can share.
        var folder = Path.Combine(Path.GetTempPath(), "rts-desync-" + Guid.NewGuid().ToString("N"));
        try
        {
            for (var i = 0; i < 2; i++)
            {
                DesyncDump.Save(folder, exchanges[i].Detector.Found!, match[i].Engine.LocalPlayer, match[i].Simulation, exchanges[i].OwnChecksums, 0);
            }

            Assert.Equal(["p1.rtsreplay", "p1_desync.txt", "p1_state.txt", "p2.rtsreplay", "p2_desync.txt", "p2_state.txt"],
                Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            Assert.Equal(string.Join('\n', dumps[1]), File.ReadAllText(Path.Combine(folder, "p2_state.txt")));
            Assert.Contains($"desync_tick = {expected}", File.ReadAllText(Path.Combine(folder, "p1_desync.txt")));
            var replay = Rts.Simulation.Replays.ReplayFormat.Read(File.ReadAllBytes(Path.Combine(folder, "p1.rtsreplay")), TestRules.Standard.MapTypes);
            Assert.Equal(expected + ChecksumExchange.StopAfterTicks, replay.TickCount);
            Assert.Equal(match[0].Simulation.ExecutedCommands.Count, replay.Commands.Count);

            // Step 6: the replay computed again tells whose computer went wrong — the second's, at the spoilt hit points.
            var verdicts = DesyncReplay.Check(replay, Rts.Simulation.Tests.TestRules.Standard,
                [("p1", File.ReadAllText(Path.Combine(folder, "p1_state.txt"))), ("p2", File.ReadAllText(Path.Combine(folder, "p2_state.txt")))]);
            Assert.True(verdicts[0].Agrees, verdicts[0].ToString());
            Assert.False(verdicts[1].Agrees);
            Assert.Contains(verdicts[1].Differences, difference => difference.Path == $"entity[{unit.Id.Value}].Hp");
            Assert.All(verdicts[1].Differences, difference => Assert.Equal($"entity[{unit.Id.Value}]", difference.Owner));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_match_that_hashes_every_tick_and_computed_its_first_tick_before_the_lockstep_agrees_on_every_tick()
    {
        // As a match of the game (M9 step 9): the simulation hashes every tick, and the loading screen computes the first tick before
        // the lockstep starts — its checksum is sent when the exchange begins.
        var match = new LockstepMatch(2, new LinkConditions { LatencyMs = 5 }, new LockstepSettings { InputDelay = 3 }, scriptTicks: 300, hashInterval: 1);
        foreach (var client in match.Clients)
        {
            client.Simulation.Step();
        }

        var exchanges = match.Clients.Select(client => new ChecksumExchange(client.Engine, client.Simulation, client.Transport, [P1, P2])).ToList();
        match.Start();
        match.RunUntil(200, limitMs: 60_000);

        Assert.All(exchanges, exchange => Assert.True(exchange.Detector.LastAgreedTick >= 195, $"agreed up to {exchange.Detector.LastAgreedTick}"));
        Assert.All(exchanges, exchange => Assert.Equal(1, exchange.OwnChecksums[0].Tick));
    }

    [Fact]
    public void Two_dumps_compare_field_by_field_and_name_the_owner_of_each_difference()
    {
        var first = "tick = 30\nworld.Random = 5\nplayer[1].Credits = 1000\nentity[12].Hp = 300\nentity[12].Position = (1, 2)\nmap.land[3] = 0011\n";
        var second = "tick = 30\nworld.Random = 5\nplayer[1].Credits = 900\nentity[12].Hp = 299\nentity[12].Position = (1, 2)\nmap.land[3] = 0011\nentity[13].Hp = 5\n";

        var differences = DumpDiff.Compare(first, second);
        Assert.Equal(["player[1].Credits", "entity[12].Hp", "entity[13].Hp"], differences.Select(difference => difference.Path));
        Assert.Equal(("player[1]", "Credits", "1000", "900"), (differences[0].Owner, differences[0].Field, differences[0].First, differences[0].Second));
        Assert.Equal(("entity[13]", null, "5"), (differences[2].Owner, differences[2].First, differences[2].Second));
        Assert.Equal("map", new DumpDifference("map.land[3]", "0", "1").Owner);
        Assert.Equal(30, DumpDiff.TickOf(first));
        Assert.Empty(DumpDiff.Compare(first, first));
        Assert.Contains("3 fields differ; the first is player[1].Credits: 1000 ≠ 900", DumpDiff.Report(differences));
        Assert.Contains("entity[12] (1)", DumpDiff.Report(differences));
    }

    [Fact]
    public void The_dump_of_one_state_is_the_same_text_every_time_and_holds_the_whole_world()
    {
        var match = new LockstepMatch(1, LinkConditions.Perfect, new LockstepSettings { InputDelay = 2 }, scriptTicks: 300);
        match.Start();
        match.RunUntil(200, limitMs: 30_000);
        var world = match[0].Simulation.World;

        var first = StateDump.Write(world);
        Assert.Equal(first, StateDump.Write(world));
        Assert.StartsWith("tick = 200\n", first);
        Assert.Contains("player[1].Credits = ", first);
        Assert.Contains($"map.land[{world.HeightCells - 1}] = ", first);
        Assert.All(world.Entities, entity => Assert.Contains($"entity[{entity.Id.Value}].kind = ", first));
        Assert.DoesNotContain("(throws", first);
    }
}
