using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>Low bridges (docs/implementation/M7_maps.md, 4.6, step 6): crossing, damage, destruction, drowned units, new paths.</summary>
public sealed class BridgeTests
{
    private const int Width = 16;
    private const int Height = 9;
    private static readonly PlayerId Blue = new(1);

    /// <summary>16 x 9 with a deep river down columns 6…9 and a bridge across it on each of <paramref name="rows"/>.</summary>
    private static MapGrid River(params int[] rows)
    {
        var land = new byte[Width * Height];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)(i % Width is >= 6 and <= 9 ? LandType.Deep : LandType.Clear);
        }

        var bridges = rows.Select(row => BridgeState.New(BridgeKind.WoodLow, new CellPos(6, row), new CellPos(9, row))).ToList();
        return MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default, null, bridges);
    }

    private static void Order(GameWorld world, Unit unit, CellPos target) =>
        CommandProcessor.Apply(world, new MoveCommand(unit.Owner, world.CurrentTick, 0, [unit.Id], target.Center));

    private static void Step(GameWorld world, List<SimEvent> events)
    {
        world.PathRequests.Process(world, events);
        MovementSystem.Update(world, events);
        world.RemoveDead();
        world.CurrentTick++;
        ReservationTests.AssertOccupancy(world);
    }

    private static bool Busy(Unit unit) => unit.MoveTarget is not null || unit.PathPending || unit.HoldTicks > 0;

    /// <summary>Runs until the unit stops; returns the cells it passed.</summary>
    private static HashSet<CellPos> RunToStop(GameWorld world, Unit unit, List<SimEvent> events)
    {
        var visited = new HashSet<CellPos>();
        for (var ticks = 0; Busy(unit); ticks++)
        {
            Assert.True(ticks < 3000, "The unit never stopped.");
            Step(world, events);
            visited.Add(unit.Position.ToCell());
        }

        return visited;
    }

    [Theory]
    [InlineData(UnitType.Conscript)]
    [InlineData(UnitType.HeavyTank)]
    [InlineData(UnitType.SupportVehicle)]
    public void Ground_units_cross_the_river_by_the_bridge(UnitType type)
    {
        var world = new GameWorld(seed: 1, River(4), TestRules.Standard);
        var unit = world.SpawnUnit(Blue, type, new CellPos(2, 2).Center);

        Order(world, unit, new CellPos(13, 6));
        var visited = RunToStop(world, unit, []);

        Assert.Equal(new CellPos(13, 6), unit.Position.ToCell());
        Assert.Contains(visited, cell => cell.Y == 4 && cell.X is >= 6 and <= 9);
    }

    [Fact]
    public void A_standing_segment_is_a_road_on_the_water_closed_to_ships()
    {
        var map = River(4);
        var cell = new CellPos(7, 4);

        Assert.Equal((LandType.Deep, LandType.Road), (map.LandAt(cell), map.SurfaceAt(cell)));
        Assert.Equal(120, map.SpeedPercent(cell, Locomotor.Wheel));
        Assert.False(map.CanEnter(cell, Locomotor.Naval));
        Assert.True(map.CanEnter(cell, Locomotor.Amphibious));
        Assert.Equal(PathStatus.Unreachable, AStar.FindPath(map, new CellPos(7, 0), new CellPos(7, 8), Locomotor.Naval).Status);
    }

    [Fact]
    public void A_destroyed_segment_is_water_again_its_units_drown_and_its_neighbours_stand()
    {
        var world = new GameWorld(seed: 1, River(4), TestRules.Standard);
        var cell = new CellPos(7, 4);
        var conscript = world.SpawnUnit(Blue, UnitType.Conscript, cell.Center, snapToSubcell: true);
        var other = world.SpawnUnit(Blue, UnitType.Conscript, cell.Center, snapToSubcell: true);
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(8, 4).Center);
        var events = new List<SimEvent>();

        Assert.True(world.DamageAtCell(cell, 200, events));
        Assert.True(world.DamageAtCell(cell, 200, events));
        Assert.Equal(new SimEvent[] { new BridgeSegmentDamaged(0, 1, cell, 400), new BridgeSegmentDamaged(0, 1, cell, 200) }, events);
        Assert.True(conscript.IsAlive);

        events.Clear();
        Assert.True(world.DamageAtCell(cell, 200, events));

        Assert.Equal((LandType.Deep, LandType.Deep), (world.Map.LandAt(cell), world.Map.SurfaceAt(cell)));
        Assert.All(Enum.GetValues<Locomotor>().Where(l => l is not (Locomotor.Naval or Locomotor.Amphibious or Locomotor.Hover)),
            l => Assert.False(world.Map.CanEnter(cell, l)));
        Assert.True(world.Map.CanEnter(cell, Locomotor.Naval));
        Assert.False(conscript.IsAlive);
        Assert.False(other.IsAlive);
        Assert.True(tank.IsAlive);
        Assert.Contains(new BridgeSegmentDestroyed(0, 1, cell), events);
        Assert.Contains(new UnitKilled(0, conscript.Id, conscript.Position), events);
        Assert.Contains(new UnitKilled(0, other.Id, other.Position), events);
        // The span across falls whole: three cells wide (three tanks side by side).
        Assert.Contains(new MapChanged(0, new CellPos(7, 3), new CellPos(7, 5)), events);
        Assert.False(world.Map.CanEnter(new CellPos(7, 3), Locomotor.Track));
        Assert.Equal(new[] { 600, 0, 600, 600 }, world.Map.Bridges[0].Segments.Select(s => s.Hp));
        Assert.Equal(PathStatus.Found, AStar.FindPath(world.Map, new CellPos(7, 0), new CellPos(7, 8), Locomotor.Naval).Status);

        // The dead keep their place until the end of the tick, then leave it; nothing more hits a fallen segment.
        world.RemoveDead();
        ReservationTests.AssertOccupancy(world);
        Assert.DoesNotContain(world.Entities, e => e.Id == conscript.Id || e.Id == other.Id);
        Assert.False(world.DamageAtCell(cell, 200, events));
        Assert.True(world.Map.ContentEquals(world.Map.Clone()));
    }

    [Fact]
    public void A_unit_on_its_way_over_a_bridge_that_falls_takes_the_other_bridge()
    {
        var world = new GameWorld(seed: 1, River(4, 7), TestRules.Standard);
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, 4).Center);
        var events = new List<SimEvent>();
        Order(world, tank, new CellPos(13, 4));
        while (tank.Position.ToCell() != new CellPos(6, 4))
        {
            Step(world, events);
        }

        // The tank is on the first segment; the third falls ahead of it.
        world.DamageAtCell(new CellPos(8, 4), 600, events);
        var visited = RunToStop(world, tank, events);

        Assert.True(tank.IsAlive);
        Assert.Equal(new CellPos(13, 4), tank.Position.ToCell());
        Assert.DoesNotContain(visited, cell => cell.X == 8 && cell.Y is >= 3 and <= 5);
        // Over the other bridge, by any of its three lanes; it may step onto it from a lane of the first one.
        Assert.All(Enumerable.Range(7, 3), x => Assert.Contains(visited, cell => cell.X == x && cell.Y is >= 6 and <= 8));
    }

    [Fact]
    public void Without_another_bridge_the_unit_stops_at_the_gap_and_lives()
    {
        var world = new GameWorld(seed: 1, River(4), TestRules.Standard);
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, 4).Center);
        var events = new List<SimEvent>();
        Order(world, tank, new CellPos(13, 4));
        while (tank.Position.ToCell() != new CellPos(6, 4))
        {
            Step(world, events);
        }

        world.DamageAtCell(new CellPos(8, 4), 600, events);
        RunToStop(world, tank, events);

        Assert.True(tank.IsAlive);
        Assert.True(tank.Position.ToCell().X < 8, $"The tank crossed the gap: {tank.Position.ToCell()}.");
        Assert.Contains(events, e => e is UnitPathFound { Status: PathStatus.Unreachable } found && found.Unit == tank.Id);
    }

    [Fact]
    public void A_map_object_on_the_cell_takes_the_damage_before_the_bridge()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Width * Height).ToArray();
        land[4 * Width + 7] = (byte)LandType.Shallow;
        var map = MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default,
            [MapObjectState.New(MapObjectKind.LampPost, new CellPos(7, 4), 0)],
            [BridgeState.New(BridgeKind.WoodLow, new CellPos(7, 4), new CellPos(7, 4))]);
        var world = new GameWorld(seed: 1, map, TestRules.Standard);
        var before = StateHasher.Compute(world);

        world.DamageAtCell(new CellPos(7, 4), 100, null);

        Assert.True(world.Map.Objects[0].Destroyed);
        Assert.Equal(600, world.Map.Bridges[0].Segments[0].Hp);
        var afterPost = StateHasher.Compute(world);
        Assert.NotEqual(before, afterPost);

        world.DamageAtCell(new CellPos(7, 4), 1, null);

        Assert.Equal(599, world.Map.Bridges[0].Segments[0].Hp);
        Assert.NotEqual(afterPost, StateHasher.Compute(world));
        Assert.True(world.Map.CanEnter(new CellPos(7, 4), Locomotor.Foot));
    }

    [Theory]
    [InlineData(2, 4, 4, 4, "cell 2,4 is not water")]
    [InlineData(6, 4, 8, 4, "no shore at 9,4")]
    [InlineData(6, 4, 16, 4, "end 16,4 is outside the 16x9 map")]
    [InlineData(6, 3, 9, 6, "6,3 to 9,6 runs neither along X nor along Y")]
    public void A_bridge_must_run_over_water_from_shore_to_shore(int x1, int y1, int x2, int y2, string problem)
    {
        var land = River().LandBytes();

        var error = Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default, null,
            [BridgeState.New(BridgeKind.WoodLow, new CellPos(x1, y1), new CellPos(x2, y2))]));

        Assert.Contains(problem, error.Message);
    }

    [Fact]
    public void Bridges_cannot_overlap_and_hit_points_must_fit_the_type()
    {
        var land = River().LandBytes();
        var blocked = new byte[land.Length];
        var full = BridgeState.New(BridgeKind.WoodLow, new CellPos(6, 4), new CellPos(9, 4));

        Assert.Contains("already has a bridge", Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, blocked, default, null,
            [full, BridgeState.New(BridgeKind.WoodLow, new CellPos(9, 4), new CellPos(6, 4))])).Message);
        Assert.Contains("hit points", Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, blocked, default, null,
            [full with { SegmentHp = [600, 601, 600, 600] }])).Message);
        Assert.Contains("4 segments", Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, blocked, default, null,
            [full with { SegmentHp = [600] }])).Message);
    }

    [Fact]
    public void A_bridge_runs_either_way_and_a_one_cell_bridge_needs_a_shore_along_x_or_along_y()
    {
        var bridge = Assert.Single(TestMapParser.Parse("""
            . . . . .
            ~ ~ ~ ~ ~
            . . . . .
            bridge bridge_wood_low 3 1 3 1
            """).Grid.Bridges);
        Assert.Equal(new[] { new CellPos(3, 1) }, bridge.Segments.Select(s => s.Cell));

        var reversed = River().Clone();
        Assert.Null(reversed.TryAddBridge(BridgeState.New(BridgeKind.WoodLow, new CellPos(9, 2), new CellPos(6, 2))));
        Assert.Equal(new[] { 9, 8, 7, 6 }, reversed.Bridges[0].Segments.Select(s => s.Cell.X));
    }

    [Theory]
    [InlineData("bridge bridge_wood_low 1 0 2 0", "line 3, column 24: cell 1,0 is not water")]
    [InlineData("bridge bridge_stone 2 0 3 0", "line 3, column 8: unknown bridge 'bridge_stone'")]
    [InlineData("bridge bridge_wood_low 2 0", "line 3, column 1: expected 'bridge <type> <x1> <y1> <x2> <y2>'")]
    public void A_bad_bridge_line_names_its_line(string line, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => TestMapParser.Parse($". . ~ ~ . .\n. . ~ ~ . .\n{line}\n"));

        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void Bridges_and_their_damage_are_in_the_hash_and_the_copy()
    {
        var world = new GameWorld(seed: 1, River(4, 7), TestRules.Standard);
        var before = StateHasher.Compute(world);
        var copy = world.Map.Clone();

        world.DamageAtCell(new CellPos(9, 7), 1, null);

        Assert.NotEqual(before, StateHasher.Compute(world));
        Assert.False(copy.ContentEquals(world.Map));
        Assert.True(world.Map.Clone().ContentEquals(world.Map));
        Assert.Equal(599, world.Map.Clone().BridgeAt(new CellPos(9, 7))!.Hp);
    }

    [Fact]
    public void Bridges_and_a_match_that_blows_one_up_survive_the_replay()
    {
        // The match starts with a bridge already broken in the middle and a damaged one.
        var start = River();
        Assert.Null(start.TryAddBridge(new BridgeState(BridgeKind.WoodLow, new CellPos(6, 2), new CellPos(9, 2), [600, 0, 250, 600])));
        Assert.Null(start.TryAddBridge(BridgeState.New(BridgeKind.WoodLow, new CellPos(6, 6), new CellPos(9, 6))));
        var simulation = new GameSimulation(new SimulationSettings(seed: 7, start), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 0, UnitType.HeavyTank, new CellPos(2, 6).Center));
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 1, UnitType.Conscript, new CellPos(2, 5).Center));
        simulation.Enqueue(new MoveCommand(Blue, 1, 2, [new EntityId(1), new EntityId(2)], new CellPos(13, 6).Center));
        for (var tick = 40; tick < 43; tick++)
        {
            simulation.Enqueue(new DamageMapObjectCommand(Blue, tick, (uint)tick, new CellPos(9, 6), 200));
        }

        var destroyed = false;
        for (var tick = 0; tick < 400; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
            destroyed |= simulation.LastTickEvents.OfType<BridgeSegmentDestroyed>().Any();
        }

        Assert.True(destroyed);
        var bytes = ReplayFormat.Write(recorder.Build());
        var read = ReplayFormat.Read(bytes);

        Assert.True(read.Settings.Map!.ContentEquals(start));
        Assert.Equal(0, read.Settings.Map.BridgeAt(new CellPos(7, 2))!.Hp);
        Assert.Equal(bytes, ReplayFormat.Write(read));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read, TestRules.Standard).Status);
    }

    [Fact]
    public void A_bridge_is_three_cells_wide_where_there_is_water_and_three_tanks_cross_side_by_side()
    {
        var world = new GameWorld(seed: 1, River(4), TestRules.Standard);
        var segment = world.Map.Bridges[0].Segments[1];
        Assert.Equal(new[] { new CellPos(7, 4), new CellPos(7, 3), new CellPos(7, 5) }, segment.Cells);
        Assert.All(segment.Cells, cell => Assert.Same(segment, world.Map.BridgeAt(cell)));
        Assert.All(segment.Cells, cell => Assert.Equal(LandType.Road, world.Map.SurfaceAt(cell)));
        Assert.False(world.Map.CanEnter(new CellPos(7, 5), Locomotor.Naval));

        var tanks = new[] { 3, 4, 5 }.Select(row => world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(4, row).Center)).ToList();
        var events = new List<SimEvent>();
        for (var i = 0; i < tanks.Count; i++)
        {
            Order(world, tanks[i], new CellPos(12, 3 + i));
        }

        for (var tick = 0; tick < 600 && tanks.Exists(Busy); tick++)
        {
            Step(world, events);
        }

        Assert.Equal(new[] { new CellPos(12, 3), new CellPos(12, 4), new CellPos(12, 5) }, tanks.Select(t => t.Position.ToCell()));
    }

    [Fact]
    public void A_bridge_beside_the_shore_takes_only_the_water_cells_beside_it()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Width * Height).ToArray();
        for (var x = 6; x <= 9; x++)
        {
            land[4 * Width + x] = (byte)LandType.Deep;
            land[5 * Width + x] = (byte)LandType.Deep;
        }

        var map = MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default, null,
            [BridgeState.New(BridgeKind.WoodLow, new CellPos(6, 4), new CellPos(9, 4))]);
        Assert.All(map.Bridges[0].Segments, segment => Assert.Equal(new[] { segment.Cell, new CellPos(segment.Cell.X, 5) }, segment.Cells));
    }
}
