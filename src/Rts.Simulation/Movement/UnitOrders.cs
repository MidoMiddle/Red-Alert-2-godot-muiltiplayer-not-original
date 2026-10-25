using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Movement;

/// <summary>Starting a unit toward a point: through the path queue, or straight along a ready-made path.</summary>
internal static class UnitOrders
{
    /// <summary>A new order from the player: forgets waiting, detours and giving way, and asks for a path.</summary>
    public static void MoveTo(GameWorld world, Unit unit, WorldPos target)
    {
        unit.WaitTicks = 0;
        unit.Replans = 0;
        unit.ResumeTarget = null;
        unit.HoldTicks = 0;
        unit.Returning = false;
        var cell = unit.Position.ToCell();
        if (unit.Archetype.Placement == UnitPlacement.Subcell && target.ToCell() == cell && world.Contains(unit.Position))
        {
            // Ordered within its own cell, a soldier moves to the free spot nearest to the click.
            world.Reservations.Leave(cell, unit);
            unit.Subcell = world.Reservations.TryEnter(cell, unit, target);
        }

        RequestPath(world, unit, target, default);
    }

    /// <summary>
    /// A route from the player: replaces the old one, or with <paramref name="append"/> goes on after it. An idle unit, or one
    /// whose route is replaced, starts on the first new leg at once.
    /// </summary>
    public static void GiveRoute(GameWorld world, Unit unit, ImmutableArray<UnitOrder> legs, bool loop, bool append)
    {
        if (append && unit.Orders.Length > 0)
        {
            // Added to a running route; a patrol that gets more legs becomes a plain route.
            unit.Orders = unit.Orders.AddRange(legs);
            unit.LoopOrders = false;
            if (unit.MoveTarget is null && unit.HoldTicks == 0)
            {
                MoveTo(world, unit, unit.Orders[unit.OrderIndex].Point);
            }

            return;
        }

        unit.Orders = legs;
        unit.OrderIndex = 0;
        unit.LoopOrders = loop;
        MoveTo(world, unit, legs[0].Point);
    }

    /// <summary>The unit arrived at the end of its leg: on to the next leg, round again for a patrol, or done.</summary>
    public static void NextLeg(GameWorld world, Unit unit)
    {
        if (unit.Orders.Length == 0)
        {
            return;
        }

        unit.OrderIndex++;
        if (unit.OrderIndex >= unit.Orders.Length)
        {
            if (!unit.LoopOrders)
            {
                // The end of the player's route: a guarding unit guards here (M4, 4.11).
                unit.Orders = [];
                unit.OrderIndex = 0;
                unit.Anchor = unit.Position;
                return;
            }

            unit.OrderIndex = 0;
        }

        MoveTo(world, unit, unit.Orders[unit.OrderIndex].Point);
    }

    /// <summary>Stops the unit where it is: no path, no route, no giving way.</summary>
    public static void Stop(GameWorld world, Unit unit)
    {
        world.PathRequests.Cancel(unit.Id);
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.PathPending = false;
        unit.Orders = [];
        unit.OrderIndex = 0;
        unit.LoopOrders = false;
        unit.ResumeTarget = null;
        unit.HoldTicks = 0;
        unit.Returning = false;
        unit.WaitTicks = 0;
        unit.Replans = 0;
    }

    /// <summary>
    /// Stops the unit's movement but keeps its route (M4, 4.11): it stopped to fire, and goes on with the route when the fight is over.
    /// </summary>
    public static void Halt(GameWorld world, Unit unit)
    {
        world.PathRequests.Cancel(unit.Id);
        unit.MoveTarget = null;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.PathPending = false;
        unit.ResumeTarget = null;
        unit.HoldTicks = 0;
        unit.Returning = false;
        unit.WaitTicks = 0;
        unit.Replans = 0;
    }

    /// <summary>
    /// Forgets the unit's target, the order behind it and the pursuit (M4, 4.12): a new order of the player ends the fight; and the
    /// building it was going into (stage 2e).
    /// </summary>
    public static void ForgetTarget(Unit unit)
    {
        unit.Target = null;
        unit.TargetOrder = TargetOrder.None;
        unit.TargetPoint = null;
        unit.Engaged = false;
        unit.ChaseCell = null;
        unit.EnterTarget = null;
        unit.Deployed = false;
        unit.UndeployIn = 0;
    }

    /// <summary>
    /// Asks for a path to <paramref name="target"/>, keeping out of <paramref name="avoid"/>; the unit waits meanwhile. A flier needs
    /// none: it flies straight (stage 2b); nor a unit that jumps (stage 3b).
    /// </summary>
    public static void RequestPath(GameWorld world, Unit unit, WorldPos target, ImmutableArray<CellPos> avoid)
    {
        unit.MoveTarget = target;
        unit.Path = null;
        unit.PathIndex = 0;
        if (unit.Archetype.Flies || unit.Archetype.Has(UnitTalents.Teleports))
        {
            unit.PathPending = false;
            return;
        }

        unit.PathPending = true;
        world.PathRequests.Request(unit, world.CurrentTick, target.ToCell(), avoid);
    }

    /// <summary>A standing unit is moved off its place: it will come back after the pocket wait, politely.</summary>
    public static void MakeRoom(GameWorld world, Unit unit, ImmutableArray<CellPos> cells)
    {
        if (unit.ResumeTarget is null)
        {
            unit.ResumeTarget = unit.Position;
            unit.Returning = true;
        }

        FollowCells(world, unit, cells);
    }

    /// <summary>Sends the unit along <paramref name="cells"/> (neighbouring cells, the first one its own) to the centre of the last.</summary>
    public static void FollowCells(GameWorld world, Unit unit, ImmutableArray<CellPos> cells)
    {
        world.PathRequests.Cancel(unit.Id);
        unit.MoveTarget = cells[^1].Center;
        unit.Path = new Pathfinding.CellPath(Pathfinding.PathStatus.Found, cells, 0, 0);
        unit.PathIndex = 1;
        unit.PathPending = false;
        unit.WaitTicks = 0;
    }
}
