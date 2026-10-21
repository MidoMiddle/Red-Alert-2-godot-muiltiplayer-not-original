using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;
using Rts.Simulation.Tests.World;

namespace Rts.Simulation.Tests.Systems;

public sealed class MovementSystemTests
{
    private static readonly PlayerId Player1 = new(1);

    private static GameWorld NewWorld() => new(seed: 1, widthCells: 64, heightCells: 64);

    /// <summary>Runs movement until the unit stops; returns the number of ticks it took.</summary>
    private static int RunUntilArrived(GameWorld world, Unit unit, int maxTicks = 10_000)
    {
        var events = new List<SimEvent>();
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            MovementSystem.Update(world, events);
            if (unit.MoveTarget is null)
            {
                return tick;
            }
        }

        throw new InvalidOperationException($"Unit did not arrive in {maxTicks} ticks.");
    }

    [Fact]
    public void Straight_move_arrives_exactly_after_distance_over_speed_ticks()
    {
        var world = NewWorld();
        var unit = world.SpawnForTest(Player1, new WorldPos(128, 128), speed: 32);
        unit.MoveTarget = new WorldPos(128 + 320, 128);

        var ticks = RunUntilArrived(world, unit);

        Assert.Equal(10, ticks);
        Assert.Equal(new WorldPos(448, 128), unit.Position);
    }

    [Theory]
    [InlineData(0, 0, 3000, 4000, 32)]
    [InlineData(5000, 5000, 100, 7, 32)]
    [InlineData(100, 100, 1234, 987, 17)]
    [InlineData(10, 10, 15, 400, 1)]
    [InlineData(400, 10, 10, 15, 1)]
    public void Any_move_arrives_exactly_at_target_without_overshooting(int fromX, int fromY, int toX, int toY, int speed)
    {
        var world = NewWorld();
        var unit = world.SpawnForTest(Player1, new WorldPos(fromX, fromY), speed);
        var target = new WorldPos(toX, toY);
        unit.MoveTarget = target;
        var events = new List<SimEvent>();
        var previousDistance = (target - unit.Position).LengthSquared;
        var distance = IntMath.Sqrt(previousDistance);
        var ticks = 0;

        while (unit.MoveTarget is not null)
        {
            MovementSystem.Update(world, events);
            ticks++;
            var remaining = (target - unit.Position).LengthSquared;
            Assert.True(remaining < previousDistance, $"tick {ticks}: distance did not decrease");
            previousDistance = remaining;
            Assert.True(ticks <= distance * 2 + 2, "too many ticks");
        }

        Assert.Equal(target, unit.Position);
        Assert.Single(events.OfType<UnitArrived>());
    }

    [Fact]
    public void Diagonal_move_takes_about_distance_over_speed_ticks()
    {
        var world = NewWorld();
        var unit = world.SpawnForTest(Player1, new WorldPos(0, 0), speed: 32);
        unit.MoveTarget = new WorldPos(3000, 4000); // distance 5000

        var ticks = RunUntilArrived(world, unit);

        // ceil(5000 / 32) = 157. Per-tick rounding of whole leptons costs at most one tick here;
        // sub-lepton accumulation that removes it belongs to M2 movement.
        Assert.InRange(ticks, 157, 158);
        Assert.Equal(new WorldPos(3000, 4000), unit.Position);
    }

    [Fact]
    public void Move_to_own_position_arrives_immediately()
    {
        var world = NewWorld();
        var unit = world.SpawnForTest(Player1, new WorldPos(500, 500), speed: 32);
        unit.MoveTarget = unit.Position;
        var events = new List<SimEvent>();

        MovementSystem.Update(world, events);

        Assert.Null(unit.MoveTarget);
        Assert.Equal(new UnitArrived(0, unit.Id, new WorldPos(500, 500)), Assert.Single(events));
    }

    [Fact]
    public void Dead_and_idle_units_do_not_move()
    {
        var world = NewWorld();
        var idle = world.SpawnForTest(Player1, new WorldPos(100, 100), speed: 32);
        var dead = world.SpawnForTest(Player1, new WorldPos(100, 100), speed: 32);
        dead.MoveTarget = new WorldPos(2000, 100);
        dead.MarkDead();
        var events = new List<SimEvent>();

        MovementSystem.Update(world, events);

        Assert.Equal(new WorldPos(100, 100), idle.Position);
        Assert.Equal(new WorldPos(100, 100), dead.Position);
        Assert.Empty(events);
    }
}
