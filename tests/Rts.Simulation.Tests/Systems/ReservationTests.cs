using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// One unit per cell and jams that always resolve (docs/implementation/M2_unit_control.md, 4.7). Every scenario checks the
/// occupancy invariant on every tick.
/// </summary>
public sealed class ReservationTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameWorld World(string map) => new(seed: 1, TestMapParserTests.Load(map).Grid, TestRules.Standard);

    private static void Order(GameWorld world, Unit unit, CellPos target) =>
        CommandProcessor.Apply(world, new MoveCommand(unit.Owner, world.CurrentTick, 0, [unit.Id], target.Center));

    private static bool Busy(GameWorld world) =>
        world.Entities.OfType<Unit>().Any(u => u.IsAlive && (u.MoveTarget is not null || u.HoldTicks > 0 || u.PathPending));

    /// <summary>Runs queue and movement until every unit is done; checks the invariant each tick; returns events and ticks.</summary>
    private static (List<SimEvent> Events, int Ticks) Run(GameWorld world, int maxTicks, Action<int>? everyTick = null)
    {
        var events = new List<SimEvent>();
        var ticks = 0;
        while (Busy(world))
        {
            Assert.True(++ticks <= maxTicks, $"Not settled after {maxTicks} ticks.");
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.RemoveDead();
            world.CurrentTick++;
            AssertOccupancy(world);
            everyTick?.Invoke(ticks);
        }

        return (events, ticks);
    }

    /// <summary>
    /// Every living unit holds its place where it stands (a vehicle its cell, a soldier its spot), places held = units,
    /// no cell has a vehicle and soldiers or more than five soldiers, nobody stands on impassable land. On both levels of a high
    /// bridge (<see cref="Decks"/>): a unit on a deck holds its place there and stands on a standing segment.
    /// </summary>
    internal static void AssertOccupancy(GameWorld world)
    {
        var units = world.Entities.OfType<Unit>().Where(u => u.IsAlive).ToList();
        var reservations = world.Reservations;
        foreach (var unit in units)
        {
            var cell = unit.Position.ToCell();
            if (unit.Archetype.Placement == UnitPlacement.Cell)
            {
                Assert.Equal(unit.Id, reservations.VehicleAt(cell, unit.OnDeck));
            }
            else
            {
                Assert.Equal(unit.Id, reservations.InfantryAt(cell, unit.Subcell, unit.OnDeck));
            }

            Assert.True(Decks.CanStand(world.Map, cell, unit.OnDeck, unit.Archetype.Locomotor), $"{unit.Id} stands on {cell}{(unit.OnDeck ? " (deck)" : "")}.");
        }

        var held = 0;
        for (var y = 0; y < world.Map.Height; y++)
        {
            for (var x = 0; x < world.Map.Width; x++)
            {
                var cell = new CellPos(x, y);
                foreach (var onDeck in world.Map.IsDeck(cell) ? new[] { false, true } : [false])
                {
                    var soldiers = reservations.InfantryCount(cell, onDeck);
                    var vehicle = reservations.VehicleAt(cell, onDeck) != EntityId.None;
                    Assert.False(vehicle && soldiers > 0, $"Vehicle and soldiers share {cell}.");
                    held += soldiers + (vehicle ? 1 : 0);
                }
            }
        }

        Assert.Equal(units.Count, held);
    }

    [Fact]
    public void Head_on_in_a_one_cell_corridor_resolves_and_the_larger_id_gives_way()
    {
        var world = World("corridor.txt");
        var east = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(0, 2).Center);
        var west = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(15, 2).Center);
        Order(world, east, new CellPos(15, 2));
        Order(world, west, new CellPos(0, 2));
        var westGaveWay = false;
        var eastGaveWay = false;

        Run(world, maxTicks: 1500, _ =>
        {
            westGaveWay |= west.ResumeTarget is not null;
            eastGaveWay |= east.ResumeTarget is not null;
        });

        Assert.Equal(new CellPos(15, 2).Center, east.Position);
        Assert.Equal(new CellPos(0, 2).Center, west.Position);
        Assert.True(westGaveWay, "The unit with the larger id gives way.");
        Assert.False(eastGaveWay);
    }

    [Fact]
    public void Units_cross_a_one_cell_ford_one_by_one()
    {
        var map = TestMapParserTests.Load("bridge_gap.txt");
        var world = new GameWorld(seed: 1, map.Grid, TestRules.Standard);
        var units = map.Units.Select(u => world.SpawnUnit(u.Owner, u.Type, u.Cell.Center)).ToList();
        CellPos[] targets = [new(10, 3), new(10, 1), new(10, 5)];
        for (var i = 0; i < units.Count; i++)
        {
            Order(world, units[i], targets[i]);
        }

        Run(world, maxTicks: 1500);

        for (var i = 0; i < units.Count; i++)
        {
            Assert.Equal(targets[i].Center, units[i].Position);
        }
    }

    [Fact]
    public void Twenty_units_pass_a_narrow_gap()
    {
        var world = World("paths/narrow_pass.txt");
        var units = new List<Unit>();
        var targets = new List<CellPos>();
        for (var i = 0; i < 20; i++)
        {
            var type = i % 3 == 0 ? UnitType.HeavyTank : i % 3 == 1 ? UnitType.Conscript : UnitType.SupportVehicle;
            units.Add(world.SpawnUnit(Player1, type, new CellPos(i % 10 + (i < 10 ? 0 : 1), i / 10).Center));
            targets.Add(new CellPos(i % 10 + 1, 5 + i / 10));
        }

        for (var i = 0; i < units.Count; i++)
        {
            Order(world, units[i], targets[i]);
        }

        var (events, _) = Run(world, maxTicks: 4000);

        Assert.DoesNotContain(events, e => e is UnitBlocked);
        Assert.All(units, unit => Assert.True(unit.Position.ToCell().Y >= 5, $"{unit.Id} stayed at {unit.Position.ToCell()}."));
    }

    [Fact]
    public void Standing_unit_of_the_same_player_steps_aside()
    {
        var world = World("corridor.txt");
        var mover = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(0, 2).Center);
        var standing = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(8, 2).Center);
        Order(world, mover, new CellPos(15, 2));
        var steppedAside = false;

        Run(world, maxTicks: 1500, _ => steppedAside |= standing.Position.ToCell() != new CellPos(8, 2));

        // It made room, let the mover through and came back to its place.
        Assert.Equal(new CellPos(15, 2).Center, mover.Position);
        Assert.True(steppedAside);
        Assert.Equal(new CellPos(8, 2).Center, standing.Position);
        Assert.Null(standing.ResumeTarget);
        Assert.False(standing.Returning);
    }

    [Fact]
    public void Standing_enemy_is_driven_around_and_does_not_move()
    {
        var world = World("paths/open_field.txt");
        var mover = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(0, 2).Center);
        var enemy = world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(5, 2).Center);
        Order(world, mover, new CellPos(9, 2));

        Run(world, maxTicks: 600);

        Assert.Equal(new CellPos(9, 2).Center, mover.Position);
        Assert.Equal(new CellPos(5, 2).Center, enemy.Position);
    }

    [Fact]
    public void Enemy_blocking_the_only_way_stops_the_mover_in_front_of_it()
    {
        var world = World("paths/narrow_pass.txt");
        var mover = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(6, 0).Center);
        var enemy = world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(6, 3).Center);
        Order(world, mover, new CellPos(6, 6));

        Run(world, maxTicks: 600);

        Assert.True(mover.Position.ToCell().Y <= 2, $"Mover ended at {mover.Position.ToCell()}.");
        Assert.Equal(new CellPos(6, 3).Center, enemy.Position);
    }

    [Fact]
    public void Units_sent_to_one_point_stop_around_it()
    {
        var world = World("paths/open_field.txt");
        var units = Enumerable.Range(0, 6).Select(i => world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(i, 0).Center)).ToList();
        foreach (var unit in units)
        {
            Order(world, unit, new CellPos(6, 4));
        }

        Run(world, maxTicks: 1500);

        // Every unit in its own cell (checked every tick) around the goal. Who stands exactly on it is up to
        // the group destinations of M2 step 9.
        Assert.Contains(units, unit => Chebyshev(unit.Position.ToCell(), new CellPos(6, 4)) <= 1);
        Assert.All(units, unit =>
        {
            var cell = unit.Position.ToCell();
            Assert.True(System.Math.Max(System.Math.Abs(cell.X - 6), System.Math.Abs(cell.Y - 4)) <= 3, $"{unit.Id} at {cell}.");
        });
    }

    private static int Chebyshev(CellPos a, CellPos b) => System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));

    [Fact]
    public void Walled_in_unit_gives_up_after_three_detours()
    {
        var world = World("paths/open_field.txt");
        var trapped = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(4, 3).Center);
        foreach (var (dx, dy) in new[] { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) })
        {
            world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(4 + dx, 3 + dy).Center);
        }

        Order(world, trapped, new CellPos(9, 3));
        var maxReplans = 0;

        var (events, _) = Run(world, maxTicks: 600, _ => maxReplans = System.Math.Max(maxReplans, trapped.Replans));

        Assert.Equal(new CellPos(4, 3), trapped.Position.ToCell());
        Assert.InRange(maxReplans, 1, MovementSystem.MaxReplans);
        Assert.Contains(events, e => e is UnitArrived arrived && arrived.Unit == trapped.Id);
    }

    [Fact]
    public void Unit_waits_for_a_moving_unit_before_driving_around_it()
    {
        var world = World("paths/open_field.txt");
        var mover = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(3, 2).Center);
        var stuck = world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(4, 2).Center);
        mover.Facing = BAngle.East;
        Order(world, mover, new CellPos(8, 2));
        // A unit that has an order but never gets its path: it counts as moving and never leaves its cell.
        stuck.MoveTarget = new CellPos(0, 5).Center;
        stuck.PathPending = true;

        var events = new List<SimEvent>();
        var replannedOnTick = 0;
        for (var tick = 1; tick <= 20 && replannedOnTick == 0; tick++)
        {
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.CurrentTick++;
            // A detour is a new request that keeps out of the stuck unit's cell.
            if (world.PathRequests.Requests.Any(r => r.Unit == mover.Id && r.Avoid.Contains(new CellPos(4, 2))))
            {
                replannedOnTick = tick;
            }
        }

        // The mover reaches the edge of its cell first, then waits 8 ticks.
        Assert.InRange(replannedOnTick, MovementSystem.WaitBeforeDetour + 1, MovementSystem.WaitBeforeDetour + 4);
    }

    [Theory]
    [InlineData("= = = = = = = = = = =", UnitType.SupportVehicle, 45)]
    [InlineData(". . . . . . . . . . .", UnitType.SupportVehicle, 54)]
    [InlineData(", , , , , , , , , , ,", UnitType.SupportVehicle, 72)]
    [InlineData(", , , , , , , , , , ,", UnitType.Conscript, 117)]
    public void Speed_follows_the_land(string row, UnitType type, int ticks)
    {
        // 10 cells = 2560 leptons. Wheels: 48 per tick, ×120% on road = 58 (45 ticks), ×75% on rough = 36 (72 ticks).
        // Infantry on rough: 24 × 90% = 21.6, rounded to 22 (117 ticks).
        var world = new GameWorld(seed: 1, TestMapParser.Parse(row, TestRules.Standard).Grid, TestRules.Standard);
        var unit = world.SpawnUnit(Player1, type, new CellPos(0, 0).Center);
        unit.Facing = BAngle.East;
        Order(world, unit, new CellPos(10, 0));

        var (_, taken) = Run(world, maxTicks: 500);

        Assert.Equal(ticks, taken);
    }

    [Fact]
    public void Spawn_on_a_taken_cell_is_rejected()
    {
        var world = World("paths/open_field.txt");
        world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(2, 2).Center);
        var sameCell = new CellPos(2, 2).Center + new WorldPos(40, -30);

        Assert.False(CommandProcessor.Apply(world, Spawn(player: 2, tick: 0, sequence: 0, sameCell.X, sameCell.Y)));
        Assert.True(CommandProcessor.Apply(world, Spawn(player: 2, tick: 0, sequence: 1, 3 * 256 + 10, 2 * 256 + 10)));
    }

    [Fact]
    public void Dead_units_free_their_cells()
    {
        var world = World("paths/open_field.txt");
        var unit = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(2, 2).Center);

        unit.MarkDead();
        world.RemoveDead();

        Assert.True(world.Reservations.IsFree(new CellPos(2, 2)));
    }

    [Fact]
    public void Random_orders_never_break_the_occupancy_invariant()
    {
        var random = new TestRandom(21);
        foreach (var mapName in new[] { "all_land_types.txt", "paths/maze.txt", "paths/narrow_pass.txt" })
        {
            var world = World(mapName);
            var units = new List<Unit>();
            for (var i = 0; i < 40 && units.Count < 12; i++)
            {
                var type = (UnitType)(1 + random.NextInt(8));
                var cell = new CellPos(random.NextInt(world.Map.Width), random.NextInt(world.Map.Height));
                var center = cell.Center;
                if (CommandProcessor.Apply(world, Spawn(1 + random.NextInt(2), 0, (uint)i, center.X, center.Y, type)))
                {
                    units.Add((Unit)world.Entities[^1]);
                }
            }

            var events = new List<SimEvent>();
            for (var tick = 0; tick < 600; tick++)
            {
                if (tick % 40 == 0)
                {
                    foreach (var unit in units)
                    {
                        Order(world, unit, new CellPos(random.NextInt(world.Map.Width), random.NextInt(world.Map.Height)));
                    }
                }

                world.PathRequests.Process(world, events);
                MovementSystem.Update(world, events);
                world.CurrentTick++;
                AssertOccupancy(world);
            }

            Assert.DoesNotContain(events, e => e is UnitBlocked);
        }
    }
}
