using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Simulation.Pathfinding;

/// <summary>
/// Cost rules of the cell graph (docs/implementation/M2_unit_control.md, 4.4): 8 neighbours, no corner cutting,
/// integer step costs that depend on the land of the cell being entered, and an octile heuristic that never overestimates.
/// </summary>
public static class AStar
{
    /// <summary>Cost of a straight step on land with 100% speed.</summary>
    public const int StraightCost = 100;

    /// <summary>Cost of a diagonal step on land with 100% speed (100·√2, rounded down).</summary>
    public const int DiagonalCost = 141;

    /// <summary>Most nodes one search may expand: protects against searching a whole map into a dead end.</summary>
    public const int DefaultMaxExpansions = 20_000;

    // Neighbour order is part of the rules: with equal costs the first one found stays the parent.
    // N, E, S, W first, then the diagonals NE, SE, SW, NW.
    internal static readonly (int Dx, int Dy)[] Directions =
        [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];

    private static readonly int[,] StepCosts = BuildStepCosts();
    private static readonly (int Straight, int Diagonal)[] MinCosts = BuildMinCosts();

    /// <summary>Cost of entering a cell of <paramref name="land"/>, or 0 when the locomotor cannot.</summary>
    public static int StepCost(Locomotor locomotor, LandType land, bool diagonal) =>
        StepCosts[Row(locomotor, land), diagonal ? 1 : 0];

    /// <summary>
    /// True when a unit with <paramref name="locomotor"/> may step from <paramref name="from"/> by (dx, dy): the target
    /// is enterable and, for a diagonal step, both cells beside the corner are too.
    /// </summary>
    public static bool CanStep(MapGrid map, CellPos from, int dx, int dy, Locomotor locomotor)
    {
        if (!map.CanEnter(new CellPos(from.X + dx, from.Y + dy), locomotor))
        {
            return false;
        }

        return dx == 0 || dy == 0
            || (map.CanEnter(new CellPos(from.X + dx, from.Y), locomotor) && map.CanEnter(new CellPos(from.X, from.Y + dy), locomotor));
    }

    /// <summary>Octile distance on the cheapest steps of the locomotor: never more than the real cost of any path.</summary>
    public static int Heuristic(Locomotor locomotor, CellPos from, CellPos to)
    {
        var dx = IntMath.Abs(from.X - to.X);
        var dy = IntMath.Abs(from.Y - to.Y);
        var (straight, diagonal) = MinCosts[(int)locomotor - 1];
        return straight * (dx + dy) + (diagonal - 2 * straight) * IntMath.Min(dx, dy);
    }

    /// <summary>Runs a whole search at once. For a search spread over several ticks, use <see cref="Pathfinder.StartSearch"/>.</summary>
    public static CellPath FindPath(MapGrid map, CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions = DefaultMaxExpansions) =>
        new Pathfinder(map).FindPath(start, goal, locomotor, maxExpansions);

    private static int Row(Locomotor locomotor, LandType land) => ((int)locomotor - 1) * 8 + (int)land - 1;

    private static int[,] BuildStepCosts()
    {
        var costs = new int[3 * 8, 2];
        foreach (var locomotor in new[] { Locomotor.Foot, Locomotor.Track, Locomotor.Wheel })
        {
            for (var land = LandType.Clear; land <= LandType.Ice; land++)
            {
                var percent = LocomotorRules.SpeedPercent(locomotor, land);
                var row = Row(locomotor, land);
                costs[row, 0] = percent == 0 ? 0 : (int)IntMath.DivRound(StraightCost * 100L, percent);
                costs[row, 1] = percent == 0 ? 0 : (int)IntMath.DivRound(DiagonalCost * 100L, percent);
            }
        }

        return costs;
    }

    private static (int, int)[] BuildMinCosts()
    {
        var result = new (int, int)[3];
        foreach (var locomotor in new[] { Locomotor.Foot, Locomotor.Track, Locomotor.Wheel })
        {
            var straight = int.MaxValue;
            var diagonal = int.MaxValue;
            for (var land = LandType.Clear; land <= LandType.Ice; land++)
            {
                var cost = StepCosts[Row(locomotor, land), 0];
                if (cost > 0)
                {
                    straight = IntMath.Min(straight, cost);
                    diagonal = IntMath.Min(diagonal, StepCosts[Row(locomotor, land), 1]);
                }
            }

            result[(int)locomotor - 1] = (straight, diagonal);
        }

        return result;
    }
}

/// <summary>
/// Path searches on one map. Keeps a pool of per-search buffers so that searches do not allocate map-sized arrays
/// every time; each search owns its buffers until it finishes, so several searches can be in progress at once (M2 step 6).
/// Not thread-safe: one pathfinder per simulation.
/// </summary>
public sealed class Pathfinder
{
    private readonly Stack<SearchBuffers> _pool = new();

    public Pathfinder(MapGrid map) => Map = map;

    public MapGrid Map { get; }

    public CellPath FindPath(CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions = AStar.DefaultMaxExpansions)
    {
        var search = StartSearch(start, goal, locomotor, maxExpansions);
        while (!search.Advance(int.MaxValue))
        {
        }

        return search.Result!;
    }

    /// <summary>A search that runs only when <see cref="PathSearch.Advance"/> is called, as many nodes as allowed each time.</summary>
    public PathSearch StartSearch(CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions = AStar.DefaultMaxExpansions)
    {
        var buffers = _pool.Count > 0 ? _pool.Pop() : new SearchBuffers(Map.Width * Map.Height);
        return new PathSearch(this, buffers, start, goal, locomotor, maxExpansions);
    }

    internal void Return(SearchBuffers buffers) => _pool.Push(buffers);
}

