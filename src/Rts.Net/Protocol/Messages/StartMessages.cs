using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Net.Protocol.Messages;

/// <summary>
/// The host starts the match (docs/implementation/M9_network.md, 4.4, 4.9): the match setup with its seed, the settings of the
/// simulation that are not in it, the hash of the map and the starting input delay —
/// <c>setup | i32 ticksPerSecond | i32 hashInterval | u64 mapHash | u8 inputDelay</c>. The map itself is not sent: every client loads
/// its own copy and checks it by the hash.
/// </summary>
public sealed record StartGame(MatchSetup Setup, int TicksPerSecond, int HashInterval, ulong MapHash, int InputDelay) : NetMessage
{
    public override MessageType Type => MessageType.StartGame;

    internal override void WriteBody(MessageWriter writer)
    {
        MatchFields.WriteSetup(writer, Setup);
        writer.WriteInt32(TicksPerSecond);
        writer.WriteInt32(HashInterval);
        writer.WriteUInt64(MapHash);
        writer.WriteSmall(InputDelay, nameof(InputDelay));
    }

    internal static StartGame ReadBody(ref MessageReader reader) =>
        new(MatchFields.ReadSetup(ref reader), reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt64(), reader.ReadByte());
}

/// <summary>How far a client has loaded: <c>i32 player | u8 percent</c>, 0…100.</summary>
public sealed record LoadProgress(PlayerId Player, int Percent) : NetMessage
{
    public override MessageType Type => MessageType.LoadProgress;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteSmall(Percent, nameof(Percent));
    }

    internal static LoadProgress ReadBody(ref MessageReader reader)
    {
        var player = reader.ReadPlayer();
        int percent = reader.ReadByte();
        return percent <= 100 ? new LoadProgress(player, percent) : throw MessageReader.Invalid($"Loaded {percent} %.");
    }
}

/// <summary>Everyone has loaded: tick 0 begins. No body.</summary>
public sealed record AllLoaded : NetMessage
{
    public override MessageType Type => MessageType.AllLoaded;

    internal override void WriteBody(MessageWriter writer)
    {
    }

    internal static AllLoaded ReadBody(ref MessageReader reader) => new();
}
