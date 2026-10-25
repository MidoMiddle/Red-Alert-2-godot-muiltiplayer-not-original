using System;
using System.Buffers.Binary;
using System.Text;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Net.Protocol;

/// <summary>
/// Writes the fields of one message, little-endian (docs/implementation/M9_network.md, 4.4). The buffer is the largest message
/// allowed: a message that does not fit is an error of the writer, not a message to cut.
/// </summary>
public sealed class MessageWriter
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly byte[] _buffer;

    /// <param name="limit">The largest message: <see cref="NetProtocol.MaxMessageSize"/>, a frame of the relay a little more.</param>
    public MessageWriter(int limit = NetProtocol.MaxMessageSize)
    {
        _buffer = new byte[limit];
    }

    /// <summary>The bytes written so far.</summary>
    public int Length { get; private set; }

    public ReadOnlySpan<byte> Written => _buffer.AsSpan(0, Length);

    public byte[] ToArray() => Written.ToArray();

    public void WriteByte(byte value) => Take(1)[0] = value;

    public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    /// <summary>A small number 0…255 (a slot, a colour, a percent); anything else is the caller's mistake.</summary>
    public void WriteSmall(int value, string field)
    {
        if (value is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(field, value, "Expected 0…255.");
        }

        WriteByte((byte)value);
    }

    public void WriteUInt16(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Take(2), value);

    public void WriteInt32(int value) => BinaryPrimitives.WriteInt32LittleEndian(Take(4), value);

    public void WriteUInt32(uint value) => BinaryPrimitives.WriteUInt32LittleEndian(Take(4), value);

    public void WriteInt64(long value) => BinaryPrimitives.WriteInt64LittleEndian(Take(8), value);

    public void WriteUInt64(ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(Take(8), value);

    /// <summary><c>i32</c>: a player 1…8.</summary>
    public void WritePlayer(PlayerId player)
    {
        if (player.Value < 1 || player.Value > PlayerId.MaxPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(player), player, $"A player of a match is 1…{PlayerId.MaxPlayers}.");
        }

        WriteInt32(player.Value);
    }

    /// <summary><c>u16 length | UTF-8 bytes</c>, at most <see cref="NetProtocol.MaxStringBytes"/> bytes.</summary>
    public void WriteString(string value)
    {
        var length = Utf8.GetByteCount(value);
        if (length > NetProtocol.MaxStringBytes)
        {
            throw new ArgumentException($"A string of {length} bytes, the limit is {NetProtocol.MaxStringBytes}.", nameof(value));
        }

        WriteUInt16((ushort)length);
        Utf8.GetBytes(value, Take(length));
    }

    /// <summary>One command in the format of the simulation (<see cref="CommandSerializer"/>), the same bytes as in a replay.</summary>
    /// <summary>Bytes as they are.</summary>
    public void WriteBytes(ReadOnlySpan<byte> bytes) => bytes.CopyTo(Take(bytes.Length));

    public void WriteCommand(Command command) => CommandSerializer.Write(command, Take(CommandSerializer.GetSize(command)));

    private Span<byte> Take(int count)
    {
        if (Length + count > _buffer.Length)
        {
            throw new MessageFormatException(
                MessageError.TooLarge, $"The message does not fit into {_buffer.Length} bytes: {Length} written, {count} more.");
        }

        var span = _buffer.AsSpan(Length, count);
        Length += count;
        return span;
    }
}
