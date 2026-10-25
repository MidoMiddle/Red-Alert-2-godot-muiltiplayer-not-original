using System.Buffers.Binary;
using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Replays;

public sealed class ReplayTests
{
    /// <summary>Plays a seeded match with random commands and records it, like the client will.</summary>
    private static (Replay Replay, GameSimulation Live) RecordMatch(int ticks = 600, ulong seed = 2026)
    {
        var simulation = new GameSimulation(new SimulationSettings(seed), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        var random = new TestRandom(seed);
        var sequences = new uint[3];

        for (var tick = 0; tick < ticks; tick++)
        {
            if (random.NextInt(4) == 0)
            {
                var player = 1 + random.NextInt(2);
                Command command = random.NextInt(3) == 0
                    ? Spawn(player, tick, sequences[player]++, random.NextInt(64 * 256), random.NextInt(64 * 256))
                    : Move(player, tick, sequences[player]++, Enumerable.Range(1, 1 + random.NextInt(12)).ToArray(), random.NextInt(64 * 256), random.NextInt(64 * 256));
                simulation.Enqueue(command);
            }

            simulation.Step();
            recorder.AfterStep();
        }

        return (recorder.Build(), simulation);
    }

    [Fact]
    public void Recorded_match_verifies_and_reproduces_the_final_state()
    {
        var (replay, live) = RecordMatch();

        var result = ReplayPlayer.Verify(replay, TestRules.Standard);

        Assert.Equal(ReplayStatus.Match, result.Status);
        Assert.Equal(new StateChecksum(live.CurrentTick, StateHasher.Compute(live.World)), result.Final);
        Assert.Equal(600 / ReplayRecorder.DefaultChecksumInterval, replay.Checksums.Count);
        Assert.NotEmpty(replay.Commands);
    }

    [Fact]
    public void Replay_survives_a_trip_through_bytes()
    {
        var (replay, _) = RecordMatch();

        var bytes = ReplayFormat.Write(replay);
        var read = ReplayFormat.Read(bytes);

        Assert.Equal(replay.SimVersion, read.SimVersion);
        Assert.Equal(replay.DataHash, read.DataHash);
        Assert.Equal(replay.Settings.StartingCredits, read.Settings.StartingCredits);
        Assert.Equal(replay.Settings, read.Settings);
        Assert.Equal(replay.TickCount, read.TickCount);
        Assert.Equal(replay.Commands, read.Commands);
        Assert.Equal(replay.Checksums, read.Checksums);
        Assert.Equal(bytes, ReplayFormat.Write(read));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read, TestRules.Standard).Status);
    }

    [Fact]
    public void File_starts_with_magic_and_versions()
    {
        var (replay, _) = RecordMatch(ticks: 15);

        var bytes = ReplayFormat.Write(replay);

        Assert.Equal("RTSR"u8.ToArray(), bytes[..4]);
        Assert.Equal(ReplayFormat.FormatVersion, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(SimulationVersion.Current, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(6)));
        Assert.Equal(DataHasher.NoData, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(10)));
    }

    [Fact]
    public void The_starting_credits_are_kept_in_the_replay()
    {
        var replay = new Replay(SimulationVersion.Current, DataHasher.NoData, new SimulationSettings(9) { StartingCredits = 7500 }, 0, [], []);

        Assert.Equal(7500, ReplayFormat.Read(ReplayFormat.Write(replay)).Settings.StartingCredits);
    }

    [Fact]
    public void The_personalities_of_the_players_are_kept_in_the_replay()
    {
        // Format 20 (M8 step 13): a random one as it is — the bots run again draw it the same way.
        var setup = Matches.MatchSetupTests.OneOnOne();
        setup = setup with { Players = [setup.Players[0], setup.Players[1] with { Personality = "random" }] };
        var replay = new Replay(
            SimulationVersion.Current, DataHasher.NoData, SimulationSettings.ForMatch(setup, new Rts.Simulation.Map.MapGrid(64, 64)), 0, [], []);

        var read = ReplayFormat.Read(ReplayFormat.Write(replay)).Settings.Match!;

        Assert.Equal(["balanced", "random"], read.Players.Select(player => player.Personality));
        Assert.Equal(setup, read);
    }

    [Fact]
    public void The_header_alone_tells_the_length_and_whether_this_build_plays_it()
    {
        // M6 step 15: the list of replays reads it from a file of any format version.
        var replay = new Replay(SimulationVersion.Current, DataHasher.NoData, new SimulationSettings(9), 450, [], []);
        var bytes = ReplayFormat.Write(replay);

        var header = ReplayFormat.ReadHeader(bytes);
        Assert.Equal((ReplayFormat.FormatVersion, SimulationVersion.Current, 15, 450), (header.FormatVersion, header.SimVersion, header.TicksPerSecond, header.TickCount));
        Assert.True(header.Playable);

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), ReplayFormat.FormatVersion - 1);
        Assert.False(ReplayFormat.ReadHeader(bytes).Playable);
        Assert.False((header with { SimVersion = SimulationVersion.Current - 1 }).Playable);
        Assert.Throws<InvalidDataException>(() => ReplayFormat.ReadHeader(bytes.AsSpan(0, 20)));

        // Before v10 the header was another: no length.
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 9);
        Assert.Equal(-1, ReplayFormat.ReadHeader(bytes).TickCount);
    }

    [Fact]
    public void The_network_of_a_match_is_kept_in_the_replay_and_a_single_match_has_none()
    {
        // Format 27 (M9 step 11): the version of the game, the hash of the map, the names by player.
        var setup = Matches.MatchSetupTests.OneOnOne();
        var single = new Replay(
            SimulationVersion.Current, DataHasher.NoData, SimulationSettings.ForMatch(setup, new Rts.Simulation.Map.MapGrid(64, 64)), 0, [], []);
        var network = single with { Network = new ReplayNetwork("0.9.0+abc", 0xFEED_F00D_1234_5678UL, new Dictionary<int, string> { [2] = "Guest", [1] = "Хост" }) };

        var read = ReplayFormat.Read(ReplayFormat.Write(network)).Network!;

        Assert.Null(ReplayFormat.Read(ReplayFormat.Write(single)).Network);
        Assert.Equal(("0.9.0+abc", 0xFEED_F00D_1234_5678UL), (read.GameVersion, read.MapHash));
        Assert.Equal([(1, "Хост"), (2, "Guest")], read.Names.Select(pair => (pair.Key, pair.Value)));
    }

    [Fact]
    public void The_data_hash_is_written_after_the_rules_version_and_read_back()
    {
        var simulation = new GameSimulation(new SimulationSettings(5), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation, dataHash: 0x0123456789abcdefUL);
        simulation.Step();
        recorder.AfterStep();

        var bytes = ReplayFormat.Write(recorder.Build());

        Assert.Equal(0x0123456789abcdefUL, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(10)));
        Assert.Equal(0x0123456789abcdefUL, ReplayFormat.Read(bytes).DataHash);
    }

    [Fact]
    public void A_replay_of_other_data_gets_a_warning_and_still_plays()
    {
        var (replay, _) = RecordMatch(ticks: 60);
        var other = replay with { DataHash = 0xfeedUL };

        Assert.Null(ReplayPlayer.DataWarning(replay, DataHasher.NoData));
        Assert.Equal(
            "recorded with other data (hash 000000000000feed, here 0000000000000000): it may not play the same",
            ReplayPlayer.DataWarning(other, DataHasher.NoData));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(other, TestRules.Standard).Status);
    }

    [Fact]
    public void Changed_command_is_reported_as_desync_on_the_first_affected_checksum()
    {
        var (replay, _) = RecordMatch();
        // The last move: no later order to the same units can overwrite the change before a checksum sees it.
        var index = replay.Commands.ToList().FindLastIndex(c => c is MoveCommand { ExecuteTick: > 100 });
        var original = (MoveCommand)replay.Commands[index];
        var tampered = new MoveCommand(original.Player, original.ExecuteTick, original.Sequence, original.Units, new WorldPos(1, 1));
        var commands = replay.Commands.ToList();
        commands[index] = tampered;

        var result = ReplayPlayer.Verify(replay with { Commands = commands }, TestRules.Standard);

        Assert.Equal(ReplayStatus.Desync, result.Status);
        Assert.InRange(result.Expected!.Value.Tick, original.ExecuteTick + 1, original.ExecuteTick + ReplayRecorder.DefaultChecksumInterval);
        Assert.NotEqual(result.Expected, result.Actual);
    }

    [Fact]
    public void Changed_checksum_is_reported_as_desync_on_that_tick()
    {
        var (replay, _) = RecordMatch();
        var checksums = replay.Checksums.ToList();
        checksums[10] = checksums[10] with { Value = checksums[10].Value ^ 1 };

        var result = ReplayPlayer.Verify(replay with { Checksums = checksums }, TestRules.Standard);

        Assert.Equal(ReplayStatus.Desync, result.Status);
        Assert.Equal(checksums[10].Tick, result.Expected!.Value.Tick);
    }

    [Fact]
    public void Different_rules_version_is_reported_as_incompatible()
    {
        var (replay, _) = RecordMatch(ticks: 30);

        var result = ReplayPlayer.Verify(replay with { SimVersion = SimulationVersion.Current + 1 }, TestRules.Standard);

        Assert.Equal(ReplayStatus.IncompatibleVersion, result.Status);
    }

    [Fact]
    public void Commands_scheduled_for_unplayed_ticks_are_not_recorded()
    {
        var simulation = new GameSimulation(new SimulationSettings(1), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(Spawn(player: 1, tick: 0, sequence: 0));
        simulation.Enqueue(Spawn(player: 1, tick: 50, sequence: 1));

        simulation.Step();
        recorder.AfterStep();

        var replay = recorder.Build();
        Assert.Single(replay.Commands);
        Assert.Equal(1, replay.TickCount);
    }

    [Fact]
    public void Checksum_interval_must_be_a_multiple_of_the_hash_interval()
    {
        var simulation = new GameSimulation(new SimulationSettings(1) { HashInterval = 4 }, TestRules.Standard);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayRecorder(simulation, checksumInterval: 6));
        _ = new ReplayRecorder(simulation, checksumInterval: 8);
    }

    [Fact]
    public void Simulation_from_replay_steps_through_the_match()
    {
        var (replay, live) = RecordMatch(ticks: 200);
        var simulation = ReplayPlayer.CreateSimulation(replay, TestRules.Standard);

        for (var i = 0; i < replay.TickCount; i++)
        {
            simulation.Step();
        }

        Assert.Equal(StateHasher.Compute(live.World), StateHasher.Compute(simulation.World));
    }

    [Theory]
    [InlineData("magic")]
    [InlineData("format version")]
    [InlineData("truncated")]
    [InlineData("trailing byte")]
    [InlineData("negative ticks")]
    public void Malformed_files_are_rejected(string damage)
    {
        var bytes = ReplayFormat.Write(RecordMatch(ticks: 30).Replay).ToList();

        switch (damage)
        {
            case "magic": bytes[0] = (byte)'X'; break;
            case "format version": bytes[4] = 99; break;
            case "truncated": bytes.RemoveAt(bytes.Count - 1); break;
            case "trailing byte": bytes.Add(0); break;
            case "negative ticks": bytes[49] = 0xFF; break;
        }

        Assert.Throws<InvalidDataException>(() => ReplayFormat.Read(bytes.ToArray()));
    }

    [Fact]
    public void Empty_match_round_trips()
    {
        var replay = new Replay(SimulationVersion.Current, DataHasher.NoData, new SimulationSettings(9), 0, [], []);

        var read = ReplayFormat.Read(ReplayFormat.Write(replay));

        Assert.Empty(read.Commands);
        Assert.Empty(read.Checksums);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read, TestRules.Standard).Status);
    }
}
