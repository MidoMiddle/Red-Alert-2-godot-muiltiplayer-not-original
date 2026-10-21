using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Movement;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Movement;

/// <summary>Five soldiers per cell (docs/implementation/M2_unit_control.md, 4.8).</summary>
public sealed class SubcellTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameWorld OpenField() => new(seed: 1, widthCells: 20, heightCells: 20);

    private static Unit Place(GameWorld world, PlayerId owner, UnitType type, WorldPos position)
    {
        Assert.True(CommandProcessor.Apply(world, new SpawnTestUnitCommand(owner, 0, 0, type, position)));
        return (Unit)world.Entities[^1];
    }

    private static int Run(GameWorld world, int maxTicks = 2000)
    {
        var events = new List<SimEvent>();
        var ticks = 0;
        while (world.Entities.OfType<Unit>().Any(u => u.MoveTarget is not null || u.PathPending || u.HoldTicks > 0))
        {
            Assert.True(++ticks <= maxTicks, "Not settled.");
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.CurrentTick++;
            ReservationTests.AssertOccupancy(world);
        }

        Assert.DoesNotContain(events, e => e is UnitBlocked);
        return ticks;
    }

    [Fact]
    public void Spot_points_are_the_centre_and_four_corners()
    {
        var cell = new CellPos(2, 3);
        Assert.Equal(
            [cell.Center, cell.Center + new WorldPos(-64, -64), cell.Center + new WorldPos(64, -64), cell.Center + new WorldPos(-64, 64), cell.Center + new WorldPos(64, 64)],
            Enumerable.Range(0, Reservations.SubcellsPerCell).Select(i => Reservations.SubcellPoint(cell, i)));
    }

    [Fact]
    public void Five_soldiers_of_any_players_share_a_cell_and_a_sixth_does_not_fit()
    {
        var world = OpenField();
        var cell = new CellPos(5, 5);
        var soldiers = Enumerable.Range(0, 5)
            .Select(i => Place(world, i % 2 == 0 ? Player1 : Player2, UnitType.Conscript, cell.Center))
            .ToList();

        Assert.Equal(5, world.Reservations.InfantryCount(cell));
        Assert.Equal(5, soldiers.Select(s => s.Subcell).Distinct().Count());
        Assert.All(soldiers, s => Assert.Equal(Reservations.SubcellPoint(cell, s.Subcell), s.Position));
        Assert.False(CommandProcessor.Apply(world, Spawn(player: 1, tick: 0, sequence: 9, cell.Center.X, cell.Center.Y)));
    }

    [Fact]
    public void Spot_nearest_to_the_point_is_taken()
    {
        var world = OpenField();
        var cell = new CellPos(5, 5);

        var soldier = Place(world, Player1, UnitType.Conscript, cell.Center + new WorldPos(50, 70));

        Assert.Equal(4, soldier.Subcell);
    }

    [Fact]
    public void Vehicles_and_soldiers_never_share_a_cell()
    {
        var world = OpenField();
        Place(world, Player1, UnitType.Conscript, new CellPos(3, 3).Center);
        Place(world, Player1, UnitType.HeavyTank, new CellPos(4, 3).Center);

        Assert.False(CommandProcessor.Apply(world, Spawn(player: 1, tick: 0, sequence: 9, new CellPos(3, 3).Center.X, new CellPos(3, 3).Center.Y, UnitType.HeavyTank)));
        Assert.False(CommandProcessor.Apply(world, Spawn(player: 1, tick: 0, sequence: 9, new CellPos(4, 3).Center.X, new CellPos(4, 3).Center.Y, UnitType.Conscript)));
    }

    [Fact]
    public void Leaving_frees_the_spot()
    {
        var world = OpenField();
        var soldier = Place(world, Player1, UnitType.Conscript, new CellPos(5, 5).Center);

        soldier.MarkDead();
        world.RemoveDead();

        Assert.True(world.Reservations.IsFree(new CellPos(5, 5)));
    }

    [Fact]
    public void Six_soldiers_sent_one_by_one_fill_the_cell_and_the_sixth_stands_next_to_it()
    {
        var world = OpenField();
        var soldiers = Enumerable.Range(0, 6).Select(i => Place(world, Player1, UnitType.Conscript, new CellPos(2 + i, 2).Center)).ToList();
        var target = new CellPos(10, 10);
        foreach (var soldier in soldiers)
        {
            CommandProcessor.Apply(world, new MoveCommand(Player1, world.CurrentTick, 0, [soldier.Id], target.Center));
        }

        Run(world);

        var inTarget = soldiers.Where(s => s.Position.ToCell() == target).ToList();
        Assert.Equal(5, inTarget.Count);
        Assert.Equal(5, inTarget.Select(s => s.Subcell).Distinct().Count());
        var sixth = Assert.Single(soldiers.Except(inTarget));
        var cell = sixth.Position.ToCell();
        Assert.Equal(1, System.Math.Max(System.Math.Abs(cell.X - target.X), System.Math.Abs(cell.Y - target.Y)));
    }

    [Fact]
    public void Soldier_walks_through_a_cell_with_a_free_spot_without_stopping()
    {
        var world = new GameWorld(seed: 1, TestMapParserTests.Load("corridor.txt").Grid);
        for (var i = 0; i < 4; i++)
        {
            Place(world, Player2, UnitType.Conscript, new CellPos(8, 2).Center);
        }

        var walker = Place(world, Player1, UnitType.Conscript, new CellPos(0, 2).Center);
        CommandProcessor.Apply(world, new MoveCommand(Player1, 0, 0, [walker.Id], new CellPos(15, 2).Center));

        var ticks = Run(world);

        Assert.Equal(new CellPos(15, 2), walker.Position.ToCell());
        // 15 cells at 24 leptons per tick: about 160 ticks with no waiting at the crowded cell.
        Assert.InRange(ticks, 155, 175);
    }

    [Fact]
    public void Tank_does_not_drive_into_soldiers()
    {
        var world = new GameWorld(seed: 1, TestMapParser.Parse(string.Join("\n", Enumerable.Repeat(". . . . . . . . . .", 5))).Grid);
        Place(world, Player2, UnitType.Conscript, new CellPos(5, 2).Center);
        var tank = Place(world, Player1, UnitType.HeavyTank, new CellPos(0, 2).Center);
        CommandProcessor.Apply(world, new MoveCommand(Player1, 0, 0, [tank.Id], new CellPos(9, 2).Center));

        Run(world);

        Assert.Equal(new CellPos(9, 2).Center, tank.Position);
        Assert.Equal(new CellPos(5, 2), ((Unit)world.Entities[0]).Position.ToCell());
    }

    [Fact]
    public void Soldiers_pass_a_narrow_gap_faster_than_tanks()
    {
        int Pass(UnitType type)
        {
            var world = new GameWorld(seed: 1, TestMapParserTests.Load("paths/narrow_pass.txt").Grid);
            var units = Enumerable.Range(0, 15).Select(i => Place(world, Player1, type, new CellPos(i % 13, i / 13).Center)).ToList();
            for (var i = 0; i < units.Count; i++)
            {
                CommandProcessor.Apply(world, new MoveCommand(Player1, 0, (uint)i, [units[i].Id], new CellPos(i % 13, 5 + i / 13).Center));
            }

            var ticks = Run(world, maxTicks: 4000);
            Assert.All(units, unit => Assert.True(unit.Position.ToCell().Y >= 5, $"{unit.Id} stayed at {unit.Position.ToCell()}."));
            return ticks;
        }

        // Tanks are faster (36 vs 24 per tick) but go one per cell; soldiers file through five abreast.
        Assert.True(Pass(UnitType.Conscript) < Pass(UnitType.HeavyTank) * 3 / 2);
    }
}
