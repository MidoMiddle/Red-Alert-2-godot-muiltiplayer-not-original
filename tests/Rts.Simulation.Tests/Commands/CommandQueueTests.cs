using Rts.Simulation.Commands;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Commands;

public sealed class CommandQueueTests
{
    [Fact]
    public void Commands_come_out_ordered_by_player_then_sequence()
    {
        var queue = new CommandQueue();
        queue.Enqueue(Spawn(player: 2, tick: 0, sequence: 1));
        queue.Enqueue(Spawn(player: 1, tick: 0, sequence: 5));
        queue.Enqueue(Spawn(player: 2, tick: 0, sequence: 0));
        queue.Enqueue(Spawn(player: 1, tick: 0, sequence: 2));

        var order = queue.TakeForTick(0).Select(c => (c.Player.Value, c.Sequence)).ToList();

        Assert.Equal([(1, 2u), (1, 5u), (2, 0u), (2, 1u)], order);
    }

    [Fact]
    public void Arrival_order_does_not_matter()
    {
        var commands = Enumerable.Range(0, 40)
            .Select(i => (Command)Spawn(player: 1 + i % 4, tick: 0, sequence: (uint)(i / 4)))
            .ToList();
        var forward = new CommandQueue();
        var backward = new CommandQueue();

        commands.ForEach(forward.Enqueue);
        Enumerable.Reverse(commands).ToList().ForEach(backward.Enqueue);

        Assert.Equal(forward.TakeForTick(0), backward.TakeForTick(0));
    }

    [Fact]
    public void Ticks_are_kept_apart()
    {
        var queue = new CommandQueue();
        queue.Enqueue(Spawn(player: 1, tick: 2, sequence: 0));
        queue.Enqueue(Spawn(player: 1, tick: 0, sequence: 1));

        Assert.Single(queue.TakeForTick(0));
        Assert.Empty(queue.TakeForTick(1));
        Assert.Equal(2, Assert.Single(queue.TakeForTick(2)).ExecuteTick);
        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(3, queue.NextTick);
    }

    [Fact]
    public void Command_for_a_taken_tick_is_rejected()
    {
        var queue = new CommandQueue();
        queue.TakeForTick(0);

        Assert.Throws<InvalidOperationException>(() => queue.Enqueue(Spawn(player: 1, tick: 0, sequence: 0)));
    }

    [Fact]
    public void Duplicate_player_and_sequence_in_a_tick_is_rejected()
    {
        var queue = new CommandQueue();
        queue.Enqueue(Spawn(player: 1, tick: 3, sequence: 7));

        Assert.Throws<InvalidOperationException>(() => queue.Enqueue(Move(player: 1, tick: 3, sequence: 7, units: [1])));
    }

    [Fact]
    public void Same_sequence_on_different_ticks_or_players_is_fine()
    {
        var queue = new CommandQueue();
        queue.Enqueue(Spawn(player: 1, tick: 0, sequence: 7));
        queue.Enqueue(Spawn(player: 2, tick: 0, sequence: 7));
        queue.Enqueue(Spawn(player: 1, tick: 1, sequence: 7));

        Assert.Equal(3, queue.PendingCount);
    }

    [Fact]
    public void Ticks_must_be_taken_in_order()
    {
        var queue = new CommandQueue(firstTick: 10);

        Assert.Throws<InvalidOperationException>(() => queue.TakeForTick(11));
        Assert.Empty(queue.TakeForTick(10));
        Assert.Throws<InvalidOperationException>(() => queue.TakeForTick(10));
    }
}
