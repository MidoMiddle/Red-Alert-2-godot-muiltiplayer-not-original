using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Commands;

/// <summary>Move with a queue, Stop, AttackMove, Waypoints and Scatter (docs/implementation/M2_unit_control.md, 4.10).</summary>
public sealed class CommandBehaviourTests
{
    private static WorldPos C(int x, int y) => new CellPos(x, y).Center;

    /// <summary>A simulation with tanks of player 1 at the given cells, spawned on tick 0 (ids from 1).</summary>
    private static GameSimulation WithTanks(params (int X, int Y)[] cells)
    {
        var simulation = new GameSimulation(new SimulationSettings(5), TestRules.Standard);
        for (var i = 0; i < cells.Length; i++)
        {
            simulation.Enqueue(Spawn(player: 1, tick: 0, sequence: (uint)i, C(cells[i].X, cells[i].Y).X, C(cells[i].X, cells[i].Y).Y, UnitType.HeavyTank));
        }

        simulation.Step();
        return simulation;
    }

    private static Unit UnitOf(GameSimulation simulation, int id)
    {
        simulation.World.TryGet(new EntityId(id), out var entity);
        return (Unit)entity;
    }

    /// <summary>Steps <paramref name="ticks"/> ticks and returns every arrival, checking occupancy every 10th tick.</summary>
    private static List<UnitArrived> Run(GameSimulation simulation, int ticks)
    {
        var arrivals = new List<UnitArrived>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            arrivals.AddRange(simulation.LastTickEvents.OfType<UnitArrived>());
            if (i % 10 == 0)
            {
                ReservationTests.AssertOccupancy(simulation.World);
            }
        }

