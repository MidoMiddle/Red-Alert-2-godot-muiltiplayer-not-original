using System;

namespace Rts.Simulation.Pathfinding;

/// <summary>An open node of A*: total estimate <see cref="F"/>, heuristic <see cref="H"/>, cell index.</summary>
public readonly record struct PathNode(int F, int H, int Index) : IComparable<PathNode>
{
    /// <summary>Total order: lower f first, then lower h (closer to the goal), then lower cell index.</summary>
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
/// differ between runtimes or platforms.
/// </summary>
public sealed class MinHeap
{
    private PathNode[] _items = new PathNode[64];

    public int Count { get; private set; }

    public void Clear() => Count = 0;

    public void Push(PathNode node)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }

        var i = Count++;
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (_items[parent].CompareTo(node) <= 0)
            {
                break;
            }

            _items[i] = _items[parent];
            i = parent;
        }

        _items[i] = node;
    }

    public PathNode Pop()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        var top = _items[0];
        var last = _items[--Count];
        var i = 0;
        while (true)
        {
            var child = 2 * i + 1;
            if (child >= Count)
            {
                break;
            }

            if (child + 1 < Count && _items[child + 1].CompareTo(_items[child]) < 0)
            {
                child++;
            }

            if (last.CompareTo(_items[child]) <= 0)
            {
                break;
            }

            _items[i] = _items[child];
            i = child;
        }

        if (Count > 0)
        {
            _items[i] = last;
        }

        return top;
    }
}
