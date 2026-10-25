using System;
using Rts.Simulation.World;

namespace Rts.Net.Protocol.Messages;

/// <summary>
/// The first message of a client to the host or the relay (docs/implementation/M9_network.md, 4.4, 4.8):
/// <c>string gameVersion | u32 simVersion | u64 dataHash | string playerName | string roomCode</c>. The room code is empty in the
/// direct mode.
/// </summary>
public sealed record Hello(string GameVersion, uint SimVersion, ulong DataHash, string PlayerName, string RoomCode) : NetMessage
{
    public override MessageType Type => MessageType.Hello;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WriteString(GameVersion);
        writer.WriteUInt32(SimVersion);
        writer.WriteUInt64(DataHash);
        writer.WriteString(PlayerName);
        writer.WriteString(RoomCode);
    }

    internal static Hello ReadBody(ref MessageReader reader) =>
        new(reader.ReadString(), reader.ReadUInt32(), reader.ReadUInt64(), reader.ReadString(), reader.ReadString());
}

/// <summary>The host takes the client: <c>u8 playerSlot</c>, 1…8.</summary>
public sealed record Welcome(int PlayerSlot) : NetMessage
{
    public override MessageType Type => MessageType.Welcome;

    internal override void WriteBody(MessageWriter writer) => writer.WriteSmall(PlayerSlot, nameof(PlayerSlot));

    internal static Welcome ReadBody(ref MessageReader reader)
    {
        int slot = reader.ReadByte();
        return slot is >= 1 and <= PlayerId.MaxPlayers ? new Welcome(slot) : throw MessageReader.Invalid($"Slot {slot}.");
    }
}

/// <summary>Why the host refuses a client (docs/implementation/M9_network.md, 4.8): each check has its own code.</summary>
public enum RejectReason : byte
{
    ProtocolVersion = 1,
    GameVersion = 2,
    SimVersion = 3,
    DataHash = 4,
    MapHash = 5,
    RoomNotFound = 6,
    RoomFull = 7,
    GameInProgress = 8,
    LoadTimeout = 9,

    /// <summary>The relay takes no more rooms: of this address, or at all.</summary>
    ServerBusy = 10,
}

/// <summary>The host refuses the client: <c>u8 reason | string detail</c>; the connection is closed after it.</summary>
public sealed record Reject(RejectReason Reason, string Detail) : NetMessage
{
    public override MessageType Type => MessageType.Reject;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WriteByte((byte)Reason);
        writer.WriteString(Detail);
    }

    internal static Reject ReadBody(ref MessageReader reader)
    {
        var reason = (RejectReason)reader.ReadByte();
        return Enum.IsDefined(reason) ? new Reject(reason, reader.ReadString()) : throw MessageReader.Invalid($"Reject reason {(byte)reason}.");
    }
}
