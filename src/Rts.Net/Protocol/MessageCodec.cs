using System;
using Rts.Net.Lockstep;
using Rts.Net.Protocol.Messages;

namespace Rts.Net.Protocol;

/// <summary>
/// Turns a message into bytes and back (docs/implementation/M9_network.md, 4.4): <c>u8 messageType | u16 protocolVersion | body</c>.
/// Reading checks the size, the version, the type, every field and that nothing is left over.
/// </summary>
public static class MessageCodec
{
    public static byte[] Write(NetMessage message)
    {
        var writer = new MessageWriter(LimitOf(message.Type));
        writer.WriteByte((byte)message.Type);
        writer.WriteUInt16(NetProtocol.Version);
        message.WriteBody(writer);
        return writer.ToArray();
    }

    public static NetMessage Read(ReadOnlySpan<byte> source)
    {
        var limit = source.Length > 0 ? LimitOf((MessageType)source[0]) : NetProtocol.MaxMessageSize;
        if (source.Length > limit)
        {
            throw new MessageFormatException(MessageError.TooLarge, $"A message of {source.Length} bytes, the limit is {limit}.");
        }

        var reader = new MessageReader(source);
        var type = (MessageType)reader.ReadByte();
        var version = reader.ReadUInt16();
        if (version != NetProtocol.Version)
        {
            throw new MessageFormatException(
                MessageError.WrongProtocolVersion, $"Protocol version {version}, this game speaks {NetProtocol.Version}.")
            {
                ProtocolVersion = version,
            };
        }

        NetMessage message = type switch
        {
            MessageType.Hello => Hello.ReadBody(ref reader),
            MessageType.Welcome => Welcome.ReadBody(ref reader),
            MessageType.Reject => Reject.ReadBody(ref reader),
            MessageType.LobbyState => LobbyState.ReadBody(ref reader),
            MessageType.LobbyChange => LobbyChange.ReadBody(ref reader),
            MessageType.StartGame => StartGame.ReadBody(ref reader),
            MessageType.LoadProgress => LoadProgress.ReadBody(ref reader),
            MessageType.AllLoaded => AllLoaded.ReadBody(ref reader),
            MessageType.TickBundle => TickBundle.ReadBody(ref reader),
            MessageType.Checksum => Checksum.ReadBody(ref reader),
            MessageType.Ping => Ping.ReadBody(ref reader),
            MessageType.Pong => Pong.ReadBody(ref reader),
            MessageType.PauseRequest => PauseRequest.ReadBody(ref reader),
            MessageType.Pause => Pause.ReadBody(ref reader),
            MessageType.Resume => Resume.ReadBody(ref reader),
            MessageType.PlayerLeft => PlayerLeft.ReadBody(ref reader),
            MessageType.Surrender => Surrender.ReadBody(ref reader),
            MessageType.DesyncReport => DesyncReport.ReadBody(ref reader),
            MessageType.InputDelayChange => InputDelayChange.ReadBody(ref reader),
            MessageType.KickVote => KickVote.ReadBody(ref reader),
            MessageType.RoomRequest => RoomRequest.ReadBody(ref reader),
            MessageType.RoomJoined => RoomJoined.ReadBody(ref reader),
            MessageType.RoomMember => RoomMember.ReadBody(ref reader),
            MessageType.Relayed => Relayed.ReadBody(ref reader),
            _ => throw new MessageFormatException(MessageError.UnknownType, $"Unknown message type {(byte)type}."),
        };

        reader.ExpectEnd();
        return message;
    }

    // A frame of the relay carries a whole message: a little more room.
    private static int LimitOf(MessageType type) => type == MessageType.Relayed ? NetProtocol.MaxFrameSize : NetProtocol.MaxMessageSize;
}
