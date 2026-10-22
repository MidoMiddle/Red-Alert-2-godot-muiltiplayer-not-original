using System.Buffers.Binary;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Lockstep;

/// <summary>The bundle of a tick on the wire (docs/implementation/M9_network.md, 4.5, step 1).</summary>
public sealed class TickBundleTests
{
    [Fact]
    public void An_empty_bundle_is_thirteen_bytes()
    {
        var bytes = MessageCodec.Write(TickBundle.Empty(Samples.P2, 77));

        Assert.Equal(NetProtocol.HeaderSize + 4 + 4 + 2, bytes.Length);
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(3)));
        Assert.Equal(77, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(7)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(11)));
        Assert.True(((TickBundle)MessageCodec.Read(bytes)).IsEmpty);
    }

    [Fact]
    public void The_commands_go_in_the_bytes_of_the_simulation()
    {
        var commands = Samples.EveryCommand(Samples.P2, 5);
        var bytes = MessageCodec.Write(new TickBundle(Samples.P2, 5, commands));

        // The batch of a replay is u32 count + commands; a bundle is u16 count + the same commands.
        var batch = CommandSerializer.WriteAll(commands);
        Assert.Equal(batch.AsSpan(4).ToArray(), bytes.AsSpan(NetProtocol.HeaderSize + 4 + 4 + 2).ToArray());
        Assert.Equal(Enum.GetValues<CommandType>(), commands.Select(command => command.Type).Order());
    }

    [Fact]
    public void A_bundle_holds_only_the_commands_of_its_player_and_tick()
    {
        var other = Samples.EveryCommand(new PlayerId(1), 5);
        Assert.Throws<ArgumentException>(() => new TickBundle(Samples.P2, 5, other));
        Assert.Throws<ArgumentException>(() => new TickBundle(Samples.P2, 6, Samples.EveryCommand(Samples.P2, 5)));

        // The same from the wire: the player of the second command patched from 2 to 1.
        var bytes = MessageCodec.Write(new TickBundle(Samples.P2, 5, Samples.EveryCommand(Samples.P2, 5)));
        var second = NetProtocol.HeaderSize + 4 + 4 + 2 + CommandSerializer.GetSize(Samples.EveryCommand(Samples.P2, 5)[0]);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(second + 1), 1);
        var error = Assert.Throws<MessageFormatException>(() => MessageCodec.Read(bytes));
        Assert.Equal(MessageError.InvalidValue, error.Error);
    }

    [Fact]
    public void A_broken_command_inside_is_refused()
    {
        var bytes = MessageCodec.Write(new TickBundle(Samples.P2, 5, [new SurrenderCommand(Samples.P2, 5, 0)]));
        bytes[NetProtocol.HeaderSize + 4 + 4 + 2] = 250;

        var error = Assert.Throws<MessageFormatException>(() => MessageCodec.Read(bytes));

        Assert.Equal(MessageError.InvalidValue, error.Error);
    }
}
