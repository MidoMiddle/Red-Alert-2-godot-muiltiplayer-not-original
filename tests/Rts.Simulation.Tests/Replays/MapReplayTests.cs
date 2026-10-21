using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Replays;

/// <summary>Matches on a map: settings carry it, the replay (format v3) stores it, playback uses it.</summary>
public sealed class MapReplayTests
{
    // "RTSR" | u16 format | u32 rules | u64 seed | 5 × i32.
    private const int MapFlagOffset = 4 + 2 + 4 + 8 + 4 * 5;

    private static TestMap LandTypes() => TestMapParserTests.Load("all_land_types.txt");

    /// <summary>Spawns the map's units on tick 0 and orders player 1 across the water and back, like the sandbox.</summary>
    private static Replay RecordOnMap(TestMap map, int ticks = 300)
    {
        var simulation = new GameSimulation(new SimulationSettings(seed: 3, map.Grid));
        var recorder = new ReplayRecorder(simulation);
        var sequences = new uint[3];
        foreach (var unit in map.Units)
        {
            simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequences[unit.Owner.Value]++, unit.Type, unit.Cell.Center));
        }

        var all = Enumerable.Range(1, map.Units.Count).Select(id => new EntityId(id)).ToImmutableArray();
        simulation.Enqueue(new MoveUnitsCommand(new PlayerId(1), 1, sequences[1]++, all, new CellPos(13, 6).Center));
        simulation.Enqueue(new MoveUnitsCommand(new PlayerId(1), 150, sequences[1]++, all, new CellPos(1, 10).Center));

        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        return recorder.Build();
    }

    [Fact]
    public void Settings_with_a_map_take_its_size()
    {
        var map = LandTypes().Grid;

        var settings = new SimulationSettings(seed: 1, map);

        Assert.Same(map, settings.Map);
        Assert.Equal((24, 12), (settings.MapWidthCells, settings.MapHeightCells));
    }

    [Fact]
    public void Map_size_must_match_the_settings()
    {
        var settings = new SimulationSettings(seed: 1, LandTypes().Grid) { MapWidthCells = 64 };

        Assert.Throws<ArgumentException>(() => new GameSimulation(settings));
    }

    [Fact]
    public void World_plays_on_a_copy_of_the_settings_map()
    {
        var map = LandTypes().Grid;
        var simulation = new GameSimulation(new SimulationSettings(seed: 1, map));

        simulation.World.Map.SetBlocked(new CellPos(0, 0), true);

        Assert.NotSame(map, simulation.World.Map);
        Assert.False(map.IsBlocked(new CellPos(0, 0)));
        Assert.Equal(LandType.Deep, simulation.World.Map.LandAt(new CellPos(12, 0)));
    }

    [Fact]
    public void Settings_compare_maps_by_content()
    {
        var a = new SimulationSettings(seed: 1, LandTypes().Grid);
        var b = new SimulationSettings(seed: 1, LandTypes().Grid);
        var changed = LandTypes().Grid;
        changed.SetLand(new CellPos(0, 0), LandType.Ice);

        Assert.Equal(a, b);
        Assert.NotEqual(a, new SimulationSettings(seed: 1, changed));
        Assert.NotEqual(a, new SimulationSettings(seed: 1) { MapWidthCells = 24, MapHeightCells = 12 });
    }

    [Fact]
    public void Replay_on_a_map_round_trips_and_verifies()
    {
        var replay = RecordOnMap(LandTypes());

        var bytes = ReplayFormat.Write(replay);
        var read = ReplayFormat.Read(bytes);

        Assert.Equal(1, bytes[MapFlagOffset]);
        Assert.Equal(replay.Settings, read.Settings);
        Assert.True(read.Settings.Map!.ContentEquals(LandTypes().Grid));
        Assert.Equal(bytes, ReplayFormat.Write(read));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read).Status);
    }

    [Fact]
    public void Replay_without_a_map_has_the_flag_cleared()
    {
        var replay = new Replay(SimulationVersion.Current, new SimulationSettings(9), 0, [], []);

        var bytes = ReplayFormat.Write(replay);

        Assert.Equal(0, bytes[MapFlagOffset]);
        Assert.Null(ReplayFormat.Read(bytes).Settings.Map);
    }

    [Theory]
    [InlineData("flag")]
    [InlineData("land")]
    [InlineData("obstacle")]
    [InlineData("truncated")]
    public void Damaged_map_data_is_rejected(string damage)
    {
        var bytes = ReplayFormat.Write(RecordOnMap(LandTypes(), ticks: 10));
        var cells = 24 * 12;
        switch (damage)
        {
            case "flag": bytes[MapFlagOffset] = 2; break;
            case "land": bytes[MapFlagOffset + 1] = 0; break;
            case "obstacle": bytes[MapFlagOffset + 1 + cells] = 7; break;
            case "truncated": bytes = bytes[..(MapFlagOffset + 1 + cells)]; break;
        }

        Assert.Throws<InvalidDataException>(() => ReplayFormat.Read(bytes));
    }
}
