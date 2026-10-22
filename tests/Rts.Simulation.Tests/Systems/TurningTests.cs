using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Systems;

/// <summary>Hull turning before and while driving (docs/implementation/M2_unit_control.md, 4.2).</summary>
public sealed class TurningTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly WorldPos Start = new(5000, 5000);

    private static (GameWorld World, Unit Unit) Spawn(UnitType type, WorldPos target)
    {
        var world = new GameWorld(seed: 1, widthCells: 64, heightCells: 64, TestRules.Standard);
        var unit = world.SpawnUnit(Player1, type, Start);
        unit.MoveTarget = target;
        return (world, unit);
    }

    private static void Tick(GameWorld world) => MovementSystem.Update(world, new List<SimEvent>());

    [Fact]
    public void Tank_turns_on_the_spot_until_45_degrees_remain_then_drives()
    {
        // Facing north, target due south: half a turn at 2048 units per tick.
        var (world, tank) = Spawn(UnitType.HeavyTank, Start + new WorldPos(0, 3000));
        Assert.Equal(2048, tank.TurnRate);

        for (var tick = 1; tick <= 11; tick++)
        {
            Tick(world);
            Assert.Equal(Start, tank.Position);
            Assert.Equal(BAngle.FromUnits(-2048 * tick), tank.Facing);
        }

        // Tick 12: 8192 units (45°) remain after the turn, so the tank starts driving straight to the target.
        Tick(world);
        Assert.Equal(BAngle.FromUnits(-2048 * 12), tank.Facing);
        Assert.Equal(Start + new WorldPos(0, tank.Speed), tank.Position);

        // It keeps turning on the way and faces the target four ticks later.
        for (var tick = 13; tick <= 16; tick++)
        {
            Tick(world);
        }

        Assert.Equal(BAngle.South, tank.Facing);
        Assert.Equal(Start + new WorldPos(0, 5 * tank.Speed), tank.Position);
    }

    [Fact]
    public void Turn_takes_the_short_side()
    {
        var (world, tank) = Spawn(UnitType.HeavyTank, Start + new WorldPos(-3000, 0));

        Tick(world);

        Assert.Equal(BAngle.FromUnits(-2048), tank.Facing);
        Assert.Equal(-2048, BAngle.Difference(BAngle.North, tank.Facing));
    }

    [Fact]
    public void Within_45_degrees_the_unit_drives_and_turns_at_once()
    {
        // About 34° east of north.
        var target = Start + new WorldPos(2000, -3000);
        var (world, tank) = Spawn(UnitType.HeavyTank, target);
        var wanted = FixedMath.Angle(2000, -3000);
        Assert.InRange(BAngle.Difference(BAngle.North, wanted), 2048 + 1, MovementSystem.MaxDriveAngle);

        Tick(world);

        Assert.Equal(BAngle.FromUnits(2048), tank.Facing);
        Assert.NotEqual(Start, tank.Position);

        // Two more ticks finish the ~34° turn; the wanted direction shifts a little as the tank moves.
        Tick(world);
        Tick(world);
        var delta = target - tank.Position;
        Assert.Equal(FixedMath.Angle(delta.X, delta.Y), tank.Facing);
    }

    [Fact]
    public void Infantry_turns_instantly_and_walks_on_the_first_tick()
    {
        var (world, conscript) = Spawn(UnitType.Conscript, Start + new WorldPos(0, 3000));
        Assert.Equal(0, conscript.TurnRate);

        Tick(world);

        Assert.Equal(BAngle.South, conscript.Facing);
        Assert.Equal(Start + new WorldPos(0, 24), conscript.Position);
    }

    [Fact]
    public void Unit_already_at_the_target_arrives_without_turning()
    {
        var (world, tank) = Spawn(UnitType.HeavyTank, Start);
        tank.Facing = BAngle.East;

        Tick(world);

        Assert.Null(tank.MoveTarget);
        Assert.Equal(BAngle.East, tank.Facing);
    }

    [Fact]
    public void Close_target_behind_waits_for_the_turn_before_arriving()
    {
        // The target is within one step, but behind the tank: it turns first, then arrives.
        var (world, tank) = Spawn(UnitType.HeavyTank, Start + new WorldPos(0, 10));

        for (var tick = 1; tick <= 11; tick++)
        {
            Tick(world);
            Assert.NotNull(tank.MoveTarget);
        }

        Tick(world);
        Assert.Null(tank.MoveTarget);
        Assert.Equal(Start + new WorldPos(0, 10), tank.Position);
    }

    [Fact]
    public void Turret_follows_the_hull_until_targeting_exists()
    {
        var (world, tank) = Spawn(UnitType.TeslaTank, Start + new WorldPos(3000, 3000));

        for (var tick = 0; tick < 5; tick++)
        {
            Tick(world);
            Assert.Equal(tank.Facing, tank.TurretFacing);
        }

        Assert.NotEqual(BAngle.North, tank.Facing);
    }

    [Fact]
    public void Faster_turn_rate_reaches_the_target_direction_sooner()
    {
        var (slowWorld, slow) = Spawn(UnitType.HeavyTank, Start + new WorldPos(0, 3000));
        var (fastWorld, fast) = Spawn(UnitType.HeavyTank, Start + new WorldPos(0, 3000));
        fast.TurnRate = 4096;

        for (var tick = 0; tick < 6; tick++)
        {
            Tick(slowWorld);
            Tick(fastWorld);
        }

        Assert.Equal(Start, slow.Position);
        Assert.NotEqual(Start, fast.Position);
    }
}
