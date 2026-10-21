using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Turns units toward their target, then moves them straight to it (docs/implementation/M2_unit_control.md, 4.2 and 4.12).
/// Turning: the hull rotates by at most <see cref="Unit.TurnRate"/> per tick (0 is instant). While more than
/// <see cref="MaxDriveAngle"/> remains after the turn, the unit stands still, like an RA2 tank turning on the spot.
/// Moving: <see cref="Unit.Speed"/> leptons per tick, pure integer math: the step is <c>delta * speed / |delta|</c>
/// with an integer square root and round-to-nearest components, so every platform lands on the same lepton and
/// diagonal moves keep the full speed. A rounded component never exceeds the remaining delta, so units never overshoot.
/// </summary>
public static class MovementSystem
{
    /// <summary>The largest remaining turn at which a unit already drives and finishes turning on the way: 45°.</summary>
    public const int MaxDriveAngle = BAngle.QuarterTurn / 2;

    public static void Update(GameWorld world, List<SimEvent> events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, MoveTarget: { } target } unit)
            {
                Update(world, unit, target, events);
            }
        }
    }

    private static void Update(GameWorld world, Unit unit, WorldPos target, List<SimEvent> events)
    {
        var delta = target - unit.Position;
        if (delta == WorldPos.Zero)
        {
            // Already there: the zero vector has no direction, so the unit keeps its facing.
            Arrive(world, unit, target, events);
            return;
        }

        var wanted = FixedMath.Angle(delta.X, delta.Y);
        unit.Facing = unit.TurnRate == 0 ? wanted : BAngle.RotateTowards(unit.Facing, wanted, unit.TurnRate);
        unit.TurretFacing = unit.Facing;
        if (IntMath.Abs(BAngle.Difference(unit.Facing, wanted)) > MaxDriveAngle)
        {
            return;
        }

        MoveTowards(world, unit, target, delta, events);
    }

    private static void MoveTowards(GameWorld world, Unit unit, WorldPos target, WorldPos delta, List<SimEvent> events)
    {
        var distanceSquared = delta.LengthSquared;
        var speed = (long)unit.Speed;

        if (distanceSquared <= speed * speed)
        {
            Arrive(world, unit, target, events);
            return;
        }

        // distance >= speed here, so each component of the step is no longer than the remaining delta.
        var distance = IntMath.Sqrt(distanceSquared);
        var stepX = IntMath.DivRound(delta.X * speed, distance);
        var stepY = IntMath.DivRound(delta.Y * speed, distance);

        if (stepX == 0 && stepY == 0)
        {
            // Very slow unit on a diagonal: truncation would freeze it. Take one lepton along the longer axis.
            if (IntMath.Abs(delta.X) >= IntMath.Abs(delta.Y))
            {
                stepX = IntMath.Sign(delta.X);
            }
            else
            {
                stepY = IntMath.Sign(delta.Y);
            }
        }

        unit.Position += new WorldPos((int)stepX, (int)stepY);
        if (unit.Position == target)
        {
            Arrive(world, unit, target, events);
        }
    }

    private static void Arrive(GameWorld world, Unit unit, WorldPos target, List<SimEvent> events)
    {
        unit.Position = target;
        unit.MoveTarget = null;
        events.Add(new UnitArrived(world.CurrentTick, unit.Id, target));
    }
}
