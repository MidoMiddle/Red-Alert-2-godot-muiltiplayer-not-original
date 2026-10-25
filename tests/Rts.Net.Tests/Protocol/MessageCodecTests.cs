using System.Buffers.Binary;
using System.Collections.Immutable;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Protocol;

/// <summary>
/// The messages of the protocol (docs/implementation/M9_network.md, 4.4, step 1, section 6): every type there and back, the header,
/// and a refusal for whatever is cut, too large, of another version or broken.
/// </summary>
public sealed class MessageCodecTests
{
    public static TheoryData<NetMessage> Messages()
    {
        var data = new TheoryData<NetMessage>();
        foreach (var message in Samples.EveryMessage())
        {
            data.Add(message);
        }

        return data;
    }

    [Fact]
    public void The_samples_have_a_message_of_every_type()
    {
        Assert.Equal(Enum.GetValues<MessageType>(), Samples.EveryMessage().Select(message => message.Type).Order());
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void A_message_comes_back_as_it_was_sent(NetMessage message)
    {
        var bytes = MessageCodec.Write(message);
        var read = MessageCodec.Read(bytes);

        Assert.Equal(message, read);
        Assert.Equal(bytes, MessageCodec.Write(read));
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Every_message_starts_with_its_type_and_the_protocol_version(NetMessage message)
    {
        var bytes = MessageCodec.Write(message);

        Assert.Equal((byte)message.Type, bytes[0]);
        Assert.Equal(NetProtocol.Version, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(1)));
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void A_cut_message_is_refused_wherever_it_is_cut(NetMessage message)
    {
        var bytes = MessageCodec.Write(message);
        for (var length = 0; length < bytes.Length; length++)
        {
            Assert.Throws<MessageFormatException>(() => MessageCodec.Read(bytes.AsSpan(0, length)));
        }
    }

    [Fact]
    public void A_message_cut_inside_a_field_is_truncated()
    {
        var bytes = MessageCodec.Write(new Checksum(Samples.P2, 15, 99));

        Assert.Equal(MessageError.Truncated, Error(bytes[..^1]));
        Assert.Equal(MessageError.Truncated, Error(bytes[..2]));
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Bytes_after_the_last_field_are_refused(NetMessage message)
    {
        byte[] bytes = [.. MessageCodec.Write(message), 0];

        Assert.Equal(MessageError.TrailingBytes, Error(bytes));
    }

    [Fact]
    public void A_message_larger_than_the_limit_is_refused_on_both_ends()
    {
        var bytes = new byte[NetProtocol.MaxMessageSize + 1];
        bytes[0] = (byte)MessageType.Ping;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1), NetProtocol.Version);
        Assert.Equal(MessageError.TooLarge, Error(bytes));

        // 16 moves of 256 units each are some 17 KB: such a bundle cannot be sent.
        var units = Enumerable.Range(1, UnitsCommand.MaxUnits).Select(id => new EntityId(id)).ToImmutableArray();
        var moves = Enumerable.Range(0, 16)
            .Select(i => (Command)new MoveCommand(Samples.P2, 10, (uint)i, units, new Rts.Simulation.Geometry.WorldPos(0, 0)))
            .ToImmutableArray();
        var error = Assert.Throws<MessageFormatException>(() => MessageCodec.Write(new TickBundle(Samples.P2, 10, moves)));
        Assert.Equal(MessageError.TooLarge, error.Error);
    }

    [Fact]
    public void A_message_of_another_protocol_version_is_refused_with_its_version()
    {
        var bytes = MessageCodec.Write(new Ping(1));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1), NetProtocol.Version + 1);

        var error = Assert.Throws<MessageFormatException>(() => MessageCodec.Read(bytes));

        Assert.Equal(MessageError.WrongProtocolVersion, error.Error);
        Assert.Equal((ushort)(NetProtocol.Version + 1), error.ProtocolVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(255)]
    public void An_unknown_type_is_refused(byte type)
    {
        var bytes = MessageCodec.Write(new Ping(1));
        bytes[0] = type;

        Assert.Equal(MessageError.UnknownType, Error(bytes));
    }

    [Fact]
    public void Values_the_protocol_does_not_allow_are_refused()
    {
        // The player 1…8: the first field of the checksum.
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new Checksum(Samples.P2, 15, 1), 3, 0)));
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new Checksum(Samples.P2, 15, 1), 3, 9)));
        // A negative tick.
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new Checksum(Samples.P2, 15, 1), 7, 0xFF, 0xFF, 0xFF, 0xFF)));
        // A flag that is neither 0 nor 1.
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new PauseRequest(Samples.P2, true), 7, 2)));
        // A reason that does not exist.
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new Reject(RejectReason.MapHash, ""), 3, 200)));
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new PlayerLeft(Samples.P2, LeaveReason.Quit, 1), 7, 0)));
        // More than 100 %.
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new LoadProgress(Samples.P2, 100), 7, 101)));
        // A slot out of 1…8.
        Assert.Equal(MessageError.InvalidValue, Error(Patch(new Welcome(1), 3, 9)));
    }

    [Fact]
    public void A_string_must_be_short_valid_utf8()
    {
        var bytes = MessageCodec.Write(new Reject(RejectReason.GameVersion, "ab"));
        bytes[^1] = 0xFF;
        Assert.Equal(MessageError.InvalidValue, Error(bytes));

        Assert.Throws<ArgumentException>(() => MessageCodec.Write(new Reject(RejectReason.GameVersion, new string('x', NetProtocol.MaxStringBytes + 1))));
        var longest = new Reject(RejectReason.GameVersion, new string('я', NetProtocol.MaxStringBytes / 2));
        Assert.Equal(longest, MessageCodec.Read(MessageCodec.Write(longest)));
    }

    private static MessageError Error(ReadOnlySpan<byte> bytes)
    {
        try
        {
            MessageCodec.Read(bytes);
        }
        catch (MessageFormatException e)
        {
            return e.Error;
        }

        throw new Xunit.Sdk.XunitException("The message was read.");
    }

    private static byte[] Patch(NetMessage message, int offset, params byte[] values)
    {
        var bytes = MessageCodec.Write(message);
        values.CopyTo(bytes, offset);
        return bytes;
    }
}