        return arrivals;
    }

    [Fact]
    public void Queued_move_is_driven_after_the_current_one()
    {
        var simulation = WithTanks((2, 2));
        simulation.Enqueue(Move(player: 1, tick: 1, sequence: 10, units: [1], C(8, 2).X, C(8, 2).Y));
        simulation.Enqueue(Move(player: 1, tick: 2, sequence: 11, units: [1], C(8, 8).X, C(8, 8).Y, queue: true));

        var arrivals = Run(simulation, 400);

        Assert.Equal([C(8, 2), C(8, 8)], arrivals.Select(a => a.Position));
        Assert.Empty(UnitOf(simulation, 1).Orders);
    }

    [Fact]
    public void Move_without_queue_replaces_the_route()
    {
        var simulation = WithTanks((2, 2));
        simulation.Enqueue(Move(player: 1, tick: 1, sequence: 10, units: [1], C(8, 2).X, C(8, 2).Y));
        simulation.Enqueue(Move(player: 1, tick: 2, sequence: 11, units: [1], C(8, 8).X, C(8, 8).Y, queue: true));
        simulation.Enqueue(Move(player: 1, tick: 3, sequence: 12, units: [1], C(2, 9).X, C(2, 9).Y));

        var arrivals = Run(simulation, 400);

        Assert.Equal([C(2, 9)], arrivals.Select(a => a.Position));
    }

    [Fact]
    public void Queue_on_an_idle_unit_starts_at_once()
    {
        var simulation = WithTanks((2, 2));
        simulation.Enqueue(Move(player: 1, tick: 1, sequence: 10, units: [1], C(6, 2).X, C(6, 2).Y, queue: true));

        var arrivals = Run(simulation, 300);

        Assert.Equal([C(6, 2)], arrivals.Select(a => a.Position));
    }

    [Fact]
    public void Stop_halts_the_unit_where_it_is()
    {
        var simulation = WithTanks((2, 2), (2, 4));
        simulation.Enqueue(Waypoints(player: 1, tick: 1, sequence: 10, units: [1, 2], points: [(C(30, 2).X, C(30, 2).Y), (C(30, 20).X, C(30, 20).Y)], loop: true));
        Run(simulation, 40);
        simulation.Enqueue(Stop(player: 1, tick: simulation.CurrentTick, sequence: 11, units: [1, 2]));
        Run(simulation, 1);
        var stoppedAt = (UnitOf(simulation, 1).Position, UnitOf(simulation, 2).Position);

        var arrivals = Run(simulation, 60);

        Assert.Empty(arrivals);
        Assert.Equal(stoppedAt, (UnitOf(simulation, 1).Position, UnitOf(simulation, 2).Position));
        foreach (var unit in new[] { UnitOf(simulation, 1), UnitOf(simulation, 2) })
        {
            Assert.Null(unit.MoveTarget);
            Assert.Empty(unit.Orders);
            Assert.False(unit.LoopOrders);
            Assert.False(unit.PathPending);
        }

        Assert.Empty(simulation.World.PathRequests.Requests);
    }

    [Fact]
    public void Attack_move_drives_like_a_move_and_keeps_its_mark()
    {
        var simulation = WithTanks((2, 2));
        simulation.Enqueue(AttackMove(player: 1, tick: 1, sequence: 10, units: [1], C(9, 2).X, C(9, 2).Y));
        simulation.Step();
        Assert.True(UnitOf(simulation, 1).IsAttackMoving);

        var arrivals = Run(simulation, 300);
        Assert.Equal([C(9, 2)], arrivals.Select(a => a.Position));

        simulation.Enqueue(AttackMove(player: 1, tick: simulation.CurrentTick, sequence: 11, units: [1], C(12, 2).X, C(12, 2).Y));
        simulation.Enqueue(Move(player: 1, tick: simulation.CurrentTick, sequence: 12, units: [1], C(12, 4).X, C(12, 4).Y));
        simulation.Step();
        Assert.False(UnitOf(simulation, 1).IsAttackMoving);
    }

    [Fact]
    public void Waypoints_are_visited_in_order()
    {
        var simulation = WithTanks((2, 2));
        simulation.Enqueue(Waypoints(player: 1, tick: 1, sequence: 10, units: [1], points: [(C(10, 2).X, C(10, 2).Y), (C(10, 10).X, C(10, 10).Y), (C(2, 10).X, C(2, 10).Y)]));

        var arrivals = Run(simulation, 800);

        Assert.Equal([C(10, 2), C(10, 10), C(2, 10)], arrivals.Select(a => a.Position));
        Assert.Empty(UnitOf(simulation, 1).Orders);
    }

    [Fact]
    public void Patrol_goes_round_again()
    {
        var simulation = WithTanks((2, 2));
        simulation.Enqueue(Waypoints(player: 1, tick: 1, sequence: 10, units: [1], points: [(C(8, 2).X, C(8, 2).Y), (C(8, 8).X, C(8, 8).Y), (C(2, 8).X, C(2, 8).Y)], loop: true));

        var arrivals = Run(simulation, 1500);

        Assert.True(arrivals.Count >= 6);
        WorldPos[] round = [C(8, 2), C(8, 8), C(2, 8)];
        Assert.Equal(round.Concat(round), arrivals.Take(6).Select(a => a.Position));
        Assert.True(UnitOf(simulation, 1).LoopOrders);
    }

    [Fact]
    public void Group_waypoints_give_every_unit_its_own_place_at_every_point()
    {
        var simulation = WithTanks((2, 2), (3, 2), (2, 3), (3, 3));
        simulation.Enqueue(Waypoints(player: 1, tick: 1, sequence: 10, units: [1, 2, 3, 4], points: [(C(20, 5).X, C(20, 5).Y), (C(20, 20).X, C(20, 20).Y)]));
        simulation.Step();

        var units = Enumerable.Range(1, 4).Select(id => UnitOf(simulation, id)).ToList();
        for (var leg = 0; leg < 2; leg++)
        {
            Assert.Equal(4, units.Select(u => u.Orders[leg].Point).Distinct().Count());
        }

        var arrivals = Run(simulation, 1200);
        Assert.Equal(8, arrivals.Count);
    }

    [Fact]
    public void Scatter_sends_each_unit_to_a_free_neighbouring_cell_the_same_way_every_time()
    {
        string Scatter()
        {
            var simulation = WithTanks((5, 5), (6, 5), (10, 10));
            simulation.Enqueue(CommandFactory.Scatter(player: 1, tick: 1, sequence: 10, units: [1, 2, 3]));
            simulation.Step();
            var targets = Enumerable.Range(1, 3).Select(id => UnitOf(simulation, id).MoveTarget!.Value.ToCell()).ToList();
            var starts = new[] { new CellPos(5, 5), new CellPos(6, 5), new CellPos(10, 10) };
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(1, System.Math.Max(System.Math.Abs(targets[i].X - starts[i].X), System.Math.Abs(targets[i].Y - starts[i].Y)));
            }

            Run(simulation, 100);
            return string.Join(";", targets);
        }

        Assert.Equal(Scatter(), Scatter());
    }

    [Fact]
    public void Unit_with_no_free_neighbour_does_not_scatter()
    {
        var simulation = WithTanks((5, 5), (4, 4), (5, 4), (6, 4), (4, 5), (6, 5), (4, 6), (5, 6), (6, 6));
        simulation.Enqueue(CommandFactory.Scatter(player: 1, tick: 1, sequence: 10, units: [1]));

        simulation.Step();

        Assert.Null(UnitOf(simulation, 1).MoveTarget);
    }

    [Fact]
    public void A_point_outside_the_map_rejects_the_whole_command()
    {
        var world = new GameWorld(seed: 1, widthCells: 16, heightCells: 16, TestRules.Standard);
        var unit = world.SpawnUnit(new PlayerId(1), UnitType.HeavyTank, C(2, 2));

        Assert.False(CommandProcessor.Apply(world, Waypoints(player: 1, tick: 0, sequence: 0, units: [1], points: [(C(5, 5).X, C(5, 5).Y), (-10, 5)])));
        Assert.False(CommandProcessor.Apply(world, AttackMove(player: 1, tick: 0, sequence: 0, units: [1], x: 99_999, y: 5)));
        Assert.Null(unit.MoveTarget);
        Assert.Empty(unit.Orders);
    }

    [Fact]
    public void Foreign_units_are_skipped_by_every_command()
    {
        var world = new GameWorld(seed: 1, widthCells: 16, heightCells: 16, TestRules.Standard);
        var foreign = world.SpawnUnit(new PlayerId(2), UnitType.HeavyTank, C(2, 2));
        foreign.MoveTarget = C(9, 9);

        Assert.False(CommandProcessor.Apply(world, Stop(player: 1, tick: 0, sequence: 0, units: [1])));
        Assert.False(CommandProcessor.Apply(world, CommandFactory.Scatter(player: 1, tick: 0, sequence: 0, units: [1])));
        Assert.False(CommandProcessor.Apply(world, AttackMove(player: 1, tick: 0, sequence: 0, units: [1])));
        Assert.False(CommandProcessor.Apply(world, Waypoints(player: 1, tick: 0, sequence: 0, units: [1], points: [(100, 100)])));
        Assert.Equal(C(9, 9), foreign.MoveTarget);
    }
}
