using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Moves units along their paths without driving through each other (docs/implementation/M2_unit_control.md, 4.2, 4.7, 4.12).
/// Units are handled in <see cref="EntityId"/> order; for each one: turn, try to take the next cell, move at the speed of the
/// land under it, free the old cell, arrive.
/// <list type="bullet">
/// <item>Turning: the hull rotates by at most <see cref="Unit.TurnRate"/> per tick; while more than <see cref="MaxDriveAngle"/>
/// remains, the unit stands, like an RA2 tank turning on the spot. Waiting for a path, it turns toward the target.</item>
/// <item>Driving: straight from waypoint to waypoint of the smoothed path; leftover speed carries on past a waypoint. Without a
/// path (tests set a target directly) it drives straight. Integer steps <c>delta * speed / |delta|</c>, never overshooting.</item>
/// <item>One unit per cell (<see cref="Reservations"/>). When the next cell is taken: if it is the last cell and its unit stands,
/// the mover heads for the nearest free cell next to the goal, or stops where it is if there is none ("arrived as far as possible"); a unit coming head-on makes the one with the larger id give way
/// into a pocket; a unit that is giving way is simply waited for; another moving unit is waited for, and after
/// <see cref="WaitBeforeDetour"/> ticks driven around; a standing
/// unit of the same player is asked to step aside into a pocket, and when its own idle units hem it in, the line of them
/// shifts one cell toward the nearest free cell; a standing enemy is driven around at once.</item>
/// <item>No endless jams: after <see cref="StallTicks"/> ticks without moving the unit asks for a path around every taken cell
/// nearby; after <see cref="MaxReplans"/> such paths (or detours around standing units) without reaching a new cell it stops
/// where it is. Waiting behind moving units never counts: a queue at a narrow pass keeps its place.</item>
/// <item>Interim rule kept as a safety net: a step into a cell the locomotor cannot enter stops the unit (<see cref="UnitBlocked"/>).</item>
/// </list>
/// </summary>
public static class MovementSystem
{
    /// <summary>The largest remaining turn at which a unit already drives and finishes turning on the way: 45°.</summary>
    public const int MaxDriveAngle = BAngle.QuarterTurn / 2;

    /// <summary>Ticks to wait for a moving unit in the way before looking for a way around it.</summary>
    public const int WaitBeforeDetour = 8;

    /// <summary>Ticks without moving after which the unit looks for a way around every taken cell nearby.</summary>
    public const int StallTicks = 30;

    /// <summary>Paths around other units in a row (without reaching a new cell) before the unit gives up and stops.</summary>
    public const int MaxReplans = 3;

    /// <summary>Ticks a unit that gave way waits in its pocket before heading for its target again.</summary>
    public const int PocketHoldTicks = 15;

    /// <summary>Radius in cells of the taken cells avoided after a stall.</summary>
    private const int StallAvoidRadius = 2;

    private enum MoveResult
    {
        Moved,
        Blocked,
        Stopped,
    }

