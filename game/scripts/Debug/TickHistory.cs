namespace Rts.Client.Debug;

/// <summary>
/// The last <see cref="Capacity"/> ticks of the path queue (docs/implementation/M2_unit_control.md, 4.14): A* nodes expanded
/// in the tick and requests waiting after it. A ring: the oldest tick drops out. Pure, tested in Rts.Simulation.Tests.
/// </summary>
public sealed class TickHistory
{
    public const int Capacity = 150;

    private readonly int[] _nodes = new int[Capacity];
    private readonly int[] _queue = new int[Capacity];
    private int _next;

    /// <summary>How many ticks are stored, up to <see cref="Capacity"/>.</summary>
    public int Count { get; private set; }

    public void Add(int nodes, int queue)
    {
        _nodes[_next] = nodes;
        _queue[_next] = queue;
        _next = (_next + 1) % Capacity;
        if (Count < Capacity)
        {
            Count++;
        }
    }

    /// <summary>Nodes of the <paramref name="index"/>-th stored tick, 0 the oldest.</summary>
    public int NodesAt(int index) => _nodes[Slot(index)];

    /// <summary>Queue length after the <paramref name="index"/>-th stored tick, 0 the oldest.</summary>
    public int QueueAt(int index) => _queue[Slot(index)];

    public int MaxNodes() => Max(_nodes);

    public int MaxQueue() => Max(_queue);

    /// <summary>Average nodes per tick, rounded down; 0 when empty.</summary>
    public int AverageNodes()
    {
        if (Count == 0)
        {
            return 0;
        }

        long sum = 0;
        for (var i = 0; i < Count; i++)
        {
            sum += NodesAt(i);
        }

        return (int)(sum / Count);
    }

    public void Clear()
    {
        _next = 0;
        Count = 0;
    }

    /// <summary><c>max 4120 · avg 380 · queue max 7</c>.</summary>
    public string Describe() => $"max {MaxNodes()} · avg {AverageNodes()} · queue max {MaxQueue()}";

    private int Max(int[] values)
    {
        var max = 0;
        for (var i = 0; i < Count; i++)
        {
            max = System.Math.Max(max, values[Slot(i)]);
        }

        return max;
    }

    private int Slot(int index)
    {
        if (index < 0 || index >= Count)
        {
            throw new System.ArgumentOutOfRangeException(nameof(index), index, $"The history holds {Count} ticks.");
        }

        return (_next - Count + index + Capacity) % Capacity;
    }
}
