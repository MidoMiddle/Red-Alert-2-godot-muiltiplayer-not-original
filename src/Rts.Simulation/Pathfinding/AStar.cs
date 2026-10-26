using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    public static int Heuristic(LandSpeeds speeds, Locomotor locomotor, CellPos from, CellPos to)
    {
        var dx = IntMath.Abs(from.X - to.X);
        var dy = IntMath.Abs(from.Y - to.Y);
        var (straight, diagonal) = speeds.MinStepCosts(locomotor);
        return straight * (dx + dy) + (diagonal - 2 * straight) * IntMath.Min(dx, dy);
    }

    /// <summary>Runs a whole search at once. For a search spread over several ticks, use <see cref="Pathfinder.StartSearch"/>.</summary>
    public static CellPath FindPath(
        MapGrid map, CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions = DefaultMaxExpansions, bool startOnDeck = false) =>
        new Pathfinder(map).FindPath(start, goal, locomotor, maxExpansions, startOnDeck);
}

/// <summary>
/// Path searches on one map. Keeps a pool of per-search buffers so that searches do not allocate map-sized arrays
/// every time; each search owns its buffers from its first step until it finishes, so several searches can be in progress
/// at once (M2 step 6). A search that is only waiting in the queue holds none: 300 units ordered at once would otherwise
/// take 300 map-sized sets (M2 step 15).
/// Not thread-safe: one pathfinder per simulation.
/// </summary>
public sealed class Pathfinder
{
    private readonly Stack<SearchBuffers> _pool = new();
    private readonly StepCostLayer?[] _costs = new StepCostLayer?[LandSpeeds.LocomotorCount];

    public Pathfinder(MapGrid map) => Map = map;

    public MapGrid Map { get; }

    public CellPath FindPath(CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions = AStar.DefaultMaxExpansions, bool startOnDeck = false)
    {
        var search = StartSearch(start, goal, locomotor, maxExpansions, default, startOnDeck);
        while (!search.Advance(int.MaxValue))
        {
        }

        return search.Result!;
    }

    /// <summary>
    /// A search that runs only when <see cref="PathSearch.Advance"/> is called, as many nodes as allowed each time.
    /// <paramref name="avoid"/> are cells treated as impassable for this search only, e.g. cells taken by other units.
    /// <paramref name="startOnDeck"/>: the unit stands on the deck of a high bridge over the start (<see cref="Decks"/>). The goal is
    /// reached on either level, whichever the search gets to first, or with <paramref name="groundGoal"/> on the ground only.
    /// </summary>
    public PathSearch StartSearch(
        CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions = AStar.DefaultMaxExpansions, ImmutableArray<CellPos> avoid = default,
        bool startOnDeck = false, bool groundGoal = false)
    {
        return new PathSearch(this, start, goal, locomotor, maxExpansions, avoid, startOnDeck, groundGoal);
    }

    /// <summary>The step costs of every cell for <paramref name="locomotor"/>, built once and again after the map changes.</summary>
    internal StepCostLayer CostsFor(Locomotor locomotor)
    {
        var slot = (int)locomotor - 1;
        if (_costs[slot] is not { } layer || layer.MapVersion != Map.Version)
        {
            layer = new StepCostLayer(Map, locomotor);
            _costs[slot] = layer;
        }

        return layer;
    }

    /// <summary>
    /// The map changed in the cells from <paramref name="from"/> to <paramref name="to"/>: layers that were up to date before the
    /// change (<paramref name="versionBefore"/>) are patched in that area only; older ones are rebuilt when next used.
    /// </summary>
    internal void MapChanged(int versionBefore, CellPos from, CellPos to)
    {
        for (var slot = 0; slot < _costs.Length; slot++)
        {
            if (_costs[slot] is { } layer && layer.MapVersion == versionBefore)
            {
                layer.Patch(Map, (Locomotor)(slot + 1), from, to);
            }
        }
    }

    internal SearchBuffers Rent() => _pool.Count > 0 ? _pool.Pop() : new SearchBuffers(Map.NodeCount);

    internal void Return(SearchBuffers buffers) => _pool.Push(buffers);
}

