using Rts.Simulation.Pathfinding;

namespace Rts.Simulation.Tests.Pathfinding;

public sealed class MinHeapTests
{
    [Fact]
    public void Pops_in_key_order()
    {
        var random = new TestRandom(1);
        var heap = new MinHeap();
        var nodes = Enumerable.Range(0, 10_000)
            .Select(i => new PathNode(random.NextInt(500), random.NextInt(50), i))
            .ToList();
        nodes.ForEach(heap.Push);

        var popped = new List<PathNode>();
        while (heap.Count > 0)
        {
            popped.Add(heap.Pop());
        }

        Assert.Equal(nodes.Order().ToList(), popped);
    }

    [Fact]
    public void Equal_f_goes_by_h_then_by_index()
    {
        var heap = new MinHeap();
        heap.Push(new PathNode(10, 5, 3));
        heap.Push(new PathNode(10, 2, 9));
        heap.Push(new PathNode(10, 5, 1));
        heap.Push(new PathNode(9, 9, 7));

        Assert.Equal(
            [new PathNode(9, 9, 7), new PathNode(10, 2, 9), new PathNode(10, 5, 1), new PathNode(10, 5, 3)],
            [heap.Pop(), heap.Pop(), heap.Pop(), heap.Pop()]);
    }

    [Fact]
    public void Mixed_pushes_and_pops_stay_ordered()
    {
        var random = new TestRandom(2);
        var heap = new MinHeap();
        var reference = new List<PathNode>();
        for (var i = 0; i < 5000; i++)
        {
            if (heap.Count > 0 && random.NextInt(3) == 0)
            {
                var expected = reference.Min();
                reference.Remove(expected);
                Assert.Equal(expected, heap.Pop());
            }
            else
            {
                var node = new PathNode(random.NextInt(100), random.NextInt(10), i);
                heap.Push(node);
                reference.Add(node);
            }

            Assert.Equal(reference.Count, heap.Count);
        }
    }

    [Fact]
    public void Empty_heap_cannot_pop()
    {
        var heap = new MinHeap();
        heap.Push(new PathNode(1, 1, 1));
        heap.Clear();

        Assert.Equal(0, heap.Count);
        Assert.Throws<InvalidOperationException>(() => heap.Pop());
    }

    [Fact]
    public void Decrease_key_matches_pushing_copies_and_skipping_the_stale_ones()
    {
        // A* with copies (the old heap) and with decrease-key must expand cells in the same order (M2 step 15).
        var random = new TestRandom(3);
        const int cells = 300;
        var indexed = new MinHeap(cells);
        var copies = new MinHeap();
        var best = new int[cells];
        Array.Fill(best, int.MaxValue);
        var closed = new bool[cells];
        var fromIndexed = new List<PathNode>();
        var fromCopies = new List<PathNode>();
        for (var step = 0; step < 4000; step++)
        {
            if (random.NextInt(3) == 0 && indexed.Count > 0)
            {
                var a = indexed.Pop();
                PathNode b;
                do
                {
                    b = copies.Pop();
                }
                while (closed[b.Index]);

                closed[a.Index] = true;
                fromIndexed.Add(a);
                fromCopies.Add(b);
                continue;
            }

            var index = random.NextInt(cells);
            var f = random.NextInt(1000);
            if (closed[index] || f >= best[index])
            {
                continue;
            }

            best[index] = f;
            var node = new PathNode(f, f % 7, index);
            indexed.PushOrDecrease(node);
            copies.Push(node);
        }

        Assert.NotEmpty(fromIndexed);
        Assert.Equal(fromCopies, fromIndexed);
    }

    [Fact]
    public void A_key_may_only_go_down()
    {
        var heap = new MinHeap(10);
        heap.PushOrDecrease(new PathNode(5, 1, 4));
        heap.PushOrDecrease(new PathNode(3, 1, 4));

        Assert.Equal(1, heap.Count);
        Assert.Throws<InvalidOperationException>(() => heap.PushOrDecrease(new PathNode(9, 1, 4)));
        Assert.Equal(new PathNode(3, 1, 4), heap.Pop());
    }
}
