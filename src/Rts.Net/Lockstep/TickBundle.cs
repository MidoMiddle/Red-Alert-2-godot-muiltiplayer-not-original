using System;
using System.Collections.Immutable;
using Rts.Net.Protocol;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Net.Lockstep;

/// <summary>
/// The commands of one player for one tick (docs/implementation/M9_network.md, 4.5):
/// <c>i32 player | i32 tick | u16 commandCount | commands</c>, the commands in the format of the simulation. Every player sends exactly
/// one bundle for every tick; an empty one says «I have reached this tick and have nothing to say». Every command of a bundle is of
/// its player and its tick — the order inside does not matter, the simulation sorts a tick by (player, sequence).
/// </summary>
public sealed record TickBundle : NetMessage
{
    public TickBundle(PlayerId player, int tick, ImmutableArray<Command> commands)
    {
        if (Problem(player, tick, commands) is { } problem)
        {
            throw new ArgumentException(problem, nameof(commands));
        }

        Player = player;
        Tick = tick;
        Commands = commands;
    }

    public PlayerId Player { get; }

    public int Tick { get; }

    public ImmutableArray<Command> Commands { get; }

    public bool IsEmpty => Commands.IsEmpty;

    public override MessageType Type => MessageType.TickBundle;

    /// <summary>Nothing to say on <paramref name="tick"/>.</summary>
    public static TickBundle Empty(PlayerId player, int tick) => new(player, tick, ImmutableArray<Command>.Empty);

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteInt32(Tick);
        writer.WriteUInt16((ushort)Commands.Length);
        foreach (var command in Commands)
        {
            writer.WriteCommand(command);
        }
    }

    internal static TickBundle ReadBody(ref MessageReader reader)
    {
        var player = reader.ReadPlayer();
        var tick = reader.ReadInt32();
        int count = reader.ReadUInt16();
        var commands = ImmutableArray.CreateBuilder<Command>(count);
        for (var i = 0; i < count; i++)
        {
            commands.Add(reader.ReadCommand());
        }

        var all = commands.MoveToImmutable();
        return Problem(player, tick, all) is { } problem ? throw MessageReader.Invalid(problem) : new TickBundle(player, tick, all);
    }

    private static string? Problem(PlayerId player, int tick, ImmutableArray<Command> commands)
    {
        if (player.Value < 1 || player.Value > PlayerId.MaxPlayers || tick < 0)
        {
            return $"A bundle of {player} for tick {tick}.";
        }

        if (commands.IsDefault || commands.Length > ushort.MaxValue)
        {
            return "A bundle needs a list of at most 65535 commands.";
        }

        foreach (var command in commands)
        {
            if (command.Player != player || command.ExecuteTick != tick)
            {
                return $"A command of {command.Player} for tick {command.ExecuteTick} in the bundle of {player} for tick {tick}.";
            }
        }

        return null;
    }

    // Records compare ImmutableArray by reference; a bundle compares by content.
    public bool Equals(TickBundle? other) =>
        other is not null && Player == other.Player && Tick == other.Tick && Commands.AsSpan().SequenceEqual(other.Commands.AsSpan());

    public override int GetHashCode() => HashCode.Combine(Player, Tick, Commands.Length);
}
