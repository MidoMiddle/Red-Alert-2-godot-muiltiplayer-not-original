using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>The map changing during a match (docs/implementation/M7_maps.md, 4.4, step 4): events, A* costs, searches, paths.</summary>
public sealed class MapChangeTests
{
    private static readonly PlayerId Blue = new(1);

    /// <summary>A world with a wall of trees down column <paramref name="wallX"/>, no gap.</summary>
    private static GameWorld Walled(int width, int height, int wallX)
    {
        var map = new MapGrid(width, height);
        for (var y = 0; y < height; y++)
        {
            map.SetBlocked(new CellPos(wallX, y), true);
        }

        return new GameWorld(seed: 1, map);
    }

    private static void Order(GameWorld world, Unit unit, CellPos target) =>
        CommandProcessor.Apply(world, new MoveCommand(unit.Owner, world.CurrentTick, 0, [unit.Id], target.Center));

    private static List<SimEvent> Step(GameWorld world, int ticks = 1, Action<GameWorld>? everyTick = null)
    {
        var events = new List<SimEvent>();
        for (var i = 0; i < ticks; i++)
        {
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.RemoveDead();
            world.CurrentTick++;
            ReservationTests.AssertOccupancy(world);
            everyTick?.Invoke(world);
        }

        return events;
    }

    private static void RunUntilStill(GameWorld world, Unit unit, Action<GameWorld>? everyTick = null)
    {
        for (var ticks = 0; unit.MoveTarget is not null || unit.PathPending || unit.HoldTicks > 0; ticks++)
        {
            Assert.True(ticks < 3000, "The unit never stopped.");
            Step(world, 1, everyTick);
        }
    }

