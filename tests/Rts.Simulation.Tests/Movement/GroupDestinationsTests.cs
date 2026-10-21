using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Movement;

/// <summary>Group destinations of docs/implementation/M2_unit_control.md, 4.6.</summary>
public sealed class GroupDestinationsTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static List<Unit> Block(GameWorld world, int left, int top, int width, int height, UnitType type = UnitType.HeavyTank)
    {
        var units = new List<Unit>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                units.Add(world.SpawnUnit(Player1, type, new CellPos(left + x, top + y).Center));
            }
        }

        return units;
    }

    private static int Chebyshev(CellPos a, CellPos b) => System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));

    [Fact]
    public void Forty_units_get_forty_cells_in_the_same_shape()
    {
        var world = new GameWorld(seed: 1, widthCells: 40, heightCells: 40);
        var units = Block(world, 5, 5, 5, 8);
        var target = new CellPos(25, 25);

        var destinations = GroupDestinations.Assign(world, units, target.Center);

        Assert.Equal(40, destinations.Select(d => d.Destination.ToCell()).Distinct().Count());
        // Centre of the block: x 5…9 → 7, y 5…12 → 8.5 rounded away from zero → 9.
        foreach (var (unit, destination) in destinations)
        {
            var start = unit.Position.ToCell();
            Assert.Equal(new CellPos(start.X - 7 + 25, start.Y - 9 + 25), destination.ToCell());
        }
    }

    [Fact]
    public void Long_column_arrives_compact_and_in_order()
    {
        var world = new GameWorld(seed: 1, widthCells: 40, heightCells: 40);
        var units = Block(world, 5, 5, 1, 20);
        var target = new CellPos(25, 20);

        var destinations = GroupDestinations.Assign(world, units, target.Center);

        Assert.Equal(20, destinations.Select(d => d.Destination.ToCell()).Distinct().Count());
        Assert.All(destinations, d => Assert.True(Chebyshev(d.Destination.ToCell(), target) <= 6, $"{d.Destination.ToCell()} is too far."));
        // Squeezed into a blob, two units of one blob row can swap; the column order holds as a whole.
        var byStartY = destinations.OrderBy(d => d.Unit.Position.Y).Select(d => d.Destination.ToCell().Y).ToList();
        Assert.True(byStartY.Take(10).Average() < byStartY.Skip(10).Average());
        Assert.True(byStartY[0] <= byStartY[^1]);
    }

    [Fact]
    public void Click_on_an_island_gives_cells_the_group_can_reach()
    {
        var world = new GameWorld(seed: 1, TestMapParserTests.Load("paths/island.txt").Grid);
        var units = Block(world, 0, 0, 3, 2, UnitType.Conscript);

        var destinations = GroupDestinations.Assign(world, units, new CellPos(5, 5).Center);

        foreach (var (unit, destination) in destinations)
        {
            var cell = destination.ToCell();
            Assert.True(world.Map.CanEnter(cell, unit.Archetype.Locomotor));
            var insideRing = cell.X is >= 2 and <= 8 && cell.Y is >= 2 and <= 8;
            Assert.False(insideRing, $"{cell} is on the island or in the water.");
        }
    }

    [Fact]
    public void Cells_of_standing_outsiders_are_not_given()
    {
        var world = new GameWorld(seed: 1, widthCells: 30, heightCells: 30);
        var units = Block(world, 2, 2, 3, 3);
        var blockers = new List<CellPos> { new(15, 15), new(16, 15), new(15, 16), new(14, 14) };
        foreach (var cell in blockers)
        {
            world.SpawnUnit(Player2, UnitType.HeavyTank, cell.Center);
        }

        var destinations = GroupDestinations.Assign(world, units, new CellPos(15, 15).Center);

        Assert.DoesNotContain(destinations, d => blockers.Contains(d.Destination.ToCell()));
        Assert.Equal(9, destinations.Select(d => d.Destination.ToCell()).Distinct().Count());
    }

    [Fact]
    public void Single_unit_drives_to_the_clicked_point()
    {
        var world = new GameWorld(seed: 1, widthCells: 30, heightCells: 30);
        var unit = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(2, 2).Center);
        var target = new WorldPos(4000, 3000);

        Assert.Equal([(unit, target)], GroupDestinations.Assign(world, [unit], target));
    }

    [Fact]
    public void Unit_given_the_clicked_cell_drives_to_the_clicked_point()
    {
        var world = new GameWorld(seed: 1, widthCells: 30, heightCells: 30);
        var units = Block(world, 2, 2, 2, 2);
        var target = new CellPos(15, 15).Center + new WorldPos(50, -30);

        var destinations = GroupDestinations.Assign(world, units, target);

        Assert.Contains(destinations, d => d.Destination == target);
    }

    [Fact]
    public void Order_of_units_in_the_command_does_not_matter()
    {
        var world = new GameWorld(seed: 1, widthCells: 40, heightCells: 40);
        var units = Block(world, 3, 3, 4, 3);
        var mixed = units.Select((u, i) => (u, i)).OrderBy(p => (p.i * 7) % units.Count).Select(p => p.u).ToList();

        var a = GroupDestinations.Assign(world, units, new CellPos(30, 30).Center);
        var b = GroupDestinations.Assign(world, mixed, new CellPos(30, 30).Center);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Mixed_group_gets_cells_each_can_stand_on()
    {
        var world = new GameWorld(seed: 1, TestMapParserTests.Load("all_land_types.txt").Grid);
        var units = new List<Unit>
        {
            world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(0, 0).Center),
            world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(1, 0).Center),
            world.SpawnUnit(Player1, UnitType.SupportVehicle, new CellPos(2, 0).Center),
            world.SpawnUnit(Player1, UnitType.Engineer, new CellPos(0, 1).Center),
        };

        var destinations = GroupDestinations.Assign(world, units, new CellPos(19, 1).Center);

        Assert.All(destinations, d => Assert.True(world.Map.CanEnter(d.Destination.ToCell(), d.Unit.Archetype.Locomotor)));
        Assert.Equal(4, destinations.Select(d => d.Destination.ToCell()).Distinct().Count());
    }

    [Fact]
    public void Forty_units_drive_to_their_planned_places()
    {
        var simulation = new GameSimulation(new SimulationSettings(7));
        uint sequence = 0;
        for (var i = 0; i < 40; i++)
        {
            var type = i % 2 == 0 ? UnitType.HeavyTank : UnitType.Conscript;
            var cell = new CellPos(4 + i % 8, 4 + i / 8);
            simulation.Enqueue(new SpawnTestUnitCommand(Player1, 0, sequence++, type, cell.Center));
        }

        var ids = Enumerable.Range(1, 40).Select(i => new EntityId(i)).ToImmutableArray();
        simulation.Enqueue(new MoveUnitsCommand(Player1, 1, sequence++, ids, new CellPos(40, 36).Center));

        simulation.Step();
        simulation.Step();
        var destinations = simulation.LastTickEvents.OfType<UnitMoveStarted>().ToDictionary(e => e.Unit, e => e.Target);
        Assert.Equal(40, destinations.Count);
        Assert.Equal(40, destinations.Values.Distinct().Count());
        // 20 tanks get 20 cells; 20 soldiers share cells, five at most.
        var cellsUsed = destinations.Values.Select(p => p.ToCell()).Distinct().Count();
        Assert.InRange(cellsUsed, 24, 39);

        var ticks = 0;
        while (simulation.World.Entities.OfType<Unit>().Any(u => u.MoveTarget is not null || u.PathPending || u.HoldTicks > 0))
        {
            Assert.True(++ticks < 2000, "The group never settled.");
            simulation.Step();
            ReservationTests.AssertOccupancy(simulation.World);
        }

        // Most units stop in the cell planned for them. In a big mixed group a tank whose way crosses standing soldiers may
        // ask them to make room; a soldier that cannot get its place back takes a free one nearby. Measured: 35 of 40 exact,
        // the rest within 2 cells.
        var units = simulation.World.Entities.OfType<Unit>().ToList();
        var exact = units.Count(unit => unit.Position.ToCell() == destinations[unit.Id].ToCell());
        Assert.True(exact >= 32, $"Only {exact} of 40 in their planned cells.");
        Assert.All(units, unit => Assert.True(
            Chebyshev(unit.Position.ToCell(), destinations[unit.Id].ToCell()) <= 2,
            $"{unit.Id} stopped at {unit.Position.ToCell()}, planned {destinations[unit.Id].ToCell()}."));
    }
}
