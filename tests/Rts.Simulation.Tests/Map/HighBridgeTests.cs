using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Movement;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>
/// High bridges (docs/implementation/M7_maps.md, 4.6, 2026-10-26): the rules they stand by, the two levels of the step rule, crossing
/// on the deck and passing under it, the fall of a segment, sight from the deck.
/// </summary>
public sealed class HighBridgeTests
{
    private const int Width = 20;
    private const int Height = 12;
    private const int Row = 5;
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Red = new(2);

    /// <summary>
    /// 20 x 12: a plateau at level 4 west of corner column 6 and east of 14, a valley at level 0 between them with a deep river down
    /// columns 9 and 10, and a high bridge along row 5 from cell 6 to cell 13 (rows 4…6 across). Cells 6 and 13 are the cliffs.
    /// </summary>
    internal static MapGrid Gorge(int plateau = 4, int eastPlateau = 4, bool bridge = true, CellPos? from = null, CellPos? to = null)
    {
        var land = new byte[Width * Height];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)(i % Width is 9 or 10 ? LandType.Deep : LandType.Clear);
        }

        var heights = new byte[(Width + 1) * (Height + 1)];
        for (var y = 0; y <= Height; y++)
        {
            for (var x = 0; x <= Width; x++)
            {
                heights[y * (Width + 1) + x] = (byte)(x <= 6 ? plateau : x >= 14 ? eastPlateau : 0);
            }
        }

        var bridges = bridge
            ? new[] { BridgeState.New(TestRules.Standard.MapTypes.Get(BridgeKind.ConcreteHigh), from ?? new CellPos(6, Row), to ?? new CellPos(13, Row)) }
            : [];
        return MapGrid.FromLayers(Width, Height, land, new byte[land.Length], heights, null, bridges, types: TestRules.Standard.MapTypes);
    }

    private static string Refusal(Func<MapGrid> build) => Assert.Throws<ArgumentException>(build).Message;

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

    /// <summary>Runs until every unit stops; returns the cells each passed and on which level.</summary>
    private static Dictionary<EntityId, List<(CellPos Cell, bool OnDeck)>> RunToStop(GameWorld world, params Unit[] units)
    {
        var visited = units.ToDictionary(unit => unit.Id, _ => new List<(CellPos, bool)>());
        for (var ticks = 0; units.Any(unit => unit.IsAlive && Busy(unit)); ticks++)
        {
            Assert.True(ticks < 3000, "A unit never stopped.");
            Step(world, []);
            foreach (var unit in units)
            {
                visited[unit.Id].Add((unit.Position.ToCell(), unit.OnDeck));
            }
        }

        return visited;
    }

    [Fact]
    public void The_bridge_lays_a_deck_three_cells_wide_at_the_level_of_its_shores()
    {
        var map = Gorge();
        var bridge = Assert.Single(map.Bridges);

        Assert.True(bridge.Type.High);
        Assert.Equal(1500, bridge.Type.SegmentHp);
        Assert.Equal(8 * 3, map.DeckCount);
        Assert.Equal(Width * Height + 24, map.NodeCount);
        for (var x = 6; x <= 13; x++)
        {
            for (var y = Row - 1; y <= Row + 1; y++)
            {
                Assert.True(map.IsDeck(new CellPos(x, y)));
                Assert.Equal(4, map.DeckLevel(new CellPos(x, y)));
            }
        }

        Assert.False(map.IsDeck(new CellPos(8, Row - 2)));
        Assert.False(map.IsDeck(new CellPos(5, Row)));
        // Below, the ground is what it was: the valley, the river; on the deck a road.
        Assert.Equal(LandType.Clear, map.SurfaceAt(new CellPos(8, Row)));
        Assert.Equal(LandType.Deep, map.SurfaceAt(new CellPos(9, Row)));
        Assert.True(map.CanEnter(new CellPos(8, Row), Locomotor.Track));
        Assert.True(map.CanEnter(new CellPos(9, Row), Locomotor.Naval));
        Assert.True(Decks.SpeedPercent(map, new CellPos(9, Row), true, Locomotor.Wheel) > 0);
        Assert.Equal(Decks.SpeedPercent(map, new CellPos(9, Row), true, Locomotor.Wheel), Decks.SpeedPercent(map, new CellPos(7, Row), true, Locomotor.Wheel));
        Assert.Equal(0, Decks.SpeedPercent(map, new CellPos(9, Row), true, Locomotor.Naval));
    }

    [Fact]
    public void A_bridge_breaking_a_rule_does_not_load()
    {
        Assert.Contains("three cells or more", Refusal(() => Gorge(from: new CellPos(6, Row), to: new CellPos(7, Row))));
        Assert.Contains("levels 4 and 3", Refusal(() => Gorge(eastPlateau: 3)));
        Assert.Contains("level 2 or higher", Refusal(() => Gorge(plateau: 1, eastPlateau: 1)));
        // From cell 5 the first cell of the deck is the plateau itself: one could drive onto it from the side.
        Assert.Contains("not low enough", Refusal(() => Gorge(from: new CellPos(5, Row), to: new CellPos(13, Row))));
        // Ending at 12, the shore beyond is the cliff.
        Assert.Contains("no shore at 13,", Refusal(() => Gorge(from: new CellPos(6, Row), to: new CellPos(12, Row))));
        // Too near the edge for three lanes.
        Assert.Contains("no shore", Refusal(() => Gorge(from: new CellPos(6, 0), to: new CellPos(13, 0))));
    }

    [Fact]
    public void A_deck_over_a_gentle_slope_does_not_load()
    {
        // The valley rises gently to the west plateau: the end cell is a slope one could climb onto the deck from. (An end cell that
        // shares its corners with a flat shore and has one two levels lower is always a cliff, so this is the rule that keeps it so.)
        var land = Enumerable.Repeat((byte)LandType.Clear, Width * Height).ToArray();
        var heights = new byte[(Width + 1) * (Height + 1)];
        for (var y = 0; y <= Height; y++)
        {
            for (var x = 0; x <= Width; x++)
            {
                heights[y * (Width + 1) + x] = (byte)(x <= 6 ? 4 : x == 7 ? 3 : x >= 14 ? 4 : 0);
            }
        }

        var error = Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, new byte[land.Length], heights, null,
            [BridgeState.New(TestRules.Standard.MapTypes.Get(BridgeKind.ConcreteHigh), new CellPos(6, Row), new CellPos(13, Row))], types: TestRules.Standard.MapTypes));
        Assert.Contains("is not low enough", error.Message);
    }

    [Fact]
    public void The_deck_is_reached_only_straight_from_its_shores_and_left_only_onto_them()
    {
        var map = Gorge();

        // On from the west shore, on every lane; never from the side, never from the valley.
        Assert.True(Decks.TryStep(map, new CellPos(5, Row), false, 1, 0, Locomotor.Track, out var onDeck) && onDeck);
        Assert.True(Decks.TryStep(map, new CellPos(5, Row + 1), false, 1, 0, Locomotor.Track, out onDeck) && onDeck);
        Assert.False(Decks.TryStep(map, new CellPos(5, Row - 2), false, 1, 1, Locomotor.Track, out _));
        Assert.True(Decks.TryStep(map, new CellPos(8, Row - 2), false, 0, 1, Locomotor.Track, out onDeck) && !onDeck);
        Assert.False(Decks.TryEnter(map, new CellPos(5, Row), false, new CellPos(6, Row - 1), Locomotor.Track, out _));
        // Ships go under, never up.
        Assert.True(Decks.TryStep(map, new CellPos(9, Row - 2), false, 0, 1, Locomotor.Naval, out onDeck) && !onDeck);

        // Along the deck and across its lanes; a diagonal only with deck on both sides of the corner.
        Assert.True(Decks.TryStep(map, new CellPos(8, Row), true, 1, 1, Locomotor.Track, out onDeck) && onDeck);
        Assert.False(Decks.TryStep(map, new CellPos(8, Row + 1), true, 1, 1, Locomotor.Track, out _));
        Assert.False(Decks.TryStep(map, new CellPos(8, Row + 1), true, 0, 1, Locomotor.Track, out _));
        // Off the end onto its shore, and nowhere else.
        Assert.True(Decks.TryStep(map, new CellPos(13, Row), true, 1, 0, Locomotor.Track, out onDeck) && !onDeck);
        Assert.False(Decks.TryStep(map, new CellPos(13, Row), true, 1, 1, Locomotor.Track, out _));
        Assert.True(Decks.TryStep(map, new CellPos(12, Row), true, 1, 0, Locomotor.Track, out onDeck) && onDeck);
    }

    [Fact]
    public void Paths_go_over_the_deck_and_under_it()
    {
        var map = Gorge();

        var over = AStar.FindPath(map, new CellPos(2, Row), new CellPos(17, Row), Locomotor.Track);
        Assert.Equal(PathStatus.Found, over.Status);
        Assert.True(Decks.Levels(map, over.Cells, false, Locomotor.Track).Count(level => level) >= 8);
        // The tanks of the valley: along it, under the deck.
        var under = AStar.FindPath(map, new CellPos(8, 0), new CellPos(8, Height - 1), Locomotor.Track);
        Assert.Equal(PathStatus.Found, under.Status);
        Assert.DoesNotContain(true, Decks.Levels(map, under.Cells, false, Locomotor.Track));
        Assert.Equal(PathStatus.Found, AStar.FindPath(map, new CellPos(9, 0), new CellPos(9, Height - 1), Locomotor.Naval).Status);
        // A path that starts on the deck stays on it until an end: no stepping off into the valley.
        var off = AStar.FindPath(map, new CellPos(8, Row), new CellPos(17, 1), Locomotor.Track, startOnDeck: true);
        Assert.Equal(PathStatus.Found, off.Status);
        var levels = Decks.Levels(map, off.Cells, true, Locomotor.Track);
        Assert.All(Enumerable.Range(0, off.Cells.Length).Where(i => off.Cells[i].X is >= 6 and <= 13), i => Assert.True(levels[i], $"{off.Cells[i]} below"));
        Assert.Equal(PathStatus.Unreachable, AStar.FindPath(map, new CellPos(8, Row), new CellPos(8, Height - 1), Locomotor.Track, startOnDeck: true).Status);
        // The smoothed way keeps the steps on and off the deck as waypoints.
        var smooth = PathSmoother.Smooth(map, over, Locomotor.Track);
        Assert.Contains(new CellPos(5, Row), smooth.Waypoints);
        Assert.Contains(new CellPos(6, Row), smooth.Waypoints);
        Assert.Contains(new CellPos(14, Row), smooth.Waypoints);
    }

    [Fact]
    public void Without_high_bridges_a_map_has_no_deck_nodes()
    {
        var map = Gorge(bridge: false);

        Assert.Equal(0, map.DeckCount);
        Assert.Equal(Width * Height, map.NodeCount);
        Assert.Equal(PathStatus.Unreachable, AStar.FindPath(map, new CellPos(2, Row), new CellPos(17, Row), Locomotor.Track).Status);
    }

    [Fact]
    public void A_tank_drives_over_the_deck_while_valley_tanks_pass_under_it()
    {
        var world = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var over = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row).Center);
        var under = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(8, 1).Center);
        var across = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(11, 10).Center);
        Order(world, over, new CellPos(17, Row));
        Order(world, under, new CellPos(8, 10));
        Order(world, across, new CellPos(11, 1));

        var visited = RunToStop(world, over, under, across);

        Assert.Equal(new CellPos(17, Row), over.Position.ToCell());
        Assert.Equal(new CellPos(8, 10), under.Position.ToCell());
        Assert.Equal(new CellPos(11, 1), across.Position.ToCell());
        Assert.False(over.OnDeck);
        Assert.Contains((new CellPos(9, Row), true), visited[over.Id]);
        Assert.Contains((new CellPos(8, Row), false), visited[under.Id]);
        Assert.Contains((new CellPos(11, Row), false), visited[across.Id]);
        Assert.DoesNotContain(visited[under.Id], place => place.OnDeck);
    }

    [Fact]
    public void A_tank_stopped_on_the_deck_holds_its_place_there_and_a_tank_below_takes_the_same_cell()
    {
        var world = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var top = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row).Center);
        Order(world, top, new CellPos(8, Row));
        RunToStop(world, top);
        Assert.Equal(new CellPos(8, Row), top.Position.ToCell());
        Assert.True(top.OnDeck);
        Assert.Equal(top.Id, world.Reservations.VehicleAt(new CellPos(8, Row), onDeck: true));
        Assert.Equal(EntityId.None, world.Reservations.VehicleAt(new CellPos(8, Row)));

        var below = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(8, 1).Center);
        Order(world, below, new CellPos(8, Row));
        RunToStop(world, below);

        Assert.Equal(new CellPos(8, Row), below.Position.ToCell());
        Assert.False(below.OnDeck);
        Assert.Equal(below.Id, world.Reservations.VehicleAt(new CellPos(8, Row)));
        Assert.Equal(top.Id, world.Reservations.VehicleAt(new CellPos(8, Row), onDeck: true));
    }

    [Fact]
    public void Tanks_meeting_on_the_deck_get_past_each_other()
    {
        var world = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var west = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(3, Row).Center);
        var east = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(16, Row).Center);
        Order(world, west, new CellPos(17, Row));
        Order(world, east, new CellPos(2, Row));

        RunToStop(world, west, east);

        Assert.Equal(new CellPos(17, Row), west.Position.ToCell());
        Assert.Equal(new CellPos(2, Row), east.Position.ToCell());
    }

    [Fact]
    public void A_fallen_segment_kills_those_on_its_deck_and_spares_those_below()
    {
        var world = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var top = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row).Center);
        Order(world, top, new CellPos(8, Row));
        RunToStop(world, top);
        var below = world.SpawnUnit(Red, UnitType.HeavyTank, new CellPos(8, Row).Center);
        Assert.False(below.OnDeck);
        Assert.Equal(below.Id, world.Reservations.VehicleAt(new CellPos(8, Row)));

        var events = new List<SimEvent>();
        var segment = world.Map.BridgeAt(new CellPos(8, Row))!;
        world.DestroyBridgeSegment(segment, events);

        Assert.False(top.IsAlive);
        Assert.True(below.IsAlive);
        Assert.Contains(events, e => e is UnitKilled killed && killed.Unit == top.Id);
        Assert.DoesNotContain(events, e => e is UnitKilled killed && killed.Unit == below.Id);
        Assert.Equal(0, Decks.SpeedPercent(world.Map, new CellPos(8, Row), true, Locomotor.Track));
        // Both plateaus are cut off from each other now; the valley still runs under the gap.
        Assert.Equal(PathStatus.Unreachable, AStar.FindPath(world.Map, new CellPos(2, Row), new CellPos(17, Row), Locomotor.Track).Status);
        Assert.Equal(PathStatus.Found, AStar.FindPath(world.Map, new CellPos(8, 0), new CellPos(8, Height - 1), Locomotor.Track).Status);
    }

    [Fact]
    public void A_high_bridge_comes_down_whole_whichever_segment_is_hit()
    {
        var world = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var top = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row).Center);
        Order(world, top, new CellPos(8, Row));
        RunToStop(world, top);
        var below = world.SpawnUnit(Red, UnitType.HeavyTank, new CellPos(11, Row).Center);
        var bridge = world.Map.Bridges[0];
        Assert.True(top.OnDeck);

        // A segment far from the tank: the whole deck falls, no span is left hanging between gaps; only the piers stay.
        var events = new List<SimEvent>();
        world.DestroyBridgeSegment(world.Map.BridgeAt(new CellPos(12, Row))!, events);

        Assert.All(bridge.Segments, segment => Assert.False(segment.IsIntact));
        Assert.Equal(bridge.Segments.Count, events.Count(e => e is BridgeSegmentDestroyed));
        Assert.Equal(new CellPos(12, Row), events.OfType<BridgeSegmentDestroyed>().First().Cell);
        Assert.False(top.IsAlive);
        Assert.True(below.IsAlive);
        Assert.Equal(0, Decks.SpeedPercent(world.Map, new CellPos(8, Row), true, Locomotor.Track));
        Assert.Equal(PathStatus.Found, AStar.FindPath(world.Map, new CellPos(8, 0), new CellPos(8, Height - 1), Locomotor.Track).Status);
    }

    [Fact]
    public void From_the_deck_a_unit_sees_as_from_its_level()
    {
        var world = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row).Center);
        Order(world, tank, new CellPos(8, Row));
        RunToStop(world, tank);
        var valley = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(8, 1).Center);

        Assert.Equal(tank.Archetype.Sight + SightRules.HeightBonus(4), SightRules.SightOf(tank, world.Map));
        Assert.Equal(valley.Archetype.Sight, SightRules.SightOf(valley, world.Map));
    }

    [Fact]
    public void The_hash_tells_a_tank_on_the_deck_from_one_below()
    {
        var onTop = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var tank = onTop.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row).Center);
        Order(onTop, tank, new CellPos(8, Row));
        RunToStop(onTop, tank);
        Assert.True(tank.OnDeck);

        var below = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var other = below.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row).Center);
        Order(below, other, new CellPos(8, Row));
        RunToStop(below, other);
        below.Reservations.Leave(new CellPos(8, Row), other);
        other.OnDeck = false;
        below.Reservations.TryEnter(new CellPos(8, Row), other, other.Position);

        Assert.Equal(onTop.CurrentTick, below.CurrentTick);
        Assert.NotEqual(StateHasher.Compute(onTop), StateHasher.Compute(below));
    }

    [Fact]
    public void A_match_over_the_bridge_and_under_it_replays()
    {
        var simulation = new GameSimulation(new SimulationSettings(seed: 3, Gorge()), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 0, UnitType.HeavyTank, new CellPos(2, Row).Center));
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 1, UnitType.HeavyTank, new CellPos(8, 1).Center));
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 2, UnitType.Conscript, new CellPos(17, Row).Center));
        simulation.Enqueue(new MoveCommand(Blue, 1, 3, [new EntityId(1)], new CellPos(17, Row - 1).Center));
        simulation.Enqueue(new MoveCommand(Blue, 1, 4, [new EntityId(2)], new CellPos(8, 10).Center));
        simulation.Enqueue(new MoveCommand(Blue, 1, 5, [new EntityId(3)], new CellPos(9, Row).Center));
        for (var tick = 0; tick < 400; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var bytes = ReplayFormat.Write(recorder.Build());
        var read = ReplayFormat.Read(bytes, TestRules.Standard.MapTypes);

        Assert.True(read.Settings.Map!.ContentEquals(Gorge()));
        Assert.Equal(Gorge().DeckCount, read.Settings.Map.DeckCount);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read, TestRules.Standard).Status);
    }

    [Fact]
    public void A_group_sent_onto_the_deck_drives_up_and_stands_on_it()
    {
        var world = new GameWorld(seed: 1, Gorge(), TestRules.Standard);
        var tanks = Enumerable.Range(0, 3).Select(i => world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, Row - 1 + i).Center)).ToArray();
        CommandProcessor.Apply(world, new MoveCommand(Blue, world.CurrentTick, 0, [.. tanks.Select(t => t.Id)], new CellPos(9, Row).Center));

        RunToStop(world, tanks);

        Assert.All(tanks, tank => Assert.True(tank.OnDeck, $"{tank.Id} stopped on {tank.Position.ToCell()} below."));
    }
}
