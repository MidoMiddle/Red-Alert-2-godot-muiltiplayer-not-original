using Rts.Simulation.World;

namespace Rts.Net.Protocol.Messages;

/// <summary>
/// The checksum of a player's simulation at the start of a tick (docs/implementation/M9_network.md, 4.10), sent every 15 ticks:
/// <c>i32 player | i32 tick | u64 hash</c>.
/// </summary>
public sealed record Checksum(PlayerId Player, int Tick, ulong Hash) : NetMessage
{
    public override MessageType Type => MessageType.Checksum;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteInt32(Tick);
        writer.WriteUInt64(Hash);
    }

    internal static Checksum ReadBody(ref MessageReader reader) => new(reader.ReadPlayer(), ReadTick(ref reader), reader.ReadUInt64());

    /// <summary><c>i32</c>: a tick, never negative.</summary>
    internal static int ReadTick(ref MessageReader reader)
    {
        var tick = reader.ReadInt32();
        return tick >= 0 ? tick : throw MessageReader.Invalid($"Tick {tick}.");
    }
}

/// <summary>A client tells the host it has a desync (4.10): <c>i32 player | i32 tick | u64 hash</c> — its own hash on that tick.</summary>
public sealed record DesyncReport(PlayerId Player, int Tick, ulong Hash) : NetMessage
{
    public override MessageType Type => MessageType.DesyncReport;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteInt32(Tick);
        writer.WriteUInt64(Hash);
    }

    internal static DesyncReport ReadBody(ref MessageReader reader) =>
        new(reader.ReadPlayer(), Checksum.ReadTick(ref reader), reader.ReadUInt64());
}

/// <summary>
/// A question for the round trip time, on the unreliable channel (4.7): <c>i64 timestamp</c> in milliseconds of the sender's clock.
/// It never reaches the simulation.
/// </summary>
public sealed record Ping(long Timestamp) : NetMessage
{
    public override MessageType Type => MessageType.Ping;

    internal override void WriteBody(MessageWriter writer) => writer.WriteInt64(Timestamp);

    internal static Ping ReadBody(ref MessageReader reader) => new(reader.ReadInt64());
}

/// <summary>The answer to a <see cref="Ping"/>: its timestamp sent back as it was.</summary>
public sealed record Pong(long Timestamp) : NetMessage
{
    public override MessageType Type => MessageType.Pong;

    internal override void WriteBody(MessageWriter writer) => writer.WriteInt64(Timestamp);

    internal static Pong ReadBody(ref MessageReader reader) => new(reader.ReadInt64());
}

/// <summary>
/// The host changes the input delay (docs/implementation/M9_network.md, 4.7): from <see cref="Tick"/> on, a command given runs
/// <see cref="Delay"/> ticks later — <c>i32 tick | u8 delay</c>, 1…10. Every computer switches on that tick.
/// </summary>
public sealed record InputDelayChange(int Tick, int Delay) : NetMessage
{
    public const int MaxDelay = 10;

    public override MessageType Type => MessageType.InputDelayChange;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WriteInt32(Tick);
        writer.WriteSmall(Delay, nameof(Delay));
    }

    internal static InputDelayChange ReadBody(ref MessageReader reader)
    {
        var tick = Checksum.ReadTick(ref reader);
        int delay = reader.ReadByte();
        return delay is >= 1 and <= MaxDelay ? new InputDelayChange(tick, delay) : throw MessageReader.Invalid($"Input delay {delay}.");
    }
}
