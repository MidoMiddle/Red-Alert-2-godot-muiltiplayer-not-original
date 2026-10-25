using System;
using Rts.Simulation.World;

namespace Rts.Net.Protocol.Messages;

/// <summary>Why the game stands (docs/implementation/M9_network.md, 4.10, 4.12).</summary>
public enum PauseReason : byte
{
    /// <summary>A player pressed pause.</summary>
    Player = 1,

    /// <summary>The checksums differ.</summary>
    Desync = 2,
}

/// <summary>A client asks the host to pause or to go on (4.12): <c>i32 player | u8 pause</c>.</summary>
public sealed record PauseRequest(PlayerId Player, bool Pause) : NetMessage
{
    public override MessageType Type => MessageType.PauseRequest;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteBool(Pause);
    }

    internal static PauseRequest ReadBody(ref MessageReader reader) => new(reader.ReadPlayer(), reader.ReadBool());
}

/// <summary>The host pauses the game on a tick: <c>i32 player | u8 reason | i32 tick</c>.</summary>
public sealed record Pause(PlayerId Player, PauseReason Reason, int Tick) : NetMessage
{
    public override MessageType Type => MessageType.Pause;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteByte((byte)Reason);
        writer.WriteInt32(Tick);
    }

    internal static Pause ReadBody(ref MessageReader reader)
    {
        var player = reader.ReadPlayer();
        var reason = (PauseReason)reader.ReadByte();
        return Enum.IsDefined(reason)
            ? new Pause(player, reason, Checksum.ReadTick(ref reader))
            : throw MessageReader.Invalid($"Pause reason {(byte)reason}.");
    }
}

/// <summary>The host lets the game go on from a tick: <c>i32 player | i32 tick</c>, the player who took the pause off.</summary>
public sealed record Resume(PlayerId Player, int Tick) : NetMessage
{
    public override MessageType Type => MessageType.Resume;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteInt32(Tick);
    }

    internal static Resume ReadBody(ref MessageReader reader) => new(reader.ReadPlayer(), Checksum.ReadTick(ref reader));
}

/// <summary>Why a player is out of the match (4.11).</summary>
public enum LeaveReason : byte
{
    Quit = 1,
    Disconnected = 2,
    Timeout = 3,
    Kicked = 4,
}

/// <summary>
/// The host tells everyone a player has left: <c>i32 player | u8 reason | i32 tick</c>, the tick of the leaving. What it does to the
/// match is a command of the simulation on that tick (4.4), not this message.
/// </summary>
public sealed record PlayerLeft(PlayerId Player, LeaveReason Reason, int Tick) : NetMessage
{
    public override MessageType Type => MessageType.PlayerLeft;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Player);
        writer.WriteByte((byte)Reason);
        writer.WriteInt32(Tick);
    }

    internal static PlayerLeft ReadBody(ref MessageReader reader)
    {
        var player = reader.ReadPlayer();
        var reason = (LeaveReason)reader.ReadByte();
        return Enum.IsDefined(reason)
            ? new PlayerLeft(player, reason, Checksum.ReadTick(ref reader))
            : throw MessageReader.Invalid($"Leave reason {(byte)reason}.");
    }
}

/// <summary>
/// A player votes to drop another who keeps everyone waiting (4.11): <c>i32 voter | i32 target</c>. To the host only, which counts the
/// votes; never passed on.
/// </summary>
public sealed record KickVote(PlayerId Voter, PlayerId Target) : NetMessage
{
    public override MessageType Type => MessageType.KickVote;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WritePlayer(Voter);
        writer.WritePlayer(Target);
    }

    internal static KickVote ReadBody(ref MessageReader reader) => new(reader.ReadPlayer(), reader.ReadPlayer());
}

/// <summary>
/// A player gives up: <c>i32 player</c>. The defeat itself is the surrender command in the player's tick bundle (4.4); this message
/// only lets the others know at once.
/// </summary>
public sealed record Surrender(PlayerId Player) : NetMessage
{
    public override MessageType Type => MessageType.Surrender;

    internal override void WriteBody(MessageWriter writer) => writer.WritePlayer(Player);

    internal static Surrender ReadBody(ref MessageReader reader) => new(reader.ReadPlayer());
}
