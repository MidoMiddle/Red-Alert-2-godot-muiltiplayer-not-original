using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Turns units toward where they go, then moves them (docs/implementation/M2_unit_control.md, 4.2 and 4.12).
/// Turning: the hull rotates by at most <see cref="Unit.TurnRate"/> per tick (0 is instant). While more than
/// <see cref="MaxDriveAngle"/> remains after the turn, the unit stands still, like an RA2 tank turning on the spot.
/// Waiting for a path: the unit turns toward its target and stands. With a path: it drives from cell centre to cell centre
/// and ends on the ordered point, or on the centre of the last cell when the goal was unreachable. Speed left over at a
/// waypoint carries on to the next one in the same tick, so corners do not slow the unit down. Without a path (tests set
/// a target directly) it drives straight. Steps are pure integer math: <c>delta * speed / |delta|</c> with an integer
/// square root and round-to-nearest components, never overshooting.
/// Path following here is the simple version taken early from M2 step 8; reservations and avoidance come with that step.
/// Interim rule until then: a unit whose next step would take it into a cell its locomotor cannot enter stops and drops the order.
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
        if (unit.PathPending)
        {
            // The path is still being searched: turn toward the target meanwhile, without driving.
            TurnToward(unit, target);
            return;
        }

        var waypoint = NextWaypoint(unit, target);
        if (waypoint == unit.Position && IsFinal(unit, target))
        {
            // Already there: the zero vector has no direction, so the unit keeps its facing.
            Arrive(world, unit, waypoint, events);
            return;
        }

        if (!TurnToward(unit, waypoint))
        {
            return;
        }

        var remaining = (long)unit.Speed;
        while (remaining > 0)
        {
            waypoint = NextWaypoint(unit, target);
            var delta = waypoint - unit.Position;
            var distanceSquared = delta.LengthSquared;
            if (distanceSquared <= remaining * remaining)
            {
                // Reaches the waypoint this tick; what is left of the speed goes on to the next one.
                if (!TryEnter(world, unit, waypoint, events))
                {
                    return;
                }

                if (IsFinal(unit, target))
                {
                    Arrive(world, unit, waypoint, events);
                    return;
                }

                remaining -= IntMath.Sqrt(distanceSquared);
                unit.PathIndex++;
                continue;
            }

            Step(world, unit, delta, remaining, events);
            return;
        }
    }

    /// <summary>Turns the hull toward <paramref name="point"/>. Returns true when the unit may drive this tick.</summary>
    private static bool TurnToward(Unit unit, WorldPos point)
    {
        var delta = point - unit.Position;
        if (delta == WorldPos.Zero)
        {
            return true;
        }

        var wanted = FixedMath.Angle(delta.X, delta.Y);
        unit.Facing = unit.TurnRate == 0 ? wanted : BAngle.RotateTowards(unit.Facing, wanted, unit.TurnRate);
        unit.TurretFacing = unit.Facing;
        return IntMath.Abs(BAngle.Difference(unit.Facing, wanted)) <= MaxDriveAngle;
    }

    /// <summary>Centre of the next path cell; on the last cell the ordered point, or the cell centre if the goal was unreachable.</summary>
    private static WorldPos NextWaypoint(Unit unit, WorldPos target)
    {
        if (unit.Path is not { } path)
        {
            return target;
        }

        if (unit.PathIndex < path.Cells.Length - 1)
        {
            return path.Cells[unit.PathIndex].Center;
        }

        return path.Status == PathStatus.Found ? target : path.End.Center;
    }

    private static bool IsFinal(Unit unit, WorldPos target) => unit.Path is not { } path || unit.PathIndex >= path.Cells.Length - 1;

    /// <summary>Moves <paramref name="speed"/> leptons along <paramref name="delta"/>, which is longer than that.</summary>
    private static void Step(GameWorld world, Unit unit, WorldPos delta, long speed, List<SimEvent> events)
    {
        // distance > speed here, so each component of the step is no longer than the remaining delta.
        var distance = IntMath.Sqrt(delta.LengthSquared);
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

        TryEnter(world, unit, unit.Position + new WorldPos((int)stepX, (int)stepY), events);
    }

    /// <summary>Moves the unit to <paramref name="next"/>, or stops it when that is a new cell it cannot enter.</summary>
    private static bool TryEnter(GameWorld world, Unit unit, WorldPos next, List<SimEvent> events)
    {
        var cell = next.ToCell();
        if (cell != unit.Position.ToCell() && !world.Map.CanEnter(cell, unit.Archetype.Locomotor))
        {
            unit.MoveTarget = null;
            unit.Path = null;
            events.Add(new UnitBlocked(world.CurrentTick, unit.Id, unit.Position));
            return false;
        }

        unit.Position = next;
        return true;
    }

    private static void Arrive(GameWorld world, Unit unit, WorldPos position, List<SimEvent> events)
    {
        unit.Position = position;
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        events.Add(new UnitArrived(world.CurrentTick, unit.Id, position));
    }
}
