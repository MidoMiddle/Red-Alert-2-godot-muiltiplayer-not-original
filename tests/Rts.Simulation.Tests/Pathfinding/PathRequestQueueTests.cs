using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Pathfinding;

/// <summary>The per-tick node budget of docs/implementation/M2_unit_control.md, 4.4.</summary>
public sealed class PathRequestQueueTests
{
    private static readonly PlayerId Player1 = new(1);

    private static GameWorld World(string map) => new(seed: 1, TestMapParserTests.Load(map).Grid, TestRules.Standard);

    private static void Order(GameWorld world, Unit unit, CellPos goal) =>
        CommandProcessor.Apply(world, new MoveCommand(Player1, world.CurrentTick, 0, [unit.Id], goal.Center));

    /// <summary>Runs only the queue, tick after tick, until every request is answered; returns the paths by unit.</summary>
    private static Dictionary<EntityId, CellPath> RunQueue(GameWorld world, List<int>? expandedPerTick = null)
    {
        var paths = new Dictionary<EntityId, CellPath>();
        var events = new List<SimEvent>();
        for (var tick = 0; world.PathRequests.Requests.Count > 0; tick++)
        {
            Assert.True(tick < 100_000, "The queue never emptied.");
            events.Clear();
            world.PathRequests.Process(world, events);
            expandedPerTick?.Add(world.PathRequests.ExpandedLastTick);
            foreach (var found in events.OfType<UnitPathFound>())
            {
                world.TryGet(found.Unit, out var entity);
                paths.Add(found.Unit, ((Unit)entity).Path!);
            }

            world.CurrentTick++;
        }

        return paths;
    }

    /// <summary>Six conscripts on the maze with far goals, all ordered on the same tick.</summary>
    private static GameWorld MazeWithOrders(int budget)
    {
        var world = World("paths/maze.txt");
        world.PathRequests.Budget = budget;
        (int Sx, int Sy, int Gx, int Gy)[] trips =
            [(0, 0, 14, 14), (14, 0, 0, 14), (0, 14, 14, 0), (4, 4, 12, 10), (14, 14, 0, 0), (8, 8, 2, 12)];
        foreach (var (sx, sy, gx, gy) in trips)
        {
            var unit = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(sx, sy).Center);
            Order(world, unit, new CellPos(gx, gy));
        }

        return world;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(PathRequestQueue.DefaultBudget)]
    [InlineData(int.MaxValue)]
    public void Paths_do_not_depend_on_the_budget(int budget)
    {
        var reference = RunQueue(MazeWithOrders(int.MaxValue));
        var expandedPerTick = new List<int>();

        var paths = RunQueue(MazeWithOrders(budget), expandedPerTick);

        Assert.Equal(reference.Keys.Order(), paths.Keys.Order());
        foreach (var (unit, path) in reference)
        {
            Assert.Equal(path.Cells.ToArray(), paths[unit].Cells.ToArray());
            Assert.Equal((path.Status, path.Cost, path.Expanded), (paths[unit].Status, paths[unit].Cost, paths[unit].Expanded));
        }

        Assert.All(expandedPerTick, spent => Assert.InRange(spent, 0, budget));
        Assert.Equal(reference.Values.Sum(p => p.Expanded), expandedPerTick.Sum());
    }

    [Fact]
    public void Small_budget_spreads_the_work_over_ticks()
    {
        var expandedPerTick = new List<int>();

        RunQueue(MazeWithOrders(budget: 50), expandedPerTick);

        Assert.True(expandedPerTick.Count > 1);
        Assert.All(expandedPerTick.SkipLast(1), spent => Assert.Equal(50, spent));
    }

    [Fact]
    public void Requests_go_by_tick_then_unit_id_and_a_new_order_replaces_the_old()
    {
        var world = World("paths/open_field.txt");
        world.PathRequests.Budget = 1;
        var units = Enumerable.Range(0, 3).Select(i => world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(i, 0).Center)).ToList();

        world.CurrentTick = 5;
        Order(world, units[2], new CellPos(9, 5));
        Order(world, units[0], new CellPos(9, 5));
        world.CurrentTick = 4;
        Order(world, units[1], new CellPos(9, 5));
        world.CurrentTick = 6;
        Order(world, units[0], new CellPos(8, 4));

        Assert.Equal(
            [(4, units[1].Id), (5, units[2].Id), (6, units[0].Id)],
            world.PathRequests.Requests.Select(r => (r.RequestTick, r.Unit)));
        Assert.Equal(new CellPos(8, 4), world.PathRequests.Requests[2].Goal);
    }

    [Fact]
    public void Request_of_a_dead_unit_is_dropped()
    {
        var world = World("paths/open_field.txt");
        var unit = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(0, 0).Center);
        Order(world, unit, new CellPos(9, 5));
        unit.MarkDead();

        var events = new List<SimEvent>();
        world.PathRequests.Process(world, events);

        Assert.Empty(world.PathRequests.Requests);
        Assert.Empty(events);
    }

    [Fact]
    public void Waiting_unit_turns_toward_the_target_and_stands_until_its_path_is_ready()
    {
        var world = World("paths/maze.txt");
        world.PathRequests.Budget = 1;
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(0, 0).Center);
        Order(world, tank, new CellPos(14, 14));
        var start = tank.Position;
        var events = new List<SimEvent>();

        var ticks = 0;
        var facingWhileWaiting = tank.Facing;
        while (tank.PathPending)
        {
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            ticks++;
            if (tank.PathPending)
            {
                Assert.Equal(start, tank.Position);
                facingWhileWaiting = tank.Facing;
            }
        }

        // 135° at 2048 units per tick takes 12 ticks; the maze search at one node per tick takes far longer.
        Assert.True(ticks > 12, "With one node per tick the maze takes many ticks.");
        Assert.Equal(FixedMath.Angle(14 * 256, 14 * 256), facingWhileWaiting);
        Assert.NotNull(tank.Path);
    }
}
