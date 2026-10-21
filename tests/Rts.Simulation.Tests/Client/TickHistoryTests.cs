using Rts.Client.Debug;

namespace Rts.Simulation.Tests.Client;

/// <summary>The path queue history behind the F8 graph (docs/implementation/M2_unit_control.md, 4.14).</summary>
public sealed class TickHistoryTests
{
    [Fact]
    public void AnEmptyHistoryHasNoTicks()
    {
        var history = new TickHistory();

        Assert.Equal(0, history.Count);
        Assert.Equal("max 0 · avg 0 · queue max 0", history.Describe());
        Assert.Throws<ArgumentOutOfRangeException>(() => history.NodesAt(0));
    }

    [Fact]
    public void TicksAreKeptOldestFirstWithMaxAndAverage()
    {
        var history = new TickHistory();
        history.Add(100, 2);
        history.Add(4000, 7);
        history.Add(0, 0);

        Assert.Equal(3, history.Count);
        Assert.Equal([100, 4000, 0], Enumerable.Range(0, 3).Select(history.NodesAt));
        Assert.Equal([2, 7, 0], Enumerable.Range(0, 3).Select(history.QueueAt));
        Assert.Equal("max 4000 · avg 1366 · queue max 7", history.Describe());
    }

    [Fact]
    public void TheOldestTickDropsOutWhenFull()
    {
        var history = new TickHistory();
        for (var i = 0; i < TickHistory.Capacity + 10; i++)
        {
            history.Add(i, i % 3);
        }

        Assert.Equal(TickHistory.Capacity, history.Count);
        Assert.Equal(10, history.NodesAt(0));
        Assert.Equal(TickHistory.Capacity + 9, history.NodesAt(TickHistory.Capacity - 1));
        Assert.Equal(TickHistory.Capacity + 9, history.MaxNodes());
    }

    [Fact]
    public void ClearEmptiesIt()
    {
        var history = new TickHistory();
        history.Add(5, 1);

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.Equal(0, history.MaxNodes());
    }
}