    [Fact]
    public void A_felled_tree_opens_a_way_for_the_next_order()
    {
        var world = Walled(12, 7, wallX: 6);
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, 3).Center);
        Order(world, tank, new CellPos(10, 3));
        RunUntilStill(world, tank);
        Assert.Equal(5, tank.Position.ToCell().X);

        var events = new List<SimEvent>();
        world.ChangeMap([new SetObstacleEdit(new CellPos(6, 3), false)], events);
        Order(world, tank, new CellPos(10, 3));
        var visited = new HashSet<CellPos>();
        RunUntilStill(world, tank, _ => visited.Add(tank.Position.ToCell()));

        Assert.Equal(new MapChanged(0, new CellPos(6, 3), new CellPos(6, 3)), Assert.Single(events) with { Tick = 0 });
        Assert.Equal(new CellPos(10, 3), tank.Position.ToCell());
        Assert.Contains(new CellPos(6, 3), visited);
    }

    [Fact]
    public void A_tree_that_grows_on_the_way_makes_the_unit_find_another()
    {
        var world = new GameWorld(seed: 1, new MapGrid(16, 8));
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(1, 3).Center);
        Order(world, tank, new CellPos(14, 3));
        Step(world, 5);
        Assert.NotNull(tank.Path);
        Assert.Contains(new CellPos(9, 3), tank.Path!.Cells);

        world.ChangeMap([new SetObstacleEdit(new CellPos(9, 3), true)], null);
        Assert.True(tank.PathPending, "A path crossing the new tree is dropped at once.");
        var visited = new HashSet<CellPos>();
        RunUntilStill(world, tank, _ => visited.Add(tank.Position.ToCell()));

        Assert.Equal(new CellPos(14, 3), tank.Position.ToCell());
        Assert.DoesNotContain(new CellPos(9, 3), visited);
        Assert.Equal(0, tank.Replans);
    }

    [Fact]
    public void A_cliff_raised_on_the_way_is_driven_around()
    {
        var world = new GameWorld(seed: 1, new MapGrid(16, 8));
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(1, 3).Center);
        Order(world, tank, new CellPos(14, 3));
        Step(world, 5);

        // A corner two levels up turns the four cells around it into cliffs, two of them on the way.
        world.ChangeMap([new SetCornerLevelEdit(9, 4, 2)], null);
        var visited = new HashSet<CellPos>();
        RunUntilStill(world, tank, _ => visited.Add(tank.Position.ToCell()));

        Assert.Equal(new CellPos(14, 3), tank.Position.ToCell());
        Assert.DoesNotContain(visited, cell => world.Map.Heights.ShapeAt(cell) == CellShape.Cliff);
    }

    [Fact]
    public void A_change_elsewhere_or_a_cell_that_opens_leaves_paths_alone()
    {
        var world = new GameWorld(seed: 1, new MapGrid(16, 8));
        world.Map.SetBlocked(new CellPos(8, 6), true);
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(1, 3).Center);
        Order(world, tank, new CellPos(14, 3));
        Step(world, 5);
        var path = tank.Path;

        world.ChangeMap([new SetObstacleEdit(new CellPos(8, 6), false), new SetObstacleEdit(new CellPos(3, 7), true)], null);

        Assert.Same(path, tank.Path);
        Assert.False(tank.PathPending);
    }

    [Fact]
    public void A_search_under_way_starts_again_on_the_changed_map()
    {
        var world = Walled(30, 12, wallX: 15);
        world.Map.SetBlocked(new CellPos(15, 2), false);
        world.Map.SetBlocked(new CellPos(15, 9), false);
        world.PathRequests.Budget = 10;
        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, 2).Center);
        Order(world, tank, new CellPos(27, 2));
        Step(world, 1);
        Assert.True(tank.PathPending, "The search is still under way.");

        // The near gap closes while the search runs: the path must take the far one.
        world.ChangeMap([new SetObstacleEdit(new CellPos(15, 2), true)], null);
        var visited = new HashSet<CellPos>();
        RunUntilStill(world, tank, _ => visited.Add(tank.Position.ToCell()));

        Assert.Equal(new CellPos(27, 2), tank.Position.ToCell());
        Assert.Contains(new CellPos(15, 9), visited);
    }

    [Fact]
    public void Patched_step_costs_equal_costs_built_from_scratch()
    {
        var random = new TestRandom(17);
        var world = new GameWorld(seed: 1, new MapGrid(20, 14));
        var pathfinder = world.PathRequests.Pathfinder;
        foreach (var locomotor in Enum.GetValues<Locomotor>())
        {
            pathfinder.CostsFor(locomotor);
        }

        for (var change = 0; change < 60; change++)
        {
            MapEdit edit = random.NextInt(3) switch
            {
                0 => new SetLandEdit(new CellPos(random.NextInt(20), random.NextInt(14)), (LandType)(1 + random.NextInt(8))),
                1 => new SetObstacleEdit(new CellPos(random.NextInt(20), random.NextInt(14)), random.NextInt(3) == 0),
                _ => new SetCornerLevelEdit(random.NextInt(21), random.NextInt(15), random.NextInt(4)),
            };
            world.ChangeMap([edit], null);
        }

        foreach (var locomotor in Enum.GetValues<Locomotor>())
        {
            var patched = pathfinder.CostsFor(locomotor);
            var fresh = new StepCostLayer(world.Map, locomotor);
            Assert.Equal(fresh.Straight, patched.Straight);
            Assert.Equal(fresh.Diagonal, patched.Diagonal);
        }
    }

    [Fact]
    public void A_match_with_map_changes_replays_to_the_same_checksums()
    {
        Assert.Equal(Play(), Play());

        static List<ulong> Play()
        {
            var simulation = new GameSimulation(new SimulationSettings(9, new MapGrid(24, 16)));
            uint sequence = 0;
            for (var i = 0; i < 6; i++)
            {
                simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, sequence++, i % 2 == 0 ? UnitType.HeavyTank : UnitType.Conscript, new CellPos(2, 2 + i * 2).Center));
            }

            simulation.Step();
            var ids = simulation.World.Entities.Select(e => e.Id).ToArray();
            simulation.Enqueue(new MoveCommand(Blue, simulation.CurrentTick, sequence++, [.. ids], new CellPos(20, 8).Center));
            var checksums = new List<ulong>();
            for (var tick = 0; tick < 300; tick++)
            {
                if (tick == 20)
                {
                    simulation.World.ChangeMap([new SetObstacleEdit(new CellPos(10, 8), true), new SetCornerLevelEdit(14, 7, 3)], null);
                }

                simulation.Step();
                checksums.Add(simulation.LastChecksum!.Value.Value);
            }

            return checksums;
        }
    }
}
