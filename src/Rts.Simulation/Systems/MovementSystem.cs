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
/// <item>One unit per cell, or five soldiers (<see cref="Reservations"/>). When the next cell is taken: if it is the last cell
/// and its unit stands, one of ours makes room, otherwise the mover heads for the nearest free cell next to the goal, or stops
/// where it is if there is none ("arrived as far as possible"); a unit coming head-on makes the one with the larger id give way
/// into a pocket; a unit that is giving way is waited for (only the stall rule counts); another moving unit is waited for, and after
/// <see cref="WaitBeforeDetour"/> ticks driven around; a standing
/// unit of the same player is first driven around, and if there is no way around, asked to step aside into a pocket (it comes
/// back to its place later); when its own idle units
/// hem it in, the line of them shifts one cell toward the nearest free cell; a unit only going back to its place never asks
/// others to make room; a standing enemy is driven around at once.</item>
/// <item>No endless jams: after <see cref="StallTicks"/> ticks without moving the unit asks for a path around every taken cell
/// nearby; after <see cref="MaxReplans"/> such paths (or detours around standing units) without reaching a new cell it stops
/// where it is. Waiting behind moving units never counts: a queue at a narrow pass keeps its place.</item>
/// <item>A step into a cell the locomotor cannot enter (the unit is off the line its path was planned on) makes it plan again
/// from where it stands (<see cref="UnitBlocked"/>), counted like the other replans.</item>
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

    /// <summary>How near its place (in cells, either axis) a standing unit must be for a blocked unit to swap places with it.</summary>
    public const int SwapRadius = 8;

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
            // Held by a temporal beam (stage 3a), a unit stands still; so does a deployed one (stage 3b).
            if (entity is not Unit { IsAlive: true, ErasedBy: null, Deployed: false } unit)
            {
                continue;
            }

            // Phasing in after a jump (stage 3b), it waits.
            if (unit.WarpTicks > 0)
            {
                unit.WarpTicks--;
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
        if (unit.Archetype.Flies)
        {
            Fly(world, unit, target, events);
            return;
        }

        if (unit.Archetype.Has(UnitTalents.Teleports))
        {
            Teleport(world, unit, target, events);
            return;
        }

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

    /// <summary>
    /// A flier (stage 2b) flies straight at its speed, over land, water and cliffs alike: no path, no cell taken, nothing crushed,
    /// nothing in its way.
    /// </summary>
    private static void Fly(GameWorld world, Unit unit, WorldPos target, List<SimEvent> events)
    {
        if (unit.Position == target)
        {
            Arrive(world, unit, target, events);
            return;
        }

        if (!TurnToward(unit, target))
        {
            return;
        }

        var delta = target - unit.Position;
        if (delta.LengthSquared <= (long)unit.Speed * unit.Speed)
        {
            Arrive(world, unit, target, events);
            return;
        }

        unit.Position += StepAlong(delta, unit.Speed);
        unit.WaitTicks = 0;
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
            // Off the line its path was planned on (it gave way, came back to its place), the straight way to the next
            // waypoint can clip an obstacle: plan again from where it stands (M2 step 15; it used to stop for good).
            events.Add(new UnitBlocked(world.CurrentTick, unit.Id, unit.Position));
            Replan(world, unit, [], events);
            return MoveResult.Stopped;
        }

        // A crushing vehicle drives over the enemy infantry of the cell (M4 step 8): their spots are free now.
        CrushSystem.TryCrush(world, unit, cell, events);

        // A soldier takes the spot nearest to where it stops in its last cell, elsewhere the one nearest to where it comes in.
        var finalPoint = FinalPoint(unit);
        var subcell = world.Reservations.TryEnter(cell, unit, cell == finalPoint.ToCell() ? finalPoint : next);
        if (subcell >= 0)
        {
            world.Reservations.Leave(current, unit);
            unit.Subcell = subcell;
            unit.Position = next;
            unit.Replans = 0;

            // Driving into a cell flattens what this unit crushes there: a fence under tracks, a lamp post under anyone (M7 step 5).
            world.CrushMapObjects(cell, unit.Archetype.Locomotor, events);
            return MoveResult.Moved;
        }

        return HandleTakenCell(world, unit, cell, events);
    }

    private static MoveResult HandleTakenCell(GameWorld world, Unit unit, CellPos cell, List<SimEvent> events)
    {
        world.TryGet(world.Reservations.Blocker(cell), out var entity);
        var other = (Unit)entity;
        var otherMoving = other.MoveTarget is not null;

        if (!otherMoving && cell == FinalPoint(unit).ToCell())
        {
            // One of ours stands on the goal: most likely it was pushed off its own place, so it makes room (and later
            // looks for a place of its own politely). Otherwise take the nearest free cell next to the goal; stopping right
            // here could plug a narrow pass for everybody behind.
            if (other.Owner == unit.Owner && !unit.Returning && !IsSettled(other, unit))
            {
                var room = GivingWay.FindPocket(world, other, GivingWay.RemainingCells(world, unit));
                if (!room.IsDefault)
                {
                    UnitOrders.MakeRoom(world, other, room);
                    return MoveResult.Blocked;
                }
            }

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
                // It is giving way (maybe to us): wait for it rather than drive around. The stall rule still counts, so a
                // unit that gives way and gets stuck itself cannot hold anybody forever.
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

        if (TrySwapPlaces(world, unit, other, events))
        {
            return MoveResult.Blocked;
        }

        if (other.Owner == unit.Owner && unit.Replans == 0)
        {
            // First try to drive around our own standing unit, like around anything else: on open ground nobody needs to
            // move. Only when that comes back to the same cell (no way around) is the unit asked to make room.
            Replan(world, unit, [cell], events);
            return MoveResult.Blocked;
        }

        if (other.Owner == unit.Owner && !unit.Returning)
        {
            var keepClear = GivingWay.RemainingCells(world, unit);
            var pocket = GivingWay.FindPocket(world, other, keepClear);
            if (!pocket.IsDefault)
            {
                // A standing unit of ours steps aside and comes back to its place afterwards (or to the target it was already
                // going back to), so a group keeps the cells it was given.
                UnitOrders.MakeRoom(world, other, pocket);
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

    /// <summary>
    /// A unit blocked by one of ours that already stands nearer the unit's place gives it that place and takes the standing one's
    /// spot instead (M2, after the closing): the standing one moves one step deeper, and if it is blocked in turn, it swaps
    /// again. So a late unit of a group never has to get through the units already standing around its place — before, they
    /// stepped aside and came back, which in a big group turned into a long shuffle at the target. Only for units of the same
    /// kind of place (vehicle with vehicle, soldier with soldier), on the last leg of a route, near the place
    /// (<see cref="SwapRadius"/>), and with a standing unit that is not giving way or going back. The place always moves to a
    /// unit strictly nearer to it, so swaps cannot go round in circles.
    /// </summary>
    private static bool TrySwapPlaces(GameWorld world, Unit unit, Unit other, List<SimEvent> events)
    {
        if (other.Owner != unit.Owner || other.Archetype.Placement != unit.Archetype.Placement
            || other.MoveTarget is not null || other.PathPending || other.HoldTicks > 0 || other.ResumeTarget is not null || other.Returning
            || unit.Returning || unit.ResumeTarget is not null || unit.Engaged || unit.MoveTarget is not { } place
            || (unit.Orders.Length > 0 && (unit.LoopOrders || unit.OrderIndex != unit.Orders.Length - 1)))
        {
            return false;
        }

        var placeCell = place.ToCell();
        var otherDistance = Chebyshev(other.Position.ToCell(), placeCell);
        if (otherDistance >= Chebyshev(unit.Position.ToCell(), placeCell) || otherDistance > SwapRadius)
        {
            return false;
        }

        var attackMove = unit.Orders.Length > 0 && unit.Orders[unit.OrderIndex].AttackMove;
        UnitOrders.GiveRoute(world, other, [new UnitOrder(place, attackMove)], loop: false, append: false);
        var spot = other.Position;
        if (unit.Orders.Length > 0)
        {
            unit.Orders = unit.Orders.SetItem(unit.OrderIndex, new UnitOrder(spot, attackMove));
        }

        UnitOrders.MoveTo(world, unit, spot);
        events.Add(new UnitPlacesSwapped(world.CurrentTick, unit.Id, other.Id));
        return true;
    }

    /// <summary>Stands still of its own accord, with the same kind of place: it keeps it, the newcomer stands next to it.</summary>
    private static bool IsSettled(Unit other, Unit unit) =>
        other.Archetype.Placement == unit.Archetype.Placement && other.MoveTarget is null && !other.PathPending
        && other.HoldTicks == 0 && other.ResumeTarget is null && !other.Returning;

    private static int Chebyshev(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));

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
                if (world.Map.Contains(cell) && cell != center && !world.Reservations.HasRoomFor(cell, unit))
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
        // A turret that aims at a target stays on it while the hull turns (M4 step 3).
        if (!unit.Archetype.HasTurret || unit.Target is null)
        {
            unit.TurretFacing = unit.Facing;
        }
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
    /// <remarks>A soldier with a path, once inside its last cell, stops on the spot it holds there.</remarks>
    private static WorldPos FinalPoint(Unit unit)
    {
        var point = unit.Path is { Status: not PathStatus.Found } path ? path.End.Center : unit.MoveTarget ?? unit.Position;
        return unit.Path is not null && unit.Archetype.Placement == UnitPlacement.Subcell && unit.Position.ToCell() == point.ToCell()
            ? Reservations.SubcellPoint(point.ToCell(), unit.Subcell)
            : point;
    }

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

    /// <summary>
    /// A unit that jumps (stage 3b: the time trooper, RA2 Chrono Legionnaire) appears at once in the free cell nearest to its target,
    /// within <see cref="JumpSearch"/> rings, and phases in for its distance in leptons over <see cref="WarpFactor"/> ticks, at least
    /// <see cref="MinWarp"/> (RA2 <c>ChronoDistanceFactor</c>, <c>ChronoMinimumDelay</c>). With no free cell there it stays.
    /// </summary>
    private static void Teleport(GameWorld world, Unit unit, WorldPos target, List<SimEvent> events)
    {
        var from = unit.Position;
        if (Landing(world, unit, target.ToCell()) is { } cell)
        {
            if (world.Contains(from))
            {
                world.Reservations.Leave(from.ToCell(), unit);
            }

            var subcell = world.Reservations.TryEnter(cell, unit, target);
            unit.Subcell = IntMath.Max(0, subcell);
            var landed = unit.Archetype.Placement == UnitPlacement.Subcell && subcell >= 0 ? Reservations.SubcellPoint(cell, subcell) : cell.Center;
            unit.WarpTicks = IntMath.Max(MinWarp, Combat.DamageCalculator.Distance(from, landed) / WarpFactor);
            events.Add(new UnitTeleported(world.CurrentTick, unit.Id, from, landed));
            Arrive(world, unit, landed, events);
            return;
        }

        Arrive(world, unit, from, events);
    }

    /// <summary>How far round its target a jumping unit looks for a free cell, in rings.</summary>
    public const int JumpSearch = 3;

    /// <summary>Leptons of a jump per tick of phasing in (RA2 <c>ChronoDistanceFactor=48</c>).</summary>
    public const int WarpFactor = 48;

    /// <summary>The least ticks of phasing in (RA2 <c>ChronoMinimumDelay=16</c>).</summary>
    public const int MinWarp = 16;

    private static CellPos? Landing(GameWorld world, Unit unit, CellPos target)
    {
        for (var ring = 0; ring <= JumpSearch; ring++)
        {
            for (var y = target.Y - ring; y <= target.Y + ring; y++)
            {
                for (var x = target.X - ring; x <= target.X + ring; x++)
                {
                    var cell = new CellPos(x, y);
                    if (IntMath.Max(IntMath.Abs(x - target.X), IntMath.Abs(y - target.Y)) == ring && world.Map.Contains(cell)
                        && world.Map.CanEnter(cell, unit.Archetype.Locomotor) && world.Reservations.HasRoomFor(cell, unit))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    private static void Arrive(GameWorld world, Unit unit, WorldPos position, List<SimEvent> events)
    {
        unit.Position = position;
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.WaitTicks = 0;
        unit.Replans = 0;
        events.Add(new UnitArrived(world.CurrentTick, unit.Id, position));
        if (unit.ResumeTarget is not null)
        {
            // In the pocket: wait a little, then go back to the original target.
            unit.HoldTicks = PocketHoldTicks;
        }
        else
        {
            unit.Returning = false;
            if (!unit.Engaged)
            {
                // A unit that drove to a fight is not at the end of its leg (M4 step 7): its route waits for the fight.
                UnitOrders.NextLeg(world, unit);
            }
        }
    }
}
