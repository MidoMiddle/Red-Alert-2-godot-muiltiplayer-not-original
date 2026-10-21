using System;

namespace Rts.Simulation.Pathfinding;

/// <summary>An open node of A*: total estimate <see cref="F"/>, heuristic <see cref="H"/>, cell index.</summary>
public readonly record struct PathNode(int F, int H, int Index) : IComparable<PathNode>
{
    /// <summary>Total order: lower f first, then lower h (closer to the goal), then lower cell index.</summary>
    /// <summary>The same order as <see cref="CompareTo"/>, as a plain test for the heap's inner loops.</summary>
    internal static bool Before(in PathNode a, in PathNode b) =>
        a.F != b.F ? a.F < b.F : a.H != b.H ? a.H < b.H : a.Index < b.Index;

    public int CompareTo(PathNode other)
    {
        if (F != other.F)
        {
            return F < other.F ? -1 : 1;
        }

        if (H != other.H)
        {
            return H < other.H ? -1 : 1;
        }

        return Index.CompareTo(other.Index);
    }
}

/// <summary>
/// Binary min-heap of <see cref="PathNode"/> (docs/implementation/M2_unit_control.md, 4.4). Own implementation instead of
/// .NET PriorityQueue: with a total order on the keys, the pop order is defined by this code alone, so it can never
/// differ between runtimes or platforms. Built with a number of cell indices, it also knows where each cell sits, so
/// <see cref="PushOrDecrease"/> lowers the key of a cell already queued instead of adding it again (M2 step 15: A* used to
/// queue a cell once per improvement and skip the old copies, which made the heap several times larger).
/// </summary>
public sealed class MinHeap
{
    private readonly int[]? _position;
    private PathNode[] _items = new PathNode[64];

    public MinHeap()
    {
    }

    /// <param name="indexCount">Cell indices are 0 … <paramref name="indexCount"/> − 1.</param>
    public MinHeap(int indexCount) => _position = new int[indexCount];

    public int Count { get; private set; }

    public void Clear() => Count = 0;

    public void Push(PathNode node)
    {
        Grow();
        SiftUp(Count++, node);
    }

    /// <summary>
    /// Adds the node, or, when a node of the same cell is queued, puts this one in its place: the key must not be larger
    /// (A* only queues a cell again with a lower cost). Needs the constructor with an index count.
    /// </summary>
    public void PushOrDecrease(PathNode node)
    {
        var at = _position![node.Index];
        if (at < Count && _items[at].Index == node.Index)
        {
            if (PathNode.Before(_items[at], node))
            {
                throw new InvalidOperationException($"The key of cell {node.Index} may only go down.");
            }

            SiftUp(at, node);
            return;
        }

        Push(node);
    }

    public PathNode Pop()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        var items = _items;
        var top = items[0];
        var last = items[--Count];
        var count = Count;
        var i = 0;
        while (true)
        {
            var child = 2 * i + 1;
            if (child >= count)
            {
                break;
            }

            if (child + 1 < count && PathNode.Before(items[child + 1], items[child]))
            {
                child++;
            }

            if (!PathNode.Before(items[child], last))
            {
                break;
            }

            Place(i, items[child]);
            i = child;
        }

        if (count > 0)
        {
            Place(i, last);
        }

        return top;
    }

    private void Grow()
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }
    }

    private void SiftUp(int i, PathNode node)
    {
        var items = _items;
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (!PathNode.Before(node, items[parent]))
            {
                break;
            }

            Place(i, items[parent]);
            i = parent;
        }

        Place(i, node);
    }

    private void Place(int at, PathNode node)
    {
        _items[at] = node;
        if (_position is not null)
        {
            _position[node.Index] = at;
        }
    }
}
