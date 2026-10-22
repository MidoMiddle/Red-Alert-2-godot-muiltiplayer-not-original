using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// A late unit swaps places with one of ours standing nearer its place (docs/implementation/M2_unit_control.md, 4.7): the
/// group fills from the inside instead of stepping aside and coming back. Every scenario checks the occupancy invariant.
/// </summary>
public sealed class SwapPlacesTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly CellPos Hole = new(16, 10);

    private static GameWorld OpenWorld() => new(seed: 1, widthCells: 32, heightCells: 32, TestRules.Standard);

    private static void Order(GameWorld world, Unit unit, CellPos target) =>
        CommandProcessor.Apply(world, new MoveCommand(unit.Owner, world.CurrentTick, 0, [unit.Id], target.Center));

    private static (List<SimEvent> Events, bool AnyReturning) Run(GameWorld world, int maxTicks)
    {
        var events = new List<SimEvent>();
        var anyReturning = false;
        for (var ticks = 0; world.Entities.OfType<Unit>().Any(GroupBusy); ticks++)
        {
            Assert.True(ticks < maxTicks, $"Not settled after {maxTicks} ticks.");
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.RemoveDead();
            world.CurrentTick++;
            ReservationTests.AssertOccupancy(world);
            anyReturning |= world.Entities.OfType<Unit>().Any(u => u.Returning);
        }

        return (events, anyReturning);
    }

    private static bool GroupBusy(Unit u) => u.MoveTarget is not null || u.HoldTicks > 0 || u.PathPending;

    /// <summary>Eight tanks standing round <see cref="Hole"/>; returns them.</summary>
    private static List<Unit> Ring(GameWorld world, UnitType type)
    {
        var ring = new List<Unit>();
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx != 0 || dy != 0)
                {
                    ring.Add(world.SpawnUnit(Player1, type, new CellPos(Hole.X + dx, Hole.Y + dy).Center));
                }
            }
        }

        return ring;
    }

    [Fact]
    public void A_late_tank_swaps_into_the_ring_and_the_hole_fills_from_inside()
    {
        var world = OpenWorld();
        var ring = Ring(world, UnitType.HeavyTank);
        var late = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(Hole.X, Hole.Y + 8).Center);

        Order(world, late, Hole);
        var (events, anyReturning) = Run(world, maxTicks: 600);

        var swap = Assert.Single(events.OfType<UnitPlacesSwapped>());
        Assert.Equal(late.Id, swap.Unit);
        Assert.False(anyReturning, "Nobody had to step aside and come back.");
        var cells = ring.Append(late).Select(u => u.Position.ToCell()).ToHashSet();
        Assert.Equal(9, cells.Count);
        Assert.Contains(Hole, cells);
        Assert.All(cells, cell => Assert.True(System.Math.Abs(cell.X - Hole.X) <= 1 && System.Math.Abs(cell.Y - Hole.Y) <= 1));
    }

    [Fact]
    public void A_late_soldier_swaps_with_a_soldier_of_a_full_cell()
    {
        var world = OpenWorld();
        var front = new CellPos(Hole.X, Hole.Y + 1);
        var soldiers = Enumerable.Range(0, 5).Select(_ => world.SpawnUnit(Player1, UnitType.Conscript, front.Center, snapToSubcell: true)).ToList();
        // Walls of soldiers left and right, so the way to the hole leads through the full cell.
        foreach (var dx in new[] { -1, 1 })
        {
            for (var i = 0; i < 5; i++)
            {
                world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(Hole.X + dx, Hole.Y + 1).Center, snapToSubcell: true);
            }
        }

        var late = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(Hole.X, Hole.Y + 6).Center, snapToSubcell: true);
        Order(world, late, Hole);
        var (events, _) = Run(world, maxTicks: 600);

        var swap = Assert.Single(events.OfType<UnitPlacesSwapped>(), e => e.Unit == late.Id);
        var other = soldiers.Single(s => s.Id == swap.Other);
        Assert.Equal(front, late.Position.ToCell());
        Assert.Equal(Hole, other.Position.ToCell());
    }

    [Fact]
    public void No_swap_with_one_of_ours_standing_far_from_the_place()
    {
        var world = OpenWorld();
        var bystander = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(Hole.X, Hole.Y + 10).Center);
        var start = bystander.Position;
        var late = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(Hole.X, Hole.Y + 14).Center);

        Order(world, late, new CellPos(Hole.X, Hole.Y - 2));
        var (events, _) = Run(world, maxTicks: 600);

        Assert.DoesNotContain(events, e => e is UnitPlacesSwapped);
        Assert.Equal(start, bystander.Position);
        Assert.Equal(new CellPos(Hole.X, Hole.Y - 2), late.Position.ToCell());
    }

    [Fact]
    public void A_tank_does_not_swap_with_soldiers()
    {
        var world = OpenWorld();
        Ring(world, UnitType.Conscript);
        var late = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(Hole.X, Hole.Y + 6).Center);

        Order(world, late, Hole);
        var (events, _) = Run(world, maxTicks: 1500);

        Assert.DoesNotContain(events, e => e is UnitPlacesSwapped);
    }

    [Fact]
    public void No_swap_on_a_waypoint_before_the_last()
    {
        var world = OpenWorld();
        Ring(world, UnitType.HeavyTank);
        var late = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(Hole.X, Hole.Y + 8).Center);
        // The last point lies farther than SwapRadius from the ring: only the first point could have offered a swap.
        var end = new CellPos(Hole.X + 12, Hole.Y);

        CommandProcessor.Apply(world, new WaypointsCommand(Player1, world.CurrentTick, 0, [late.Id], [Hole.Center, end.Center], loop: false));
        var (events, _) = Run(world, maxTicks: 1500);

        Assert.DoesNotContain(events, e => e is UnitPlacesSwapped);
        Assert.Equal(end, late.Position.ToCell());
    }
}
