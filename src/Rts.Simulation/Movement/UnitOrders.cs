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
        RequestPath(world, unit, target, default);
    }

    /// <summary>Asks for a path to <paramref name="target"/>, keeping out of <paramref name="avoid"/>; the unit waits meanwhile.</summary>
    public static void RequestPath(GameWorld world, Unit unit, WorldPos target, ImmutableArray<CellPos> avoid)
    {
        unit.MoveTarget = target;
        unit.Path = null;
        unit.PathIndex = 0;
        unit.PathPending = true;
        world.PathRequests.Request(unit, world.CurrentTick, target.ToCell(), avoid);
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
