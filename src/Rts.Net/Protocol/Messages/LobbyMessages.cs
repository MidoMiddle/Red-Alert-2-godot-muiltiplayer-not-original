using System;
using System.Collections.Immutable;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Net.Protocol.Messages;

/// <summary>
/// One slot of the lobby (docs/implementation/M9_network.md, 4.4): who plays it and how, and whether they are ready.
/// <see cref="StartIndex"/> is <see cref="RandomStart"/> until the host draws the starts.
/// </summary>
public sealed record LobbySlot(
    int Slot, PlayerKind Kind, AiDifficulty Difficulty, string Name, string Faction, string Country, int Color, int Team, int StartIndex, bool Ready)
{
    public const int RandomStart = -1;

    /// <summary>
    /// <c>u8 slot | u8 kind | u8 difficulty | string name | string faction | string country | u8 colour | u8 team | u8 startIndex
    /// (255 = random) | u8 ready</c>.
    /// </summary>
    internal void Write(MessageWriter writer)
    {
        writer.WriteSmall(Slot, nameof(Slot));
        writer.WriteByte((byte)Kind);
        writer.WriteByte((byte)Difficulty);
        writer.WriteString(Name);
        writer.WriteString(Faction);
        writer.WriteString(Country);
        writer.WriteSmall(Color, nameof(Color));
        writer.WriteSmall(Team, nameof(Team));
        WriteStart(writer, StartIndex);
        writer.WriteBool(Ready);
    }

    internal static LobbySlot Read(ref MessageReader reader)
    {
        int slot = reader.ReadByte();
        if (slot is < 1 or > PlayerId.MaxPlayers)
        {
            throw MessageReader.Invalid($"Slot {slot}.");
        }

        return new LobbySlot(
            slot, MatchFields.ReadKind(ref reader), MatchFields.ReadDifficulty(ref reader), reader.ReadString(), reader.ReadString(),
            reader.ReadString(), reader.ReadByte(), reader.ReadByte(), ReadStart(ref reader), reader.ReadBool());
    }

    internal static void WriteStart(MessageWriter writer, int startIndex) =>
        writer.WriteSmall(startIndex == RandomStart ? byte.MaxValue : startIndex, nameof(StartIndex));

    internal static int ReadStart(ref MessageReader reader)
    {
        int start = reader.ReadByte();
        return start == byte.MaxValue ? RandomStart : start;
    }
}

/// <summary>
/// The lobby as the host sees it, sent to everyone on any change: <c>string mapId | u64 mapHash | options | u8 slotCount | slots</c>.
/// </summary>
public sealed record LobbyState(string MapId, ulong MapHash, MatchOptions Options, ImmutableArray<LobbySlot> Slots) : NetMessage
{
    public override MessageType Type => MessageType.LobbyState;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WriteString(MapId);
        writer.WriteUInt64(MapHash);
        MatchFields.WriteOptions(writer, Options);
        writer.WriteSmall(Slots.Length, nameof(Slots));
        foreach (var slot in Slots)
        {
            slot.Write(writer);
        }
    }

    internal static LobbyState ReadBody(ref MessageReader reader)
    {
        var mapId = reader.ReadString();
        var mapHash = reader.ReadUInt64();
        var options = MatchFields.ReadOptions(ref reader);
        int count = reader.ReadByte();
        if (count > PlayerId.MaxPlayers)
        {
            throw MessageReader.Invalid($"{count} slots, at most {PlayerId.MaxPlayers}.");
        }

        var slots = ImmutableArray.CreateBuilder<LobbySlot>(count);
        for (var i = 0; i < count; i++)
        {
            slots.Add(LobbySlot.Read(ref reader));
        }

        return new LobbyState(mapId, mapHash, options, slots.MoveToImmutable());
    }

    // Records compare ImmutableArray by reference; a message compares by content.
    public bool Equals(LobbyState? other) =>
        other is not null && MapId == other.MapId && MapHash == other.MapHash && Options == other.Options
        && Slots.AsSpan().SequenceEqual(other.Slots.AsSpan());

    public override int GetHashCode() => HashCode.Combine(MapId, MapHash, Slots.Length);
}

/// <summary>
/// A client changes its own slot: <c>string faction | string country | u8 colour | u8 team | u8 startIndex (255 = random) | u8 ready</c>.
/// The host checks it and answers with a new <see cref="LobbyState"/>.
/// </summary>
public sealed record LobbyChange(string Faction, string Country, int Color, int Team, int StartIndex, bool Ready) : NetMessage
{
    public override MessageType Type => MessageType.LobbyChange;

    internal override void WriteBody(MessageWriter writer)
    {
        writer.WriteString(Faction);
        writer.WriteString(Country);
        writer.WriteSmall(Color, nameof(Color));
        writer.WriteSmall(Team, nameof(Team));
        LobbySlot.WriteStart(writer, StartIndex);
        writer.WriteBool(Ready);
    }

    internal static LobbyChange ReadBody(ref MessageReader reader) =>
        new(reader.ReadString(), reader.ReadString(), reader.ReadByte(), reader.ReadByte(), LobbySlot.ReadStart(ref reader), reader.ReadBool());
}
