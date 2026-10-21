using System;
using System.Collections.Generic;

namespace Rts.Simulation.Commands;

/// <summary>
/// Commands waiting for their execution tick. Ticks are taken strictly one after another, and the
/// commands of a tick come out ordered by (Player, Sequence), so every peer applies them in the same
/// order no matter in which order they arrived.
/// </summary>
public sealed class CommandQueue
{
    private readonly SortedDictionary<int, List<Command>> _byTick = new();

    public CommandQueue(int firstTick = 0)
    {
        NextTick = firstTick;
    }

    /// <summary>Tick that the next <see cref="TakeForTick"/> call must ask for.</summary>
    public int NextTick { get; private set; }

    /// <summary>Number of commands not taken yet.</summary>
    public int PendingCount { get; private set; }

    public void Enqueue(Command command)
    {
        if (command.ExecuteTick < NextTick)
        {
            throw new InvalidOperationException(
                $"Command for tick {command.ExecuteTick} arrived after that tick was taken (next tick is {NextTick}).");
        }

        if (!_byTick.TryGetValue(command.ExecuteTick, out var commands))
        {
            commands = new List<Command>();
            _byTick.Add(command.ExecuteTick, commands);
        }

        foreach (var existing in commands)
        {
            if (existing.Player == command.Player && existing.Sequence == command.Sequence)
            {
                throw new InvalidOperationException(
                    $"Duplicate command {command.Player} #{command.Sequence} on tick {command.ExecuteTick}.");
            }
        }

        commands.Add(command);
        PendingCount++;
    }

    /// <summary>Removes and returns the commands of <paramref name="tick"/>, ordered by (Player, Sequence).</summary>
    public IReadOnlyList<Command> TakeForTick(int tick)
    {
        if (tick != NextTick)
        {
            throw new InvalidOperationException($"Ticks must be taken in order: expected {NextTick}, got {tick}.");
        }

        NextTick++;
        if (!_byTick.TryGetValue(tick, out var commands))
        {
            return Array.Empty<Command>();
        }

        _byTick.Remove(tick);
        PendingCount -= commands.Count;

        // Keys are unique (duplicates are rejected on enqueue), so the unstable sort is still deterministic.
        commands.Sort(CompareForExecution);
        return commands;
    }

    private static int CompareForExecution(Command a, Command b)
    {
        var byPlayer = a.Player.Value.CompareTo(b.Player.Value);
        return byPlayer != 0 ? byPlayer : a.Sequence.CompareTo(b.Sequence);
    }
}