/// <summary>Map-sized arrays of one search. A generation stamp marks valid entries, so a new search needs no clearing.</summary>
internal sealed class SearchBuffers
{
    public SearchBuffers(int cells)
    {
        Cost = new int[cells];
        Parent = new int[cells];
        Seen = new int[cells];
        Closed = new int[cells];
    }

    public int[] Cost { get; }

    public int[] Parent { get; }

    public int[] Seen { get; }

    public int[] Closed { get; }

    public MinHeap Open { get; } = new();

    public int Generation { get; private set; }

    public void BeginSearch()
    {
        Open.Clear();
        if (Generation == int.MaxValue)
        {
            Array.Clear(Seen);
            Array.Clear(Closed);
            Generation = 0;
        }

        Generation++;
    }
}

/// <summary>
/// One A* search (docs/implementation/M2_unit_control.md, 4.4) that can be advanced a few nodes at a time.
/// Unreachable goal: the path ends on the expanded cell with the lowest heuristic (then lowest cost, then lowest index),
/// so a click into a lake leads to its shore. The start may be any cell inside the map: the unit already stands there.
/// </summary>
public sealed class PathSearch
{
    private readonly Pathfinder _owner;
    private readonly MapGrid _map;
    private readonly CellPos _goal;
    private readonly Locomotor _locomotor;
    private readonly int _maxExpansions;
    private readonly int _startIndex;
    private readonly int _goalIndex;
    private SearchBuffers? _buffers;
    private int _bestIndex;
    private int _bestH;

    internal PathSearch(Pathfinder owner, SearchBuffers buffers, CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions)
    {
        var map = owner.Map;
        if (!map.Contains(start))
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "The search must start inside the map.");
        }

        if (!map.Contains(goal))
        {
            throw new ArgumentOutOfRangeException(nameof(goal), goal, "The goal must be inside the map.");
        }

        if (maxExpansions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExpansions), maxExpansions, "The node limit must be positive.");
        }

        _owner = owner;
        _map = map;
        _goal = goal;
        _locomotor = locomotor;
        _maxExpansions = maxExpansions;
        _startIndex = start.Y * map.Width + start.X;
        _goalIndex = goal.Y * map.Width + goal.X;

        _buffers = buffers;
        buffers.BeginSearch();
        var h = AStar.Heuristic(locomotor, start, goal);
        buffers.Cost[_startIndex] = 0;
        buffers.Parent[_startIndex] = -1;
        buffers.Seen[_startIndex] = buffers.Generation;
        buffers.Open.Push(new PathNode(h, h, _startIndex));
        _bestIndex = _startIndex;
        _bestH = h;
    }

    /// <summary>Nodes expanded so far.</summary>
    public int Expanded { get; private set; }

    /// <summary>The finished path, or null while the search is still running.</summary>
    public CellPath? Result { get; private set; }

    public bool IsDone => Result is not null;

    /// <summary>Expands at most <paramref name="maxNodes"/> nodes. Returns true when the search has finished.</summary>
    public bool Advance(int maxNodes)
    {
        if (_buffers is not { } b)
        {
            return true;
        }

        var width = _map.Width;
        var expandedNow = 0;
        while (expandedNow < maxNodes)
        {
            if (b.Open.Count == 0)
            {
                return Finish(PathStatus.Unreachable, _bestIndex);
            }

            var node = b.Open.Pop();
            if (b.Closed[node.Index] == b.Generation)
            {
                // A stale entry: the cell was reached again more cheaply and already expanded. Costs no budget.
                continue;
            }

            b.Closed[node.Index] = b.Generation;
            Expanded++;
            expandedNow++;
            var cost = b.Cost[node.Index];
            if (node.H < _bestH || (node.H == _bestH && (cost < b.Cost[_bestIndex] || (cost == b.Cost[_bestIndex] && node.Index < _bestIndex))))
            {
                _bestIndex = node.Index;
                _bestH = node.H;
            }

            if (node.Index == _goalIndex)
            {
                return Finish(PathStatus.Found, node.Index);
            }

            if (Expanded >= _maxExpansions)
            {
                return Finish(PathStatus.LimitReached, _bestIndex);
            }

            var cell = new CellPos(node.Index % width, node.Index / width);
            foreach (var (dx, dy) in AStar.Directions)
            {
                if (!AStar.CanStep(_map, cell, dx, dy, _locomotor))
                {
                    continue;
                }

                var next = new CellPos(cell.X + dx, cell.Y + dy);
                var nextIndex = next.Y * width + next.X;
                if (b.Closed[nextIndex] == b.Generation)
                {
                    continue;
                }

                var nextCost = cost + AStar.StepCost(_locomotor, _map.LandAt(next), dx != 0 && dy != 0);
                if (b.Seen[nextIndex] == b.Generation && nextCost >= b.Cost[nextIndex])
                {
                    continue;
                }

                b.Seen[nextIndex] = b.Generation;
                b.Cost[nextIndex] = nextCost;
                b.Parent[nextIndex] = node.Index;
                var h = AStar.Heuristic(_locomotor, next, _goal);
                b.Open.Push(new PathNode(nextCost + h, h, nextIndex));
            }
        }

        return false;
    }

    private bool Finish(PathStatus status, int endIndex)
    {
        var b = _buffers!;
        var width = _map.Width;
        var cells = new List<CellPos>();
        for (var index = endIndex; index != -1; index = b.Parent[index])
        {
            cells.Add(new CellPos(index % width, index / width));
        }

        cells.Reverse();
        Result = new CellPath(status, [.. cells], b.Cost[endIndex], Expanded);
        _buffers = null;
        _owner.Return(b);
        return true;
    }
}
