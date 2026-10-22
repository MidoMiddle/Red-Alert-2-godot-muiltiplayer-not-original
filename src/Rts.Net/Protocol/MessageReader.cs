using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Net.Protocol;

/// <summary>
/// Reads the fields of one message, little-endian (docs/implementation/M9_network.md, 4.4). The bytes come from another computer, so
/// every read is checked: a short message, a bad flag or string, a player out of range is a <see cref="MessageFormatException"/>.
/// </summary>
public ref struct MessageReader
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ReadOnlySpan<byte> _source;
    private int _position;

    public MessageReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        _position = 0;
    }

    /// <summary>The bytes not read yet.</summary>
    public readonly int Remaining => _source.Length - _position;

    public byte ReadByte() => Take(1)[0];

    public bool ReadBool() => ReadByte() switch
    {
        0 => false,
        1 => true,
        var value => throw Invalid($"A flag byte {value}."),
    };

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));

    public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

    /// <summary><c>i32</c>: a player 1…8.</summary>
    public PlayerId ReadPlayer()
    {
        var value = ReadInt32();
        return value is >= 1 and <= PlayerId.MaxPlayers ? new PlayerId(value) : throw Invalid($"Player {value}, expected 1…{PlayerId.MaxPlayers}.");
    }

    /// <summary><c>u16 length | UTF-8 bytes</c>, at most <see cref="NetProtocol.MaxStringBytes"/> bytes of valid UTF-8.</summary>
    public string ReadString()
    {
        int length = ReadUInt16();
        if (length > NetProtocol.MaxStringBytes)
        {
            throw Invalid($"A string of {length} bytes, the limit is {NetProtocol.MaxStringBytes}.");
        }

        var bytes = Take(length);
        try
        {
            return Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw new MessageFormatException(MessageError.InvalidValue, "A string that is not UTF-8.", e);
        }
    }

    /// <summary>One command in the format of the simulation (<see cref="CommandSerializer"/>).</summary>
    public Command ReadCommand()
    {
        Command command;
        int read;
        try
        {
            command = CommandSerializer.Read(_source[_position..], out read);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException)
        {
            throw new MessageFormatException(MessageError.InvalidValue, $"A broken command: {e.Message}", e);
        }

        _position += read;
        return command;
    }

    /// <summary>The message must end here.</summary>
    public readonly void ExpectEnd()
    {
        if (Remaining != 0)
        {
            throw new MessageFormatException(MessageError.TrailingBytes, $"{Remaining} bytes after the last field.");
        }
    }

    public static MessageFormatException Invalid(string message) => new(MessageError.InvalidValue, message);

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > Remaining)
        {
            throw new MessageFormatException(MessageError.Truncated, $"The message ends early: {count} bytes needed, {Remaining} left.");
        }

        var span = _source.Slice(_position, count);
        _position += count;
        return span;
    }
}
