using System;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;

namespace Rts.Net.Protocol.Messages;

/// <summary>
/// The parts of a match that the lobby and the start share, laid out as in the replay (M5, 4.11): the options, the players, the
/// starts of the map.
/// </summary>
internal static class MatchFields
{
    /// <summary>
    /// <c>i32 startingCredits | u8 shortGame | u8 gameSpeed | u8 fogOfWar | u8 mcvRepacks | u8 superweapons | u8 destroyableBridges |
    /// u8 startingUnits | u8 crates</c> (the fog since protocol 2, the next three since 6, the units since 10, the crates since 11).
    /// </summary>
    public static void WriteOptions(MessageWriter writer, MatchOptions options)
    {
        writer.WriteInt32(options.StartingCredits);
        writer.WriteBool(options.ShortGame);
        writer.WriteSmall(options.GameSpeed, nameof(options.GameSpeed));
        writer.WriteBool(options.FogOfWar);
        writer.WriteBool(options.McvRepacks);
        writer.WriteBool(options.Superweapons);
        writer.WriteBool(options.DestroyableBridges);
        writer.WriteSmall(options.StartingUnits, nameof(options.StartingUnits));
        writer.WriteBool(options.Crates);
    }

    public static MatchOptions ReadOptions(ref MessageReader reader) =>
        new(reader.ReadInt32(), reader.ReadBool(), reader.ReadByte(), reader.ReadBool(), reader.ReadBool(), reader.ReadBool(), reader.ReadBool(), reader.ReadByte(), reader.ReadBool());

    /// <summary>
    /// <c>string mapId | u64 seed | options | u8 playerCount | players | u8 startCount | (i32 x | i32 y) × startCount</c>, a player being
    /// <c>u8 slot | u8 kind | u8 difficulty | string faction | string country | u8 colour | u8 team | u8 startIndex | i32 x | i32 y</c>.
    /// </summary>
    public static void WriteSetup(MessageWriter writer, MatchSetup setup)
    {
        writer.WriteString(setup.MapId);
        writer.WriteUInt64(setup.Seed);
        WriteOptions(writer, setup.Options);
        writer.WriteSmall(setup.Players.Length, nameof(setup.Players));
        foreach (var player in setup.Players)
        {
            writer.WriteSmall(player.Slot, nameof(player.Slot));
            writer.WriteByte((byte)player.Kind);
            writer.WriteByte((byte)player.Difficulty);
            writer.WriteString(player.Faction);
            writer.WriteString(player.Country);
            writer.WriteSmall(player.Color, nameof(player.Color));
            writer.WriteSmall(player.Team, nameof(player.Team));
            writer.WriteSmall(player.StartIndex, nameof(player.StartIndex));
            WriteCell(writer, player.Start);
        }

        writer.WriteSmall(setup.MapStarts.Length, nameof(setup.MapStarts));
        foreach (var start in setup.MapStarts)
        {
            WriteCell(writer, start);
        }
    }

    public static MatchSetup ReadSetup(ref MessageReader reader)
    {
        var mapId = reader.ReadString();
        var seed = reader.ReadUInt64();
        var options = ReadOptions(ref reader);
        int playerCount = reader.ReadByte();
        var players = ImmutableArray.CreateBuilder<MatchPlayer>(playerCount);
        for (var i = 0; i < playerCount; i++)
        {
            int slot = reader.ReadByte();
            var kind = ReadKind(ref reader);
            var difficulty = ReadDifficulty(ref reader);
            var faction = reader.ReadString();
            var country = reader.ReadString();
            int color = reader.ReadByte();
            int team = reader.ReadByte();
            int startIndex = reader.ReadByte();
            players.Add(new MatchPlayer(slot, kind, difficulty, faction, color, team, startIndex, ReadCell(ref reader)) { Country = country });
        }

        int startCount = reader.ReadByte();
        var starts = ImmutableArray.CreateBuilder<CellPos>(startCount);
        for (var i = 0; i < startCount; i++)
        {
            starts.Add(ReadCell(ref reader));
        }

        return new MatchSetup(mapId, seed, options, players.MoveToImmutable(), starts.MoveToImmutable());
    }

    public static PlayerKind ReadKind(ref MessageReader reader)
    {
        var kind = (PlayerKind)reader.ReadByte();
        return Enum.IsDefined(kind) ? kind : throw MessageReader.Invalid($"Player kind {(byte)kind}.");
    }

    public static AiDifficulty ReadDifficulty(ref MessageReader reader)
    {
        var difficulty = (AiDifficulty)reader.ReadByte();
        return Enum.IsDefined(difficulty) ? difficulty : throw MessageReader.Invalid($"Bot difficulty {(byte)difficulty}.");
    }

    private static void WriteCell(MessageWriter writer, CellPos cell)
    {
        writer.WriteInt32(cell.X);
        writer.WriteInt32(cell.Y);
    }

    private static CellPos ReadCell(ref MessageReader reader) => new(reader.ReadInt32(), reader.ReadInt32());
}
