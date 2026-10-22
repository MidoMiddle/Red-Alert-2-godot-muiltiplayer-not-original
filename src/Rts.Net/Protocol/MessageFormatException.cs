using System;

namespace Rts.Net.Protocol;

/// <summary>What is wrong with a message that cannot be read or written.</summary>
public enum MessageError : byte
{
    /// <summary>The message ends before its fields do.</summary>
    Truncated = 1,

    /// <summary>The message is longer than <see cref="NetProtocol.MaxMessageSize"/>.</summary>
    TooLarge = 2,

    /// <summary>The first byte names no <see cref="MessageType"/>.</summary>
    UnknownType = 3,

    /// <summary>The message comes from another version of the protocol; <see cref="MessageFormatException.ProtocolVersion"/> says which.</summary>
    WrongProtocolVersion = 4,

    /// <summary>Bytes are left after the last field.</summary>
    TrailingBytes = 5,

    /// <summary>A field holds a value the protocol does not allow.</summary>
    InvalidValue = 6,
}

/// <summary>
/// A message that breaks the protocol (docs/implementation/M9_network.md, 4.4). The peer that sent it is broken or hostile; what to do
/// with it — drop the message, refuse the peer — is for the session.
/// </summary>
public sealed class MessageFormatException : Exception
{
    public MessageFormatException(MessageError error, string message, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
    }

    public MessageError Error { get; }

    /// <summary>The version the message came with, when the error is <see cref="MessageError.WrongProtocolVersion"/>.</summary>
    public ushort? ProtocolVersion { get; init; }
}
