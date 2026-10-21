using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Rts.Simulation.Commands;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Replays;

/// <summary>
/// Binary <c>.rtsreplay</c> format v2, little-endian (docs/implementation/M0_foundation.md, 4.10):
/// <code>
/// "RTSR" | u16 formatVersion | u32 simVersion
/// u64 seed | i32 ticksPerSecond | i32 mapWidth | i32 mapHeight | i32 hashInterval | i32 tickCount
/// command batch (u32 count + commands, format of CommandSerializer)
/// u32 checksumCount | (i32 tick, u64 value) × checksumCount
/// </code>
/// The layout is unchanged since v1; v2 (M2 step 2) marks command format v2 inside.
/// </summary>
public static class ReplayFormat
{
    public const ushort FormatVersion = 2;
    public const string FileExtension = ".rtsreplay";

    private static ReadOnlySpan<byte> Magic => "RTSR"u8;

    private const int HeaderSize = 4 + 2 + 4 + 8 + 4 * 5;
    private const int ChecksumSize = 4 + 8;

    public static byte[] Write(Replay replay)
    {
        var commands = CommandSerializer.WriteAll(replay.Commands);
        var buffer = new byte[HeaderSize + commands.Length + 4 + ChecksumSize * replay.Checksums.Count];
        var span = buffer.AsSpan();

        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], FormatVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(span[6..], replay.SimVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(span[10..], replay.Settings.Seed);
        BinaryPrimitives.WriteInt32LittleEndian(span[18..], replay.Settings.TicksPerSecond);
        BinaryPrimitives.WriteInt32LittleEndian(span[22..], replay.Settings.MapWidthCells);
        BinaryPrimitives.WriteInt32LittleEndian(span[26..], replay.Settings.MapHeightCells);
        BinaryPrimitives.WriteInt32LittleEndian(span[30..], replay.Settings.HashInterval);
        BinaryPrimitives.WriteInt32LittleEndian(span[34..], replay.TickCount);

        var offset = HeaderSize;
        commands.CopyTo(span[offset..]);
        offset += commands.Length;

        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)replay.Checksums.Count);
        offset += 4;
        foreach (var checksum in replay.Checksums)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], checksum.Tick);
            BinaryPrimitives.WriteUInt64LittleEndian(span[(offset + 4)..], checksum.Value);
            offset += ChecksumSize;
        }

        return buffer;
    }

    public static Replay Read(ReadOnlySpan<byte> source)
    {
        Require(source, HeaderSize);
        if (!source[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a replay file: wrong magic bytes.");
        }

        var formatVersion = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        if (formatVersion != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported replay format version {formatVersion}, expected {FormatVersion}.");
        }

        var simVersion = BinaryPrimitives.ReadUInt32LittleEndian(source[6..]);
        var settings = new SimulationSettings(BinaryPrimitives.ReadUInt64LittleEndian(source[10..]))
        {
            TicksPerSecond = BinaryPrimitives.ReadInt32LittleEndian(source[18..]),
            MapWidthCells = BinaryPrimitives.ReadInt32LittleEndian(source[22..]),
            MapHeightCells = BinaryPrimitives.ReadInt32LittleEndian(source[26..]),
            HashInterval = BinaryPrimitives.ReadInt32LittleEndian(source[30..]),
        };
        var tickCount = BinaryPrimitives.ReadInt32LittleEndian(source[34..]);
        if (tickCount < 0)
        {
            throw new InvalidDataException($"Negative tick count {tickCount}.");
        }

        var offset = HeaderSize;
        var commands = CommandSerializer.ReadBatch(source[offset..], out var commandBytes);
        offset += commandBytes;

        Require(source[offset..], 4);
        var checksumCount = BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
        offset += 4;
        if ((ulong)checksumCount * ChecksumSize != (ulong)(source.Length - offset))
        {
            throw new InvalidDataException(
                $"Checksum section has {source.Length - offset} bytes, expected {checksumCount} × {ChecksumSize}.");
        }

        var checksums = new List<StateChecksum>((int)checksumCount);
        for (var i = 0; i < checksumCount; i++)
        {
            checksums.Add(new StateChecksum(
                BinaryPrimitives.ReadInt32LittleEndian(source[offset..]),
                BinaryPrimitives.ReadUInt64LittleEndian(source[(offset + 4)..])));
            offset += ChecksumSize;
        }

        return new Replay(simVersion, settings, tickCount, commands, checksums);
    }

    private static void Require(ReadOnlySpan<byte> source, int length)
    {
        if (source.Length < length)
        {
            throw new InvalidDataException($"Unexpected end of replay data: need {length} bytes, have {source.Length}.");
        }
    }
}
