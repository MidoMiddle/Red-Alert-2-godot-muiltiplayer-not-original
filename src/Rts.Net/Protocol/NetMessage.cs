namespace Rts.Net.Protocol;

/// <summary>
/// A network message (docs/implementation/M9_network.md, 4.4): immutable, fully described by its fields. <see cref="MessageCodec"/>
/// puts the header in front of the body and reads it back.
/// </summary>
public abstract record NetMessage
{
    public abstract MessageType Type { get; }

    /// <summary>Writes the fields after the header.</summary>
    internal abstract void WriteBody(MessageWriter writer);
}