/// <summary>
/// The cost of stepping into each cell for one locomotor, straight and diagonal, 0 where it cannot enter (outside the map
/// does not occur: callers check bounds). From <see cref="MapGrid.SpeedPercent"/>, so slopes cost more and cliffs are closed;
/// on flat land the same numbers as <see cref="LandSpeeds.StepCost"/>,
/// read from flat arrays in the inner loop of the search instead of through the land tables (M2 step 15).
/// </summary>
internal sealed class StepCostLayer
{
    public StepCostLayer(MapGrid map, Locomotor locomotor)
    {
        Straight = new int[map.Width * map.Height];
        Diagonal = new int[map.Width * map.Height];
        DeckStraight = new int[map.DeckCount];
        DeckDiagonal = new int[map.DeckCount];
        Patch(map, locomotor, new CellPos(0, 0), new CellPos(map.Width - 1, map.Height - 1));
    }

    public int MapVersion { get; private set; }

    /// <summary>Works out the cells from <paramref name="from"/> to <paramref name="to"/> again and takes the map's version.</summary>
    public void Patch(MapGrid map, Locomotor locomotor, CellPos from, CellPos to)
    {
        for (var y = from.Y; y <= to.Y; y++)
        {
            for (var x = from.X; x <= to.X; x++)
            {
                // From the cell's speed, slopes included (M7 step 3): on flat land the same numbers as LandSpeeds.StepCost.
                var percent = map.SpeedPercent(new CellPos(x, y), locomotor);
                var index = y * map.Width + x;
                Straight[index] = percent > 0 ? (int)IntMath.DivRound(AStar.StraightCost * 100L, percent) : 0;
                Diagonal[index] = percent > 0 ? (int)IntMath.DivRound(AStar.DiagonalCost * 100L, percent) : 0;
            }
        }

        // The decks of the high bridges are few: worked out again whole, whatever changed (a segment fell or was mended).
        for (var slot = 0; slot < DeckStraight.Length; slot++)
        {
            var percent = map.DeckSpeedPercent(slot, locomotor);
            DeckStraight[slot] = percent > 0 ? (int)IntMath.DivRound(AStar.StraightCost * 100L, percent) : 0;
            DeckDiagonal[slot] = percent > 0 ? (int)IntMath.DivRound(AStar.DiagonalCost * 100L, percent) : 0;
        }

        MapVersion = map.Version;
    }

    public int[] Straight { get; }

    public int[] Diagonal { get; }

    /// <summary>The step costs onto each slot of deck (<see cref="MapGrid.DeckSlot"/>), 0 where the segment fell or for ships.</summary>
    public int[] DeckStraight { get; }

