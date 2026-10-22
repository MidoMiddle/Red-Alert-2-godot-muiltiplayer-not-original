using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Binary command format v7, little-endian regardless of the CPU. The same bytes go to the network
/// and into replays (docs/implementation/M0_foundation.md, 4.7; M2_unit_control.md, 4.10):
/// <code>
/// u8 type | i32 player | i32 executeTick | u32 sequence | payload
/// SpawnTestUnit: u8 unitType | i32 x | i32 y
/// Move:          u8 queue | units | i32 x | i32 y
/// Stop:          units
/// AttackMove:    u8 queue | units | i32 x | i32 y
/// Waypoints:     u8 loop | units | u8 pointCount | (i32 x | i32 y) × pointCount
/// Scatter:       units
/// DamageMapObject: i32 cellX | i32 cellY | i32 amount
/// PlaceBuilding: u16 defIndex | i32 cellX | i32 cellY
/// Deploy:        i32 unit
/// Produce:       u8 kind | u16 defIndex | u8 count
/// PauseProduction, CancelProduction: u8 kind | u16 defIndex
/// SetRallyPoint: i32 building | i32 x | i32 y
/// Harvest:       units | i32 cellX | i32 cellY
/// units = u16 count | i32 × count ids
/// </code>
/// A batch is <c>u32 count</c> followed by the commands.
/// Version history: v1 (M0); v2 (M2 step 2) added the unit type to SpawnTestUnit; v3 (M2 step 12) added the queue flag,
/// Stop, AttackMove, Waypoints and Scatter; v4 (M7 step 5) added DamageMapObject; v5 (M3 step 6) added PlaceBuilding and Deploy; v6 (M3 step 8) added Produce, PauseProduction,
/// CancelProduction and SetRallyPoint; v7 (M3 step 10) added Harvest.
/// </summary>
public static class CommandSerializer
{
    public const int FormatVersion = 7;

    private const int HeaderSize = 1 + 4 + 4 + 4;
    private const int PositionSize = 4 + 4;
    private const int SpawnSize = 1 + PositionSize;

    // u8 kind | u16 defIndex.
    private const int ProductSize = 1 + 2;

    public static int GetSize(Command command) => command switch
    {
        SpawnTestUnitCommand => HeaderSize + SpawnSize,
        MoveCommand move => HeaderSize + 1 + UnitsSize(move) + PositionSize,
        AttackMoveCommand attack => HeaderSize + 1 + UnitsSize(attack) + PositionSize,
        StopCommand stop => HeaderSize + UnitsSize(stop),
        ScatterCommand scatter => HeaderSize + UnitsSize(scatter),
        WaypointsCommand route => HeaderSize + 1 + UnitsSize(route) + 1 + PositionSize * route.Points.Length,
        DamageMapObjectCommand => HeaderSize + PositionSize + 4,
        PlaceBuildingCommand => HeaderSize + 2 + PositionSize,
        DeployCommand => HeaderSize + 4,
        ProduceCommand => HeaderSize + ProductSize + 1,
        PauseProductionCommand or CancelProductionCommand => HeaderSize + ProductSize,
        SetRallyPointCommand => HeaderSize + 4 + PositionSize,
        HarvestCommand harvest => HeaderSize + UnitsSize(harvest) + PositionSize,
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
                payload[0] = (byte)spawn.UnitType;
                WritePosition(payload[1..], spawn.Position);
                break;
            case MoveCommand move:
                payload[0] = move.Queue ? (byte)1 : (byte)0;
                WritePosition(payload[(1 + WriteUnits(payload[1..], move))..], move.Target);
                break;
            case AttackMoveCommand attack:
                payload[0] = attack.Queue ? (byte)1 : (byte)0;
                WritePosition(payload[(1 + WriteUnits(payload[1..], attack))..], attack.Target);
                break;
            case StopCommand stop:
                WriteUnits(payload, stop);
                break;
            case ScatterCommand scatter:
                WriteUnits(payload, scatter);
                break;
            case WaypointsCommand route:
                payload[0] = route.Loop ? (byte)1 : (byte)0;
                var offset = 1 + WriteUnits(payload[1..], route);
                payload[offset++] = (byte)route.Points.Length;
                foreach (var point in route.Points)
                {
                    WritePosition(payload[offset..], point);
                    offset += PositionSize;
                }

                break;
            case DamageMapObjectCommand damage:
                BinaryPrimitives.WriteInt32LittleEndian(payload, damage.Cell.X);
                BinaryPrimitives.WriteInt32LittleEndian(payload[4..], damage.Cell.Y);
                BinaryPrimitives.WriteInt32LittleEndian(payload[8..], damage.Amount);
                break;
            case PlaceBuildingCommand place:
                if (place.DefIndex is < 0 or > ushort.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(command), place.DefIndex, "A definition index is 0…65535.");
                }

                BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)place.DefIndex);
                BinaryPrimitives.WriteInt32LittleEndian(payload[2..], place.Cell.X);
                BinaryPrimitives.WriteInt32LittleEndian(payload[6..], place.Cell.Y);
                break;
            case DeployCommand deploy:
                BinaryPrimitives.WriteInt32LittleEndian(payload, deploy.Unit.Value);
                break;
            case ProduceCommand produce:
                WriteProduct(payload, produce.Kind, produce.DefIndex);
                payload[ProductSize] = produce.Count is >= 0 and <= byte.MaxValue
                    ? (byte)produce.Count
                    : throw new ArgumentOutOfRangeException(nameof(command), produce.Count, "A count is 0…255.");
                break;
            case PauseProductionCommand pause:
                WriteProduct(payload, pause.Kind, pause.DefIndex);
                break;
            case CancelProductionCommand cancel:
                WriteProduct(payload, cancel.Kind, cancel.DefIndex);
                break;
            case SetRallyPointCommand rally:
                BinaryPrimitives.WriteInt32LittleEndian(payload, rally.Building.Value);
                WritePosition(payload[4..], rally.Point);
                break;
            case HarvestCommand harvest:
                var unitsEnd = WriteUnits(payload, harvest);
                BinaryPrimitives.WriteInt32LittleEndian(payload[unitsEnd..], harvest.Cell.X);
                BinaryPrimitives.WriteInt32LittleEndian(payload[(unitsEnd + 4)..], harvest.Cell.Y);
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
                // Any type byte is read as is; whether the type exists is a rules question for CommandProcessor.
                Require(payload, SpawnSize);
                bytesRead = HeaderSize + SpawnSize;
                return new SpawnTestUnitCommand(player, tick, sequence, (UnitType)payload[0], ReadPosition(payload[1..]));
            }

            case CommandType.Move:
            case CommandType.AttackMove:
            {
                Require(payload, 1);
                var queue = ReadFlag(payload[0]);
                var units = ReadUnits(payload[1..], out var unitsSize);
                Require(payload, 1 + unitsSize + PositionSize);
                var target = ReadPosition(payload[(1 + unitsSize)..]);
                bytesRead = HeaderSize + 1 + unitsSize + PositionSize;
                return type == CommandType.Move
                    ? new MoveCommand(player, tick, sequence, units, target, queue)
                    : new AttackMoveCommand(player, tick, sequence, units, target, queue);
            }

            case CommandType.Stop:
            case CommandType.Scatter:
            {
                var units = ReadUnits(payload, out var unitsSize);
                bytesRead = HeaderSize + unitsSize;
                return type == CommandType.Stop
                    ? new StopCommand(player, tick, sequence, units)
                    : new ScatterCommand(player, tick, sequence, units);
            }

            case CommandType.Waypoints:
            {
                Require(payload, 1);
                var loop = ReadFlag(payload[0]);
                var units = ReadUnits(payload[1..], out var unitsSize);
                var offset = 1 + unitsSize;
                Require(payload, offset + 1);
                int count = payload[offset++];
                if (count is 0 or > WaypointsCommand.MaxPoints)
                {
                    throw new InvalidDataException($"Waypoints with {count} points, expected 1…{WaypointsCommand.MaxPoints}.");
                }

                Require(payload, offset + PositionSize * count);
                var points = ImmutableArray.CreateBuilder<WorldPos>(count);
                for (var i = 0; i < count; i++)
                {
                    points.Add(ReadPosition(payload[(offset + PositionSize * i)..]));
                }

                bytesRead = HeaderSize + offset + PositionSize * count;
                return new WaypointsCommand(player, tick, sequence, units, points.MoveToImmutable(), loop);
            }

            case CommandType.DamageMapObject:
            {
                // Any cell and amount are read as they are; CommandProcessor decides whether they are allowed.
                Require(payload, PositionSize + 4);
                bytesRead = HeaderSize + PositionSize + 4;
                var cell = new CellPos(BinaryPrimitives.ReadInt32LittleEndian(payload), BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
                return new DamageMapObjectCommand(player, tick, sequence, cell, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
            }

            case CommandType.PlaceBuilding:
            {
                // Whether the definition exists and the cell is allowed is for CommandProcessor.
                Require(payload, 2 + PositionSize);
                bytesRead = HeaderSize + 2 + PositionSize;
                var cell = new CellPos(BinaryPrimitives.ReadInt32LittleEndian(payload[2..]), BinaryPrimitives.ReadInt32LittleEndian(payload[6..]));
                return new PlaceBuildingCommand(player, tick, sequence, BinaryPrimitives.ReadUInt16LittleEndian(payload), cell);
            }

            case CommandType.Deploy:
            {
                Require(payload, 4);
                bytesRead = HeaderSize + 4;
                return new DeployCommand(player, tick, sequence, new EntityId(BinaryPrimitives.ReadInt32LittleEndian(payload)));
            }

            case CommandType.Produce:
            {
                // Any kind, index and count are read as they are; CommandProcessor decides whether they are allowed.
                Require(payload, ProductSize + 1);
                bytesRead = HeaderSize + ProductSize + 1;
                return new ProduceCommand(player, tick, sequence, (ProductionKind)payload[0], ReadIndex(payload), payload[ProductSize]);
            }

            case CommandType.PauseProduction:
            case CommandType.CancelProduction:
            {
                Require(payload, ProductSize);
                bytesRead = HeaderSize + ProductSize;
                return type == CommandType.PauseProduction
                    ? new PauseProductionCommand(player, tick, sequence, (ProductionKind)payload[0], ReadIndex(payload))
                    : new CancelProductionCommand(player, tick, sequence, (ProductionKind)payload[0], ReadIndex(payload));
            }

            case CommandType.Harvest:
            {
                var units = ReadUnits(payload, out var unitsSize);
                Require(payload, unitsSize + PositionSize);
                bytesRead = HeaderSize + unitsSize + PositionSize;
                var cell = new CellPos(
                    BinaryPrimitives.ReadInt32LittleEndian(payload[unitsSize..]), BinaryPrimitives.ReadInt32LittleEndian(payload[(unitsSize + 4)..]));
                return new HarvestCommand(player, tick, sequence, units, cell);
            }

            case CommandType.SetRallyPoint:
            {
                Require(payload, 4 + PositionSize);
                bytesRead = HeaderSize + 4 + PositionSize;
                var building = new EntityId(BinaryPrimitives.ReadInt32LittleEndian(payload));
                return new SetRallyPointCommand(player, tick, sequence, building, ReadPosition(payload[4..]));
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

    private static int UnitsSize(UnitsCommand command) => 2 + 4 * command.Units.Length;

    /// <summary>Writes <c>u8 kind | u16 defIndex</c>.</summary>
    private static void WriteProduct(Span<byte> destination, ProductionKind kind, int defIndex)
    {
        if (defIndex is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(defIndex), defIndex, "A definition index is 0…65535.");
        }

        destination[0] = (byte)kind;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[1..], (ushort)defIndex);
    }

    private static int ReadIndex(ReadOnlySpan<byte> payload) => BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]);

    /// <summary>Writes <c>u16 count | i32 ids</c>; returns the bytes written.</summary>
    private static int WriteUnits(Span<byte> destination, UnitsCommand command)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)command.Units.Length);
        var offset = 2;
        foreach (var id in command.Units)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], id.Value);
            offset += 4;
        }

        return offset;
    }

    private static ImmutableArray<EntityId> ReadUnits(ReadOnlySpan<byte> source, out int size)
    {
        Require(source, 2);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(source);
        if (count > UnitsCommand.MaxUnits)
        {
            throw new InvalidDataException($"Command with {count} units exceeds the limit of {UnitsCommand.MaxUnits}.");
        }

        size = 2 + 4 * count;
        Require(source, size);
        var units = ImmutableArray.CreateBuilder<EntityId>(count);
        for (var i = 0; i < count; i++)
        {
            units.Add(new EntityId(BinaryPrimitives.ReadInt32LittleEndian(source[(2 + 4 * i)..])));
        }

        return units.MoveToImmutable();
    }

    private static bool ReadFlag(byte value) => value switch
    {
        0 => false,
        1 => true,
        _ => throw new InvalidDataException($"Invalid flag byte {value}."),
    };

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
