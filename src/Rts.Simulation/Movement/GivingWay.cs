using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Movement;

/// <summary>
/// Making room for another unit (docs/implementation/M2_unit_control.md, 4.7): a breadth-first search for the nearest free cell
/// that is not on the other unit's way ("a pocket"), and the cells a unit is still going to drive through.
/// </summary>
internal static class GivingWay
{
    /// <summary>How far a unit looks for a pocket, in BFS steps.</summary>
    public const int PocketRadius = 12;

    /// <summary>
    /// Cells from the unit to the pocket, starting with its own cell, or default when there is none within
    /// <see cref="PocketRadius"/>. The pocket is the nearest free, enterable cell outside <paramref name="keepClear"/>
    /// (the most open one among equally near cells, then the lowest index);
    /// the way there may cross <paramref name="keepClear"/> (backing out of a corridor) but never another unit's cell.
    /// </summary>
    public static ImmutableArray<CellPos> FindPocket(GameWorld world, Unit unit, HashSet<int> keepClear) =>
        FindPocket(world, unit, keepClear, throughOwnIdle: false);

    /// <summary>
    /// Like <see cref="FindPocket(GameWorld, Unit, HashSet{int})"/>, but the way may pass through cells where idle units of the
    /// same player stand: they will shift one cell along it, like a crowd making room (<see cref="ShiftChain"/>).
    /// </summary>
    public static ImmutableArray<CellPos> FindPocketThroughCrowd(GameWorld world, Unit unit, HashSet<int> keepClear) =>
        FindPocket(world, unit, keepClear, throughOwnIdle: true);

    /// <summary>
    /// Moves every unit standing on <paramref name="way"/> one cell forward along it, the one nearest to the free end first
    /// in effect: each only drives once the cell ahead is free. The first cell is the unit that was asked to make room.
    /// Each comes back to its place after the pocket wait.
    /// </summary>
    public static void ShiftChain(GameWorld world, ImmutableArray<CellPos> way)
    {
        for (var i = way.Length - 2; i >= 0; i--)
        {
            if (world.TryGet(world.Reservations.Blocker(way[i]), out var entity) && entity is Unit unit)
            {
                UnitOrders.MakeRoom(world, unit, [way[i], way[i + 1]]);
            }
        }
    }

    private static ImmutableArray<CellPos> FindPocket(GameWorld world, Unit unit, HashSet<int> keepClear, bool throughOwnIdle)
    {
        var map = world.Map;
        var locomotor = unit.Archetype.Locomotor;
        var start = unit.Position.ToCell();
        var startIndex = start.Y * map.Width + start.X;
        var parent = new Dictionary<int, int> { [startIndex] = -1 };
        var queue = new List<(int Index, int Depth)> { (startIndex, 0) };

        var best = -1;
        var bestDepth = 0;
        var bestOpenness = -1;
        for (var head = 0; head < queue.Count; head++)
        {
            var (index, depth) = queue[head];
            if (best >= 0 && depth > bestDepth)
            {
                break;
            }

            var cell = new CellPos(index % map.Width, index / map.Width);
            if (index != startIndex && !keepClear.Contains(index) && world.Reservations.HasRoomFor(cell, unit))
            {
                // Among the nearest pockets, the most open one: stepping aside into a narrow pass would plug it.
                var openness = Openness(map, cell, locomotor);
                if (openness > bestOpenness || (openness == bestOpenness && index < best))
                {
                    best = index;
                    bestDepth = depth;
                    bestOpenness = openness;
                }

                continue;
            }

            if (depth == PocketRadius)
            {
                continue;
            }

            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (!AStar.CanStep(map, cell, dx, dy, locomotor))
                {
                    continue;
                }

                var nextIndex = next.Y * map.Width + next.X;
                if (parent.ContainsKey(nextIndex)
                    || (!world.Reservations.HasRoomFor(next, unit) && !(throughOwnIdle && IsSingleOwnIdle(world, next, unit.Owner))))
                {
                    continue;
                }

                parent[nextIndex] = index;
                queue.Add((nextIndex, depth + 1));
            }
        }

        return best >= 0 ? BuildWay(parent, best, map.Width) : default;
    }

    /// <summary>How many of the 8 neighbours of the cell the locomotor can step to.</summary>
    private static int Openness(MapGrid map, CellPos cell, Locomotor locomotor)
    {
        var open = 0;
        foreach (var (dx, dy) in AStar.Directions)
        {
            open += AStar.CanStep(map, cell, dx, dy, locomotor) ? 1 : 0;
        }

        return open;
    }

    /// <summary>
    /// The free cell nearest to <paramref name="goal"/> that the unit could stand on, found by a breadth-first search from the
    /// goal over enterable cells (no corner cutting), within <see cref="PocketRadius"/>; null when there is none.
    /// </summary>
    public static CellPos? NearestFreeCell(GameWorld world, Unit unit, CellPos goal)
    {
        var map = world.Map;
        var locomotor = unit.Archetype.Locomotor;
        var seen = new HashSet<CellPos> { goal };
        var queue = new List<(CellPos Cell, int Depth)> { (goal, 0) };
        for (var head = 0; head < queue.Count; head++)
        {
            var (cell, depth) = queue[head];
            if (cell != goal && map.CanEnter(cell, locomotor) && world.Reservations.HasRoomFor(cell, unit))
            {
                return cell;
            }

            if (depth == PocketRadius)
            {
                continue;
            }

            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (AStar.CanStep(map, cell, dx, dy, locomotor) && seen.Add(next))
                {
                    queue.Add((next, depth + 1));
                }
            }
        }

        return null;
    }

    /// <summary>Cell indices the unit will still pass: its cell, the segments to its remaining waypoints, its final cell.</summary>
    public static HashSet<int> RemainingCells(GameWorld world, Unit unit)
    {
        var width = world.Map.Width;
        var cells = new HashSet<int>();
        var current = unit.Position.ToCell();
        cells.Add(current.Y * width + current.X);
        if (unit.MoveTarget is { } target)
        {
            var end = target.ToCell();
            cells.Add(end.Y * width + end.X);
        }

        if (unit.Path is not { } path)
        {
            return cells;
        }

        var from = current;
        for (var i = IntMath.Max(unit.PathIndex, 1); i < path.Waypoints.Length; i++)
        {
            foreach (var cell in GridLine.Cells(from, path.Waypoints[i]))
            {
                cells.Add(cell.Y * width + cell.X);
            }

            from = path.Waypoints[i];
        }

        return cells;
    }

    /// <summary>One idle unit of <paramref name="owner"/> stands in the cell, nobody else: the crowd can shift it along.</summary>
    private static bool IsSingleOwnIdle(GameWorld world, CellPos cell, PlayerId owner)
    {
        var reservations = world.Reservations;
        var single = reservations.VehicleAt(cell) != EntityId.None
            ? reservations.InfantryCount(cell) == 0
            : reservations.InfantryCount(cell) == 1;
        return single && world.TryGet(reservations.Blocker(cell), out var entity) && entity is Unit { MoveTarget: null } other && other.Owner == owner;
    }

    private static ImmutableArray<CellPos> BuildWay(Dictionary<int, int> parent, int end, int width)
    {
        var cells = new List<CellPos>();
        for (var index = end; index != -1; index = parent[index])
        {
            cells.Add(new CellPos(index % width, index / width));
        }

        cells.Reverse();
        return [.. cells];
    }
}