    public int[] DeckDiagonal { get; }
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
        Open = new MinHeap(cells);
    }

    public int[] Cost { get; }

    public int[] Parent { get; }

    public int[] Seen { get; }

    public int[] Closed { get; }

    public MinHeap Open { get; }

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
/// The nodes are the cells, then the slots of deck of the high bridges (<see cref="Decks"/>): a cell node steps to its 8
/// neighbours on the ground as it always did and onto the deck its shore leads to; a deck node steps along its deck and off an
/// end onto the shore. A goal under a deck is reached on either level, whichever comes first.
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
    private readonly int _goalDeckIndex;
    private readonly int[] _avoid;
    private SearchBuffers? _buffers;
    private bool _started;
    private bool _stopped;
    private int _bestIndex;
    private int _bestH;

    internal PathSearch(
        Pathfinder owner, CellPos start, CellPos goal, Locomotor locomotor, int maxExpansions, ImmutableArray<CellPos> avoid, bool startOnDeck = false,
        bool groundGoal = false)
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
        var cells = map.Width * map.Height;
        _startIndex = startOnDeck && map.DeckSlot(start) is var startSlot and >= 0 ? cells + startSlot : start.Y * map.Width + start.X;
        _goalIndex = goal.Y * map.Width + goal.X;
        _goalDeckIndex = !groundGoal && map.DeckSlot(goal) is var goalSlot and >= 0 ? cells + goalSlot : -1;
        _avoid = avoid.IsDefaultOrEmpty ? [] : avoid.Where(map.Contains).Select(cell => cell.Y * map.Width + cell.X).Order().ToArray();
        _bestIndex = _startIndex;
        _bestH = AStar.Heuristic(map.Speeds, locomotor, start, goal);
    }

    /// <summary>Nodes expanded so far.</summary>
    public int Expanded { get; private set; }

    /// <summary>The finished path, or null while the search is still running.</summary>
    public CellPath? Result { get; private set; }

    public bool IsDone => Result is not null;

    /// <summary>True once the search took its first step, so it holds state worked out on the map as it was then.</summary>
    internal bool IsStarted => _started;

    /// <summary>Expands at most <paramref name="maxNodes"/> nodes. Returns true when the search has finished.</summary>
    public bool Advance(int maxNodes)
    {
        if (_stopped)
        {
            return true;
        }

        if (!_started)
        {
            Begin();
        }

        var b = _buffers!;

        var width = _map.Width;
        var height = _map.Height;
        var costs = _owner.CostsFor(_locomotor);
        var straight = costs.Straight;
        var diagonal = costs.Diagonal;
        var directions = AStar.Directions;
        var open = b.Open;
        var closed = b.Closed;
        var seen = b.Seen;
        var costOf = b.Cost;
        var parent = b.Parent;
        var generation = b.Generation;
        var (hStraight, hDiagonal) = _map.Speeds.MinStepCosts(_locomotor);
        var goalX = _goal.X;
        var goalY = _goal.Y;
        var expandedNow = 0;
        while (expandedNow < maxNodes)
        {
            if (open.Count == 0)
            {
                return Finish(PathStatus.Unreachable, _bestIndex);
            }

            var node = open.Pop();
            if (closed[node.Index] == generation)
            {
                // A stale entry: the cell was reached again more cheaply and already expanded. Costs no budget.
                continue;
            }

            closed[node.Index] = generation;
            Expanded++;
            expandedNow++;
            var cost = costOf[node.Index];
            if (node.H < _bestH || (node.H == _bestH && (cost < costOf[_bestIndex] || (cost == costOf[_bestIndex] && node.Index < _bestIndex))))
            {
                _bestIndex = node.Index;
                _bestH = node.H;
            }

            if (node.Index == _goalIndex || node.Index == _goalDeckIndex)
            {
                return Finish(PathStatus.Found, node.Index);
            }

            if (Expanded >= _maxExpansions)
            {
                return Finish(PathStatus.LimitReached, _bestIndex);
            }

            if (node.Index >= width * height)
            {
                ExpandDeck(node.Index, cost, costs, b);
                continue;
            }

            var x = node.Index % width;
            var y = node.Index / width;
            for (var d = 0; d < directions.Length; d++)
            {
                // The same test as AStar.CanStep: the cell is enterable and, for a diagonal, both cells beside the corner too.
                var (dx, dy) = directions[d];
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    continue;
                }

                var nextIndex = ny * width + nx;
                var isDiagonal = dx != 0 && dy != 0;
                if (straight[nextIndex] == 0 || (isDiagonal && (straight[y * width + nx] == 0 || straight[ny * width + x] == 0)))
                {
                    continue;
                }

                if (closed[nextIndex] == generation || (_avoid.Length > 0 && Array.BinarySearch(_avoid, nextIndex) >= 0))
                {
                    continue;
                }

                var nextCost = cost + (isDiagonal ? diagonal[nextIndex] : straight[nextIndex]);
                if (seen[nextIndex] == generation && nextCost >= costOf[nextIndex])
                {
                    continue;
                }

                seen[nextIndex] = generation;
                costOf[nextIndex] = nextCost;
                parent[nextIndex] = node.Index;

                // AStar.Heuristic, with the locomotor's step costs looked up once per call of Advance.
                var hx = nx > goalX ? nx - goalX : goalX - nx;
                var hy = ny > goalY ? ny - goalY : goalY - ny;
                var h = hStraight * (hx + hy) + (hDiagonal - 2 * hStraight) * (hx < hy ? hx : hy);
                open.PushOrDecrease(new PathNode(nextCost + h, h, nextIndex));
            }

            // Onto the decks this shore leads to (straight along their bridges).
            if (_map.DeckCount > 0)
            {
                var entries = _map.DeckEntries(node.Index);
                for (var e = 0; e < entries.Count; e++)
                {
                    var slot = entries[e];
                    if (costs.DeckStraight[slot] > 0)
                    {
                        Relax(width * height + slot, node.Index, cost + costs.DeckStraight[slot], b);
                    }
                }
            }
        }

        return false;
    }

    // A node of deck: along its deck to the neighbouring cells of the same deck (a diagonal with both cells beside the corner on it
    // too), and off an end cell onto its shore — the step rule of Decks.TryStep.
    private void ExpandDeck(int nodeIndex, int cost, StepCostLayer costs, SearchBuffers b)
    {
        var cells = _map.Width * _map.Height;
        var slot = nodeIndex - cells;
        var cell = _map.DeckCell(slot);
        var directions = AStar.Directions;
        for (var d = 0; d < directions.Length; d++)
        {
            var (dx, dy) = directions[d];
            if (!Decks.TryStep(_map, cell, true, dx, dy, _locomotor, out var toDeck))
            {
                continue;
            }

            var next = new CellPos(cell.X + dx, cell.Y + dy);
            var isDiagonal = dx != 0 && dy != 0;
            if (toDeck)
            {
                var nextSlot = _map.DeckSlot(next);
                Relax(cells + nextSlot, nodeIndex, cost + (isDiagonal ? costs.DeckDiagonal[nextSlot] : costs.DeckStraight[nextSlot]), b);
            }
            else
            {
                var index = next.Y * _map.Width + next.X;
                Relax(index, nodeIndex, cost + costs.Straight[index], b);
            }
        }
    }

    // Opens a node at a cost if it is better than what it has; the heuristic from the node's cell.
    private void Relax(int nextIndex, int parentIndex, int nextCost, SearchBuffers b)
    {
        var cell = CellOf(nextIndex);
        if (b.Closed[nextIndex] == b.Generation
            || (_avoid.Length > 0 && Array.BinarySearch(_avoid, cell.Y * _map.Width + cell.X) >= 0)
            || (b.Seen[nextIndex] == b.Generation && nextCost >= b.Cost[nextIndex]))
        {
            return;
        }

        b.Seen[nextIndex] = b.Generation;
        b.Cost[nextIndex] = nextCost;
        b.Parent[nextIndex] = parentIndex;
        var h = AStar.Heuristic(_map.Speeds, _locomotor, cell, _goal);
        b.Open.PushOrDecrease(new PathNode(nextCost + h, h, nextIndex));
    }

    // The cell of a node: itself, or the cell under a slot of deck.
    private CellPos CellOf(int nodeIndex)
    {
        var cells = _map.Width * _map.Height;
        return nodeIndex < cells ? new CellPos(nodeIndex % _map.Width, nodeIndex / _map.Width) : _map.DeckCell(nodeIndex - cells);
    }

    /// <summary>Stops an unfinished search and gives its buffers back; the search never finishes after this.</summary>
    internal void Cancel()
    {
        _stopped = true;
        if (_buffers is { } b)
        {
            _buffers = null;
            _owner.Return(b);
        }
    }

    /// <summary>The first step: takes buffers and opens the start cell.</summary>
    private void Begin()
    {
        _started = true;
        var b = _owner.Rent();
        _buffers = b;
        b.BeginSearch();
        b.Cost[_startIndex] = 0;
        b.Parent[_startIndex] = -1;
        b.Seen[_startIndex] = b.Generation;
        b.Open.Push(new PathNode(_bestH, _bestH, _startIndex));
    }

    private bool Finish(PathStatus status, int endIndex)
    {
        var b = _buffers!;
        var width = _map.Width;
        var cells = new List<CellPos>();
        for (var index = endIndex; index != -1; index = b.Parent[index])
        {
            cells.Add(CellOf(index));
        }

        cells.Reverse();
        Result = new CellPath(status, [.. cells], b.Cost[endIndex], Expanded);
        _stopped = true;
        _buffers = null;
        _owner.Return(b);
        return true;
    }
}
