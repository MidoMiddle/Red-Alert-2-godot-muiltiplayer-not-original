using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// Interim rule until pathfinding (M2 steps 5–8): units stop in front of cells they cannot enter, and cannot spawn on them.
/// Row 0 of all_land_types.txt: clear 0–2, rough 3–5, road 6–7, beach 8–9, shallow 10–11, deep 12–13, shallow 14–15,
/// beach 16–17, rock 18–19, ice 20–21, clear 22–23. Trees stand at (1, 2), (2, 2), (1, 3), (2, 3).
/// </summary>
public sealed class BlockingTests
{
    private static readonly PlayerId Player1 = new(1);

    private static GameWorld LandTypesWorld() => new(seed: 1, TestMapParserTests.Load("all_land_types.txt").Grid);

    /// <summary>Runs movement until the unit has no order; returns the events.</summary>
    private static List<SimEvent> RunUntilStopped(GameWorld world, Unit unit)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < 2000 && unit.MoveTarget is not null; tick++)
        {
            MovementSystem.Update(world, events);
        }

        Assert.Null(unit.MoveTarget);
        return events;
    }

    [Fact]
    public void Infantry_stops_at_the_waters_edge()
    {
        var world = LandTypesWorld();
        var conscript = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(8, 0).Center);
        conscript.MoveTarget = new CellPos(13, 0).Center;

        var events = RunUntilStopped(world, conscript);

        Assert.Equal(new CellPos(9, 0), conscript.Position.ToCell());
        var blocked = Assert.IsType<UnitBlocked>(Assert.Single(events));
        Assert.Equal(conscript.Position, blocked.Position);
    }

    [Fact]
    public void Tank_stops_in_front_of_the_trees()
    {
        var world = LandTypesWorld();
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(0, 2).Center);
        tank.MoveTarget = new CellPos(2, 2).Center;

        var events = RunUntilStopped(world, tank);

        Assert.Equal(new CellPos(0, 2), tank.Position.ToCell());
        Assert.IsType<UnitBlocked>(Assert.Single(events));
    }

    [Fact]
    public void Vehicle_stops_before_rock()
    {
        var world = LandTypesWorld();
        var jeep = world.SpawnUnit(Player1, UnitType.SupportVehicle, new CellPos(23, 0).Center);
        jeep.MoveTarget = new CellPos(17, 0).Center;

        RunUntilStopped(world, jeep);

        Assert.Equal(new CellPos(20, 0), jeep.Position.ToCell());
    }

    [Fact]
    public void Passable_land_lets_the_unit_through()
    {
        var world = LandTypesWorld();
        var conscript = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(0, 0).Center);
        var target = new CellPos(9, 0).Center;
        conscript.MoveTarget = target;

        var events = RunUntilStopped(world, conscript);

        Assert.Equal(target, conscript.Position);
        Assert.IsType<UnitArrived>(Assert.Single(events));
    }

    [Theory]
    [InlineData(UnitType.Conscript, 12, 0)]
    [InlineData(UnitType.Conscript, 10, 0)]
    [InlineData(UnitType.Conscript, 18, 0)]
    [InlineData(UnitType.Conscript, 1, 2)]
    [InlineData(UnitType.HeavyTank, 12, 0)]
    [InlineData(UnitType.HeavyTank, 19, 0)]
    [InlineData(UnitType.HeavyTank, 2, 3)]
    [InlineData(UnitType.SupportVehicle, 11, 0)]
    [InlineData(UnitType.SupportVehicle, 18, 1)]
    [InlineData(UnitType.SupportVehicle, 1, 3)]
    public void Spawn_on_impassable_cells_is_rejected(UnitType type, int x, int y)
    {
        var world = LandTypesWorld();
        var center = new CellPos(x, y).Center;

        Assert.False(CommandProcessor.Apply(world, Spawn(player: 1, tick: 0, sequence: 0, center.X, center.Y, type)));
        Assert.Empty(world.Entities);
    }

    [Theory]
    [InlineData(UnitType.Conscript, 20, 0)]
    [InlineData(UnitType.HeavyTank, 8, 0)]
    [InlineData(UnitType.SupportVehicle, 3, 0)]
    public void Spawn_on_passable_land_is_accepted(UnitType type, int x, int y)
    {
        var world = LandTypesWorld();
        var center = new CellPos(x, y).Center;

        Assert.True(CommandProcessor.Apply(world, Spawn(player: 1, tick: 0, sequence: 0, center.X, center.Y, type)));
    }
}
