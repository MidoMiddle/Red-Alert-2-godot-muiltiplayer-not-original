using System.Buffers.Binary;
using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Replays;

public sealed class ReplayTests
{
    /// <summary>Plays a seeded match with random commands and records it, like the client will.</summary>
    private static (Replay Replay, GameSimulation Live) RecordMatch(int ticks = 600, ulong seed = 2026)
    {
        var simulation = new GameSimulation(new SimulationSettings(seed));
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

        var result = ReplayPlayer.Verify(replay);

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
        Assert.Equal(replay.Settings, read.Settings);
        Assert.Equal(replay.TickCount, read.TickCount);
        Assert.Equal(replay.Commands, read.Commands);
        Assert.Equal(replay.Checksums, read.Checksums);
        Assert.Equal(bytes, ReplayFormat.Write(read));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read).Status);
    }

    [Fact]
    public void File_starts_with_magic_and_versions()
    {
        var (replay, _) = RecordMatch(ticks: 15);

        var bytes = ReplayFormat.Write(replay);

        Assert.Equal("RTSR"u8.ToArray(), bytes[..4]);
        Assert.Equal(ReplayFormat.FormatVersion, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(SimulationVersion.Current, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(6)));
    }

    [Fact]
    public void Changed_command_is_reported_as_desync_on_the_first_affected_checksum()
    {
        var (replay, _) = RecordMatch();
        var index = replay.Commands.ToList().FindIndex(c => c is MoveUnitsCommand { ExecuteTick: > 100 });
        var original = (MoveUnitsCommand)replay.Commands[index];
        var tampered = new MoveUnitsCommand(original.Player, original.ExecuteTick, original.Sequence, original.Units, new WorldPos(1, 1));
        var commands = replay.Commands.ToList();
        commands[index] = tampered;

        var result = ReplayPlayer.Verify(replay with { Commands = commands });

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

        var result = ReplayPlayer.Verify(replay with { Checksums = checksums });

        Assert.Equal(ReplayStatus.Desync, result.Status);
        Assert.Equal(checksums[10].Tick, result.Expected!.Value.Tick);
    }

    [Fact]
    public void Different_rules_version_is_reported_as_incompatible()
    {
        var (replay, _) = RecordMatch(ticks: 30);

        var result = ReplayPlayer.Verify(replay with { SimVersion = SimulationVersion.Current + 1 });

        Assert.Equal(ReplayStatus.IncompatibleVersion, result.Status);
    }

    [Fact]
    public void Commands_scheduled_for_unplayed_ticks_are_not_recorded()
    {
        var simulation = new GameSimulation(new SimulationSettings(1));
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
        var simulation = new GameSimulation(new SimulationSettings(1) { HashInterval = 4 });

        Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayRecorder(simulation, checksumInterval: 6));
        _ = new ReplayRecorder(simulation, checksumInterval: 8);
    }

    [Fact]
    public void Simulation_from_replay_steps_through_the_match()
    {
        var (replay, live) = RecordMatch(ticks: 200);
        var simulation = ReplayPlayer.CreateSimulation(replay);

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
            case "negative ticks": bytes[37] = 0xFF; break;
        }

        Assert.Throws<InvalidDataException>(() => ReplayFormat.Read(bytes.ToArray()));
    }

    [Fact]
    public void Empty_match_round_trips()
    {
        var replay = new Replay(SimulationVersion.Current, new SimulationSettings(9), 0, [], []);

        var read = ReplayFormat.Read(ReplayFormat.Write(replay));

        Assert.Empty(read.Commands);
        Assert.Empty(read.Checksums);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read).Status);
    }
}
