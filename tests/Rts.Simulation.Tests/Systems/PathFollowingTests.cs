using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Systems;

/// <summary>Units drive along the path found for them (simple following, taken early from M2 step 8).</summary>
public sealed class PathFollowingTests
{
    private static readonly PlayerId Player1 = new(1);

    private static GameWorld World(string map) => new(seed: 1, TestMapParserTests.Load(map).Grid);

    /// <summary>Orders the unit to <paramref name="target"/> and runs queue and movement until it stops; checks every tick.</summary>
    private static (List<SimEvent> Events, int Ticks) Drive(GameWorld world, Unit unit, WorldPos target)
    {
        CommandProcessor.Apply(world, new MoveUnitsCommand(Player1, world.CurrentTick, 0, [unit.Id], target));
        var events = new List<SimEvent>();
        var ticks = 0;
        while (unit.MoveTarget is not null)
        {
            Assert.True(++ticks < 5000, "The unit never arrived.");
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            Assert.True(world.Map.CanEnter(unit.Position.ToCell(), unit.Archetype.Locomotor), $"{unit.Type} stands on {unit.Position.ToCell()}.");
            world.CurrentTick++;
        }

        Assert.DoesNotContain(events, e => e is UnitBlocked);
        return (events, ticks);
    }

    [Fact]
    public void Infantry_takes_the_one_cell_pass()
    {
        var world = World("paths/narrow_pass.txt");
        var conscript = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(0, 0).Center);
        var target = new CellPos(12, 6).Center + new WorldPos(17, -40);

        Drive(world, conscript, target);

        Assert.Equal(target, conscript.Position);
    }

    [Fact]
    public void Tank_drives_around_the_trees()
    {
        var world = World("all_land_types.txt");
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(1, 1).Center);
        var target = new CellPos(1, 5).Center;

        var (events, _) = Drive(world, tank, target);

        Assert.Equal(target, tank.Position);
        Assert.Contains(events, e => e is UnitPathFound { Status: PathStatus.Found });
    }

    [Fact]
    public void Wheels_follow_the_maze()
    {
        var world = World("paths/maze.txt");
        var jeep = world.SpawnUnit(Player1, UnitType.SupportVehicle, new CellPos(0, 0).Center);

        Drive(world, jeep, new CellPos(14, 14).Center);

        Assert.Equal(new CellPos(14, 14).Center, jeep.Position);
    }

    [Fact]
    public void Unreachable_goal_ends_at_the_centre_of_the_nearest_cell()
    {
        var world = World("all_land_types.txt");
        var conscript = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(8, 0).Center);

        var (events, _) = Drive(world, conscript, new CellPos(13, 0).Center);

        // Deep water at (13, 0): the nearest reachable cell is the far beach (16, 0), three cells away, reached over the
        // road of row 4; the near beach (9, 0) is four cells away.
        Assert.Equal(new CellPos(16, 0).Center, conscript.Position);
        Assert.Contains(events, e => e is UnitPathFound { Status: PathStatus.Unreachable });
        Assert.Equal(conscript.Position, Assert.IsType<UnitArrived>(events[^1]).Position);
    }

    [Fact]
    public void Corners_do_not_slow_the_unit_down()
    {
        // Straight along a row: the same number of ticks as a direct straight move of the same length.
        var world = World("paths/open_field.txt");
        var conscript = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(0, 0).Center);

        var (_, ticks) = Drive(world, conscript, new CellPos(9, 0).Center);

        var distance = 9 * WorldPos.LeptonsPerCell;
        Assert.Equal((distance + conscript.Speed - 1) / conscript.Speed, ticks);
    }

    [Fact]
    public void Target_inside_the_start_cell_is_reached_directly()
    {
        var world = World("paths/open_field.txt");
        var conscript = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(3, 3).Center);
        var target = new CellPos(3, 3).Center + new WorldPos(60, 20);

        Drive(world, conscript, target);

        Assert.Equal(target, conscript.Position);
    }
}
