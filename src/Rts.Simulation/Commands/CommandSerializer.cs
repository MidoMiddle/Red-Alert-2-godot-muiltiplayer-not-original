using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Binary command format v1, little-endian regardless of the CPU. The same bytes go to the network
/// and into replays (docs/implementation/M0_foundation.md, 4.7):
/// <code>
/// u8 type | i32 player | i32 executeTick | u32 sequence | payload
/// MoveUnits:     u16 count | i32 × count ids | i32 x | i32 y
/// SpawnTestUnit: i32 x | i32 y
/// </code>
/// A batch is <c>u32 count</c> followed by the commands.
/// </summary>
public static class CommandSerializer
{
    public const int FormatVersion = 1;

    private const int HeaderSize = 1 + 4 + 4 + 4;
    private const int PositionSize = 4 + 4;

    public static int GetSize(Command command) => command switch
    {
        SpawnTestUnitCommand => HeaderSize + PositionSize,
        MoveUnitsCommand move => HeaderSize + 2 + 4 * move.Units.Length + PositionSize,
        _ => throw UnknownCommand(command),
    };

    /// <summary>Writes the command and returns the number of bytes written.</summary>
    public static int Write(Command command, Span<byte> destination)
    {
        var size = GetSize(command);
        if (destination.Length < size)
        {
            throw new ArgumentException($"Need {size} bytes, got {destination.Length}.", nameof(destination));
        }

        destination[0] = (byte)command.Type;
        BinaryPrimitives.WriteInt32LittleEndian(destination[1..], command.Player.Value);
        BinaryPrimitives.WriteInt32LittleEndian(destination[5..], command.ExecuteTick);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[9..], command.Sequence);
        var payload = destination[HeaderSize..];

        switch (command)
        {
            case SpawnTestUnitCommand spawn:
                WritePosition(payload, spawn.Position);
                break;
            case MoveUnitsCommand move:
                BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)move.Units.Length);
                var offset = 2;
                foreach (var id in move.Units)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(payload[offset..], id.Value);
                    offset += 4;
                }

                WritePosition(payload[offset..], move.Target);
                break;
        }

        return size;
    }

    /// <summary>Reads one command from the start of <paramref name="source"/>.</summary>
    public static Command Read(ReadOnlySpan<byte> source, out int bytesRead)
    {
        Require(source, HeaderSize);
        var type = (CommandType)source[0];
        var player = new PlayerId(BinaryPrimitives.ReadInt32LittleEndian(source[1..]));
        var tick = BinaryPrimitives.ReadInt32LittleEndian(source[5..]);
        var sequence = BinaryPrimitives.ReadUInt32LittleEndian(source[9..]);
        var payload = source[HeaderSize..];

        switch (type)
        {
            case CommandType.SpawnTestUnit:
            {
                Require(payload, PositionSize);
                bytesRead = HeaderSize + PositionSize;
                return new SpawnTestUnitCommand(player, tick, sequence, ReadPosition(payload));
            }

            case CommandType.MoveUnits:
            {
                Require(payload, 2);
                int count = BinaryPrimitives.ReadUInt16LittleEndian(payload);
                if (count > MoveUnitsCommand.MaxUnits)
                {
                    throw new InvalidDataException($"MoveUnits with {count} units exceeds the limit of {MoveUnitsCommand.MaxUnits}.");
                }

                Require(payload, 2 + 4 * count + PositionSize);
                var units = ImmutableArray.CreateBuilder<EntityId>(count);
                for (var i = 0; i < count; i++)
                {
                    units.Add(new EntityId(BinaryPrimitives.ReadInt32LittleEndian(payload[(2 + 4 * i)..])));
                }

                var target = ReadPosition(payload[(2 + 4 * count)..]);
                bytesRead = HeaderSize + 2 + 4 * count + PositionSize;
                return new MoveUnitsCommand(player, tick, sequence, units.MoveToImmutable(), target);
            }

            default:
                throw new InvalidDataException($"Unknown command type {(byte)type}.");
        }
    }

    /// <summary>Writes <c>u32 count</c> followed by every command.</summary>
    public static byte[] WriteAll(IReadOnlyList<Command> commands)
    {
        var size = 4;
        foreach (var command in commands)
        {
            size += GetSize(command);
        }

        var buffer = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)commands.Count);
        var offset = 4;
        foreach (var command in commands)
        {
            offset += Write(command, buffer.AsSpan(offset));
        }

        return buffer;
    }

    /// <summary>Reads a batch written by <see cref="WriteAll"/>. Trailing bytes are an error.</summary>
    public static IReadOnlyList<Command> ReadAll(ReadOnlySpan<byte> source)
    {
        var commands = ReadBatch(source, out var bytesRead);
        if (bytesRead != source.Length)
        {
            throw new InvalidDataException($"{source.Length - bytesRead} unexpected bytes after {commands.Count} commands.");
        }

        return commands;
    }

    /// <summary>Reads a batch from the start of <paramref name="source"/>; data after it is left for the caller.</summary>
    public static IReadOnlyList<Command> ReadBatch(ReadOnlySpan<byte> source, out int bytesRead)
    {
        Require(source, 4);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(source);
        var commands = new List<Command>();
        var offset = 4;
        for (var i = 0u; i < count; i++)
        {
            commands.Add(Read(source[offset..], out var read));
            offset += read;
        }

        bytesRead = offset;
        return commands;
    }

    private static void WritePosition(Span<byte> destination, WorldPos position)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, position.X);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], position.Y);
    }

    private static WorldPos ReadPosition(ReadOnlySpan<byte> source) => new(
        BinaryPrimitives.ReadInt32LittleEndian(source),
        BinaryPrimitives.ReadInt32LittleEndian(source[4..]));

    private static void Require(ReadOnlySpan<byte> source, int length)
    {
        if (source.Length < length)
        {
            throw new InvalidDataException($"Unexpected end of command data: need {length} bytes, have {source.Length}.");
        }
    }

    private static ArgumentException UnknownCommand(Command command) =>
        new($"Unknown command type {command.GetType().Name}.", nameof(command));
}