    public static void Update(GameWorld world, List<SimEvent> events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            if (unit.MoveTarget is { } target)
            {
                Update(world, unit, target, events);
            }
            else if (unit.HoldTicks > 0 && --unit.HoldTicks == 0 && unit.ResumeTarget is { } resume)
            {
                // Waited long enough in the pocket: back on the way to the original target.
                unit.ResumeTarget = null;
                UnitOrders.RequestPath(world, unit, resume, default);
            }
        }
    }

    private static void Update(GameWorld world, Unit unit, WorldPos target, List<SimEvent> events)
    {
        if (unit.PathPending)
        {
            TurnToward(unit, target);
            return;
        }

        var waypoint = NextWaypoint(unit, target);
        if (waypoint == unit.Position && IsFinal(unit))
        {
            Arrive(world, unit, waypoint, events);
            return;
        }

        if (!TurnToward(unit, waypoint))
        {
            return;
        }

        var start = unit.Position;
        var remaining = (long)LandSpeed(world, unit);
        while (remaining > 0)
        {
            waypoint = NextWaypoint(unit, target);
            var delta = waypoint - unit.Position;
            var distanceSquared = delta.LengthSquared;
            MoveResult result;
            if (distanceSquared <= remaining * remaining)
            {
                // Reaches the waypoint this tick; what is left of the speed goes on to the next one.
                result = TryMoveTo(world, unit, waypoint, events);
                if (result != MoveResult.Moved)
                {
                    break;
                }

                if (IsFinal(unit))
                {
                    Arrive(world, unit, waypoint, events);
                    return;
                }

                remaining -= IntMath.Sqrt(distanceSquared);
                unit.PathIndex++;
                continue;
            }

            TryMoveTo(world, unit, unit.Position + StepAlong(delta, remaining), events);
            break;
        }

        if (unit.MoveTarget is null)
        {
            return;
        }

        if (unit.Position != start)
        {
            unit.WaitTicks = 0;
            return;
        }

        if (++unit.WaitTicks >= StallTicks && !unit.PathPending)
        {
            Replan(world, unit, TakenCellsAround(world, unit), events);
        }
    }

    /// <summary>Moves to <paramref name="next"/>, taking its cell if it is a new one; handles a taken cell.</summary>
    private static MoveResult TryMoveTo(GameWorld world, Unit unit, WorldPos next, List<SimEvent> events)
    {
        var current = unit.Position.ToCell();
        var cell = next.ToCell();
        if (cell == current)
        {
            unit.Position = next;
            return MoveResult.Moved;
        }

        if (!world.Map.CanEnter(cell, unit.Archetype.Locomotor))
        {
            unit.MoveTarget = null;
            unit.Path = null;
            events.Add(new UnitBlocked(world.CurrentTick, unit.Id, unit.Position));
            return MoveResult.Stopped;
        }

        if (world.Reservations.TryOccupy(cell, unit.Id))
        {
            world.Reservations.Release(current, unit.Id);
            unit.Position = next;
            unit.Replans = 0;
            return MoveResult.Moved;
        }

        return HandleTakenCell(world, unit, cell, events);
    }

    private static MoveResult HandleTakenCell(GameWorld world, Unit unit, CellPos cell, List<SimEvent> events)
    {
        world.TryGet(world.Reservations.OwnerOf(cell), out var entity);
        var other = (Unit)entity;
        var otherMoving = other.MoveTarget is not null;

        if (!otherMoving && cell == FinalPoint(unit).ToCell())
        {
            // Somebody stands on the goal: take the nearest free cell next to it instead of pushing units along in a chain.
            // Stopping right here could plug a narrow pass for everybody behind.
            var nearGoal = GivingWay.NearestFreeCell(world, unit, cell);
            if (nearGoal is { } free && unit.Replans < MaxReplans)
            {
                unit.Replans++;
                UnitOrders.RequestPath(world, unit, free.Center, default);
                return MoveResult.Blocked;
            }

            Arrive(world, unit, unit.Position, events);
            return MoveResult.Stopped;
        }

        if (otherMoving)
        {
            if (other.ResumeTarget is not null)
            {
                // It is giving way (maybe to us): wait for it without counting toward a detour or a stall.
                unit.WaitTicks = 0;
                return MoveResult.Blocked;
            }

            if (!other.PathPending && NextCell(other) == unit.Position.ToCell() && unit.Id.CompareTo(other.Id) > 0)
            {
                // Head-on: the larger id gives way. The smaller one just waits for that.
                GiveWay(world, unit, GivingWay.RemainingCells(world, other));
                return MoveResult.Blocked;
            }

            if (unit.WaitTicks + 1 == WaitBeforeDetour && cell != FinalPoint(unit).ToCell())
            {
                // Look for a way around once per wait; it does not count toward giving up: a moving unit will clear the way.
                Replan(world, unit, [cell], events, counts: false);
            }

            return MoveResult.Blocked;
        }

        if (other.Owner == unit.Owner)
        {
            var keepClear = GivingWay.RemainingCells(world, unit);
            var pocket = GivingWay.FindPocket(world, other, keepClear);
            if (!pocket.IsDefault)
            {
                // A standing unit of ours steps aside; it keeps any target it was going back to after giving way itself.
                UnitOrders.FollowCells(world, other, pocket);
                return MoveResult.Blocked;
            }

            var crowdWay = GivingWay.FindPocketThroughCrowd(world, other, keepClear);
            if (!crowdWay.IsDefault)
            {
                // Hemmed in by our own idle units: the whole line shifts one cell toward the nearest free one.
                GivingWay.ShiftChain(world, crowdWay);
                return MoveResult.Blocked;
            }
        }

        Replan(world, unit, [cell], events);
        return MoveResult.Blocked;
    }

    /// <summary>Drives into the nearest pocket off the other unit's way, remembering where to go afterwards.</summary>
    private static void GiveWay(GameWorld world, Unit unit, HashSet<int> keepClear)
    {
        var pocket = GivingWay.FindPocket(world, unit, keepClear);
        if (pocket.IsDefault)
        {
            return;
        }

        unit.ResumeTarget ??= unit.MoveTarget;
        UnitOrders.FollowCells(world, unit, pocket);
    }

    /// <summary>Asks for a path around <paramref name="avoid"/>; after <see cref="MaxReplans"/> in a row the unit stops where it is.</summary>
    /// <remarks>The goal cell is never avoided: a path that may not end on the goal would only lead somewhere else.</remarks>
    private static void Replan(GameWorld world, Unit unit, ImmutableArray<CellPos> avoid, List<SimEvent> events, bool counts = true)
    {
        if (counts && unit.Replans >= MaxReplans)
        {
            Arrive(world, unit, unit.Position, events);
            return;
        }

        var goal = unit.MoveTarget!.Value;
        unit.Replans += counts ? 1 : 0;
        unit.WaitTicks = 0;
        UnitOrders.RequestPath(world, unit, goal, avoid.RemoveAll(cell => cell == goal.ToCell()));
    }

    /// <summary>Cells within <see cref="StallAvoidRadius"/> taken by other units, in index order.</summary>
    private static ImmutableArray<CellPos> TakenCellsAround(GameWorld world, Unit unit)
    {
        var center = unit.Position.ToCell();
        var taken = ImmutableArray.CreateBuilder<CellPos>();
        for (var y = center.Y - StallAvoidRadius; y <= center.Y + StallAvoidRadius; y++)
        {
            for (var x = center.X - StallAvoidRadius; x <= center.X + StallAvoidRadius; x++)
            {
                var cell = new CellPos(x, y);
                if (world.Map.Contains(cell) && world.Reservations.OwnerOf(cell) is var owner && owner != EntityId.None && owner != unit.Id)
                {
                    taken.Add(cell);
                }
            }
        }

        return taken.ToImmutable();
    }

    /// <summary>The cell the unit is about to enter: one cell length toward its next waypoint.</summary>
    private static CellPos? NextCell(Unit unit)
    {
        if (unit.MoveTarget is not { } target)
        {
            return null;
        }

        var delta = NextWaypoint(unit, target) - unit.Position;
        if (delta == WorldPos.Zero)
        {
            return null;
        }

        var distance = IntMath.Sqrt(delta.LengthSquared);
        var reach = IntMath.Min(distance, WorldPos.LeptonsPerCell);
        var probe = unit.Position + StepAlong(delta, reach);
        return probe.ToCell() == unit.Position.ToCell() ? null : probe.ToCell();
    }

    /// <summary>Speed this tick: the unit's speed times the land speed of its cell.</summary>
    private static int LandSpeed(GameWorld world, Unit unit)
    {
        var percent = world.Map.SpeedPercent(unit.Position.ToCell(), unit.Archetype.Locomotor);
        return percent == 0 ? unit.Speed : (int)IntMath.DivRound((long)unit.Speed * percent, 100);
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

    /// <summary>Centre of the next waypoint; on the last one the final point.</summary>
    private static WorldPos NextWaypoint(Unit unit, WorldPos target)
    {
        if (unit.Path is not { } path)
        {
            return target;
        }

        return unit.PathIndex < path.Waypoints.Length - 1 ? path.Waypoints[unit.PathIndex].Center : FinalPoint(unit);
    }

    /// <summary>Where the unit stops: the ordered point, or the centre of the last cell when the goal was unreachable.</summary>
    private static WorldPos FinalPoint(Unit unit) =>
        unit.Path is { Status: not PathStatus.Found } path ? path.End.Center : unit.MoveTarget ?? unit.Position;

    private static bool IsFinal(Unit unit) => unit.Path is not { } path || unit.PathIndex >= path.Waypoints.Length - 1;

    /// <summary>A step of <paramref name="length"/> leptons along <paramref name="delta"/>, which is at least that long.</summary>
    private static WorldPos StepAlong(WorldPos delta, long length)
    {
        var distance = IntMath.Sqrt(delta.LengthSquared);
        var stepX = IntMath.DivRound(delta.X * length, distance);
        var stepY = IntMath.DivRound(delta.Y * length, distance);
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

        return new WorldPos((int)stepX, (int)stepY);
    }

    private static void Arrive(GameWorld world, Unit unit, WorldPos position, List<SimEvent> events)
    {
        unit.Position = position;
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.WaitTicks = 0;
        unit.Replans = 0;
        if (unit.ResumeTarget is not null)
        {
            // In the pocket: wait a little, then go back to the original target.
            unit.HoldTicks = PocketHoldTicks;
        }

        events.Add(new UnitArrived(world.CurrentTick, unit.Id, position));
    }
}
