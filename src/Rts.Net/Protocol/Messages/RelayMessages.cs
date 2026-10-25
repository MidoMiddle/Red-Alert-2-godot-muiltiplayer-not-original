using System;

namespace Rts.Net.Protocol.Messages;

/// <summary>
/// The first word to the relay (docs/implementation/M9_network.md, 4.14, step 12): <c>string code</c> — empty to open a new room, the
/// code of a room to join it. The relay answers <see cref="RoomJoined"/> or <see cref="Reject"/>.
/// </summary>
public sealed record RoomRequest(string Code) : NetMessage
{
    public override MessageType Type => MessageType.RoomRequest;

    internal override void WriteBody(MessageWriter writer) => writer.WriteString(Code);

    internal static RoomRequest ReadBody(ref MessageReader reader) => new(reader.ReadString());
}

/// <summary>
/// The relay took us into a room: <c>string code | u8 member</c>. Member 1 opened the room and hosts its lobby; the others are numbered
/// as they come.
/// </summary>
public sealed record RoomJoined(string Code, int Member) : NetMessage
{
    public override MessageType Type => MessageType.RoomJoined;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WriteString(Code);
        writer.WriteByte(checked((byte)Member));
    }

    internal static RoomJoined ReadBody(ref MessageReader reader)
    {
        var code = reader.ReadString();
        var member = reader.ReadByte();
        return member >= 1 ? new RoomJoined(code, member) : throw MessageReader.Invalid($"Member {member}.");
    }
}

/// <summary>
/// One came into the room or left it: <c>u8 member | u8 here</c>. From the relay: to the host about a client, to a client about the host.
/// From the host to the relay, <c>here = 0</c> puts that member out of the room.
/// </summary>
public sealed record RoomMember(int Member, bool Here) : NetMessage
{
    public override MessageType Type => MessageType.RoomMember;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WriteByte(checked((byte)Member));
        writer.WriteBool(Here);
    }

    internal static RoomMember ReadBody(ref MessageReader reader)
    {
        var member = reader.ReadByte();
        return member >= 1 ? new RoomMember(member, reader.ReadBool()) : throw MessageReader.Invalid($"Member {member}.");
    }
}

/// <summary>
/// A message passed through the relay: <c>u8 member | u16 length | message</c>. To the relay, the member it is for — 0 for all of ours: every client
/// for the host, the host for a client; from the relay, the member it is from. The message inside is any message of this protocol,
/// whole, header and all.
/// </summary>
public sealed record Relayed(int Member, byte[] Payload) : NetMessage
{
    public override MessageType Type => MessageType.Relayed;

    internal override void WriteBody(MessageWriter writer)
    {
        if (Payload.Length > NetProtocol.MaxMessageSize)
        {
            throw new MessageFormatException(MessageError.TooLarge, $"A message of {Payload.Length} bytes cannot be relayed.");
        }

        writer.WriteByte(checked((byte)Member));
        writer.WriteUInt16((ushort)Payload.Length);
        writer.WriteBytes(Payload);
    }

    internal static Relayed ReadBody(ref MessageReader reader)
    {
        var member = reader.ReadByte();
        var length = reader.ReadUInt16();
        return length <= NetProtocol.MaxMessageSize
            ? new Relayed(member, reader.ReadBytes(length).ToArray())
            : throw new MessageFormatException(MessageError.TooLarge, $"A relayed message of {length} bytes.");
    }

    /// <summary>The same member and the same bytes.</summary>
    public bool Equals(Relayed? other) => other is not null && Member == other.Member && Payload.AsSpan().SequenceEqual(other.Payload);

    public override int GetHashCode() => HashCode.Combine(Member, Payload.Length);

    /// <summary>The bytes of a relay frame, without a copy of the message into a record first.</summary>
    public static byte[] Frame(int member, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > NetProtocol.MaxMessageSize)
        {
            throw new MessageFormatException(MessageError.TooLarge, $"A message of {payload.Length} bytes cannot be relayed.");
        }

        var frame = new byte[NetProtocol.HeaderSize + 1 + 2 + payload.Length];
        frame[0] = (byte)MessageType.Relayed;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(1), NetProtocol.Version);
        frame[NetProtocol.HeaderSize] = checked((byte)member);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(NetProtocol.HeaderSize + 1), (ushort)payload.Length);
        payload.CopyTo(frame.AsSpan(NetProtocol.HeaderSize + 3));
        return frame;
    }
}
