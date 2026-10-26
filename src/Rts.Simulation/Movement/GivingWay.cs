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
/// that is not on the other unit's way ("a pocket"), and the cells a unit is still going to drive through. The searches go over
/// the nodes of the path search: the cells, then the cells of high bridges' decks (<see cref="Decks"/>), so a unit on a deck looks for
/// room along the deck and off its ends, and one under it never on it.
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
    public static void ShiftChain(GameWorld world, ImmutableArray<CellPos> way, Unit asked)
    {
        var levels = world.Map.DeckCount > 0 ? Decks.Levels(world.Map, way, asked.OnDeck, asked.Archetype.Locomotor) : null;
        for (var i = way.Length - 2; i >= 0; i--)
        {
            if (world.TryGet(world.Reservations.Blocker(way[i], levels is not null && levels[i]), out var entity) && entity is Unit unit)
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
        var startIndex = unit.OnDeck && map.DeckSlot(start) is var startSlot and >= 0 ? map.Width * map.Height + startSlot : start.Y * map.Width + start.X;
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

            var (cell, onDeck) = NodeOf(map, index);
            if (index != startIndex && !keepClear.Contains(cell.Y * map.Width + cell.X) && world.Reservations.HasRoomFor(cell, unit, onDeck))
            {
                // Among the nearest pockets, the most open one: stepping aside into a narrow pass would plug it.
                var openness = Openness(map, cell, onDeck, locomotor);
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
                if (!Decks.TryStep(map, cell, onDeck, dx, dy, locomotor, out var nextOnDeck))
                {
                    continue;
                }

                var nextIndex = NodeIndex(map, next, nextOnDeck);
                if (parent.ContainsKey(nextIndex)
                    || (!world.Reservations.HasRoomFor(next, unit, nextOnDeck) && !(throughOwnIdle && IsSingleOwnIdle(world, next, nextOnDeck, unit.Owner))))
                {
                    continue;
                }

                parent[nextIndex] = index;
                queue.Add((nextIndex, depth + 1));
            }
        }

        return best >= 0 ? BuildWay(map, parent, best) : default;
    }

    /// <summary>How many of the 8 neighbours of the cell the locomotor can step to from its level.</summary>
    private static int Openness(MapGrid map, CellPos cell, bool onDeck, Locomotor locomotor)
    {
        var open = 0;
        foreach (var (dx, dy) in AStar.Directions)
        {
            open += Decks.TryStep(map, cell, onDeck, dx, dy, locomotor, out _) ? 1 : 0;
        }

        return open;
    }

    // A node of the search: a cell (y * width + x) or, past them, a cell of a deck by its slot.
    private static int NodeIndex(MapGrid map, CellPos cell, bool onDeck) =>
        onDeck ? map.Width * map.Height + map.DeckSlot(cell) : cell.Y * map.Width + cell.X;

    private static (CellPos Cell, bool OnDeck) NodeOf(MapGrid map, int index) =>
        index < map.Width * map.Height ? (new CellPos(index % map.Width, index / map.Width), false) : (map.DeckCell(index - map.Width * map.Height), true);

    /// <summary>
    /// The free cell nearest to <paramref name="goal"/> that the unit could stand on, found by a breadth-first search from the
    /// goal over enterable cells (no corner cutting), within <see cref="PocketRadius"/>; null when there is none.
    /// </summary>
    /// <remarks>The search starts on the level of the goal (<paramref name="goalOnDeck"/>) and keeps to the step rule of <see cref="Decks"/>.</remarks>
    public static CellPos? NearestFreeCell(GameWorld world, Unit unit, CellPos goal, bool goalOnDeck = false)
    {
        var map = world.Map;
        var locomotor = unit.Archetype.Locomotor;
        goalOnDeck &= map.IsDeck(goal);
        var seen = new HashSet<int> { NodeIndex(map, goal, goalOnDeck) };
        var queue = new List<(CellPos Cell, bool OnDeck, int Depth)> { (goal, goalOnDeck, 0) };
        for (var head = 0; head < queue.Count; head++)
        {
            var (cell, onDeck, depth) = queue[head];
            if (cell != goal && Decks.CanStand(map, cell, onDeck, locomotor) && world.Reservations.HasRoomFor(cell, unit, onDeck))
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
                if (Decks.TryStep(map, cell, onDeck, dx, dy, locomotor, out var nextOnDeck) && seen.Add(NodeIndex(map, next, nextOnDeck)))
                {
                    queue.Add((next, nextOnDeck, depth + 1));
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
    private static bool IsSingleOwnIdle(GameWorld world, CellPos cell, bool onDeck, PlayerId owner)
    {
        var reservations = world.Reservations;
        var single = reservations.VehicleAt(cell, onDeck) != EntityId.None
            ? reservations.InfantryCount(cell, onDeck) == 0
            : reservations.InfantryCount(cell, onDeck) == 1;
        return single && world.TryGet(reservations.Blocker(cell, onDeck), out var entity) && entity is Unit { MoveTarget: null } other && other.Owner == owner;
    }

    private static ImmutableArray<CellPos> BuildWay(MapGrid map, Dictionary<int, int> parent, int end)
    {
        var cells = new List<CellPos>();
        for (var index = end; index != -1; index = parent[index])
        {
            cells.Add(NodeOf(map, index).Cell);
        }

        cells.Reverse();
        return [.. cells];
    }
}
