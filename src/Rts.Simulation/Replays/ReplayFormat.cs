using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;

namespace Rts.Simulation.Replays;

/// <summary>
/// Binary <c>.rtsreplay</c> format v17, little-endian (docs/implementation/M0_foundation.md, 4.10):
/// <code>
/// "RTSR" | u16 formatVersion | u32 simVersion | u64 dataHash
/// u64 seed | i32 ticksPerSecond | i32 mapWidth | i32 mapHeight | i32 hashInterval | i32 startingCredits | i32 tickCount
/// u8 hasMap | (u8 land × width·height | u8 obstacle × width·height) when hasMap = 1
/// u8 hasMatch | the match setup when hasMatch = 1
/// command batch (u32 count + commands, format of CommandSerializer)
/// u32 checksumCount | (i32 tick, u64 value) × checksumCount
/// </code>
/// History: v2 (M2 step 2) marks command format v2 inside; v3 (M2 step 4) adds the map layers; v4 (M2 step 12) marks command format v3 inside;
/// v5 (M7 step 2) adds the corner heights, (W+1) × (H+1) bytes after the cell layers; v6 (M7 step 5) adds the map objects after
/// them (u32 count, then u8 kind | u16 x | u16 y | u8 variant | i32 hp | u8 destroyed each) and marks command format v4;
/// v7 (M7 step 6) adds the bridges after the objects (u32 count, then u8 kind | u16 fromX | u16 fromY | u16 toX | u16 toY |
/// i32 hp per segment from the first end each); v8 (M7 step 7) adds the buildings of the start after the bridges (u32 count, then
/// u8 kind | u16 x | u16 y | u8 owner | u8 facing each); v9 (M3 step 3) adds the hash of the data files after the rules version
/// (<c>DataHasher</c>, docs/implementation/M3_base_economy.md, 4.4); v10 (M3 step 5) adds the starting credits of the players
/// after the hash interval; v11 (M3 step 6) marks command format v5 inside (PlaceBuilding, Deploy); v12 (M3 step 8) marks command
/// format v6 inside (production and rally points); v13 (M3 step 9) adds the ore layer, one byte per cell (kind × 16 + stage), after
/// the corner heights; v14 (M3 step 10) marks command format v7 inside (Harvest); v15 (M3 step 11) command format v8 (Sell,
/// ToggleRepair, PlaceWalls); v16 (M4 step 7) command format v9 (Attack, ForceAttack, SetStance); v17 (M5 step 2) adds the match
/// setup after the map: u16 length + UTF-8 map id | u8 shortGame | u8 gameSpeed | u8 playerCount, then per player u8 slot | u8 kind |
/// u8 difficulty | u16 length + UTF-8 faction | u8 colour | u8 team | u8 startIndex | u16 startX | u16 startY; its seed and credits are
/// those of the header. The commands of a bot are those of an Ai player numbered from <see cref="Ai.AiTurn.FirstSequence"/>; v18 (M5 step 3)
/// adds every start of the map after the players: u8 count, then u16 x | u16 y each; v19 (M5 step 10) command format v10 (Surrender);
/// v20 (M8 step 13) adds the personality of every player after its difficulty: u16 length + UTF-8 id, or <c>random</c>.
/// </summary>
public static class ReplayFormat
{
    public const ushort FormatVersion = 20;
    public const string FileExtension = ".rtsreplay";

    private static ReadOnlySpan<byte> Magic => "RTSR"u8;

    private const int HeaderSize = 4 + 2 + 4 + 8 + 8 + 4 * 6;
    private const int ChecksumSize = 4 + 8;

    // u8 kind | u16 x | u16 y | u8 variant | i32 hp | u8 destroyed.
    private const int ObjectSize = 1 + 2 + 2 + 1 + 4 + 1;

    // u8 kind | u16 fromX | u16 fromY | u16 toX | u16 toY, then i32 hp per segment.
    private const int BridgeHeaderSize = 1 + 2 * 4;

    // u8 kind | u16 x | u16 y | u8 owner | u8 facing.
    private const int BuildingSize = 1 + 2 + 2 + 1 + 1;

    // 512 × 512 cells: far above any map we plan, small enough to reject garbage sizes before allocating.
    private const long MaxMapCells = 512 * 512;

    public static byte[] Write(Replay replay)
    {
        var commands = CommandSerializer.WriteAll(replay.Commands);
        var map = replay.Settings.Map;
        var mapSize = 1 + (map is null ? 0 : 3 * map.Width * map.Height + (map.Width + 1) * (map.Height + 1) + 4 + ObjectSize * map.Objects.Count + 4);
        foreach (var bridge in map?.Bridges ?? [])
        {
            mapSize += BridgeHeaderSize + 4 * bridge.Segments.Count;
        }

        mapSize += map is null ? 0 : 4 + BuildingSize * map.StartBuildings.Count;

        var match = WriteMatch(replay.Settings.Match);
        var buffer = new byte[HeaderSize + mapSize + match.Length + commands.Length + 4 + ChecksumSize * replay.Checksums.Count];
        var span = buffer.AsSpan();

        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], FormatVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(span[6..], replay.SimVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(span[10..], replay.DataHash);
        BinaryPrimitives.WriteUInt64LittleEndian(span[18..], replay.Settings.Seed);
        BinaryPrimitives.WriteInt32LittleEndian(span[26..], replay.Settings.TicksPerSecond);
        BinaryPrimitives.WriteInt32LittleEndian(span[30..], replay.Settings.MapWidthCells);
        BinaryPrimitives.WriteInt32LittleEndian(span[34..], replay.Settings.MapHeightCells);
        BinaryPrimitives.WriteInt32LittleEndian(span[38..], replay.Settings.HashInterval);
        BinaryPrimitives.WriteInt32LittleEndian(span[42..], replay.Settings.StartingCredits);
        BinaryPrimitives.WriteInt32LittleEndian(span[46..], replay.TickCount);

        var offset = HeaderSize;
        span[offset++] = map is null ? (byte)0 : (byte)1;
        if (map is not null)
        {
            map.LandBytes().CopyTo(span[offset..]);
            offset += map.Width * map.Height;
            map.BlockedBytes().CopyTo(span[offset..]);
            offset += map.Width * map.Height;
            map.Heights.LevelBytes().CopyTo(span[offset..]);
            offset += (map.Width + 1) * (map.Height + 1);
            map.OreBytes().CopyTo(span[offset..]);
            offset += map.Width * map.Height;
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)map.Objects.Count);
            offset += 4;
            foreach (var mapObject in map.Objects)
            {
                span[offset] = (byte)mapObject.Kind;
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 1)..], (ushort)mapObject.Cell.X);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 3)..], (ushort)mapObject.Cell.Y);
                span[offset + 5] = (byte)mapObject.Variant;
                BinaryPrimitives.WriteInt32LittleEndian(span[(offset + 6)..], mapObject.Hp);
                span[offset + 10] = mapObject.Destroyed ? (byte)1 : (byte)0;
                offset += ObjectSize;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)map.Bridges.Count);
            offset += 4;
            foreach (var bridge in map.Bridges)
            {
                span[offset] = (byte)bridge.Kind;
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 1)..], (ushort)bridge.From.X);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 3)..], (ushort)bridge.From.Y);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 5)..], (ushort)bridge.To.X);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 7)..], (ushort)bridge.To.Y);
                offset += BridgeHeaderSize;
                foreach (var segment in bridge.Segments)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(span[offset..], segment.Hp);
                    offset += 4;
                }
            }

            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)map.StartBuildings.Count);
            offset += 4;
            foreach (var building in map.StartBuildings)
            {
                span[offset] = (byte)building.Kind;
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 1)..], (ushort)building.Cell.X);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(offset + 3)..], (ushort)building.Cell.Y);
                span[offset + 5] = (byte)building.Owner.Value;
                span[offset + 6] = (byte)building.Facing;
                offset += BuildingSize;
            }
        }

        match.CopyTo(span[offset..]);
        offset += match.Length;
        commands.CopyTo(span[offset..]);
        offset += commands.Length;

        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)replay.Checksums.Count);
        offset += 4;
        foreach (var checksum in replay.Checksums)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], checksum.Tick);
            BinaryPrimitives.WriteUInt64LittleEndian(span[(offset + 4)..], checksum.Value);
            offset += ChecksumSize;
        }

        return buffer;
    }

    public static Replay Read(ReadOnlySpan<byte> source)
    {
        Require(source, HeaderSize);
        if (!source[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a replay file: wrong magic bytes.");
        }

        var formatVersion = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        if (formatVersion != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported replay format version {formatVersion}, expected {FormatVersion}.");
        }

        var simVersion = BinaryPrimitives.ReadUInt32LittleEndian(source[6..]);
        var dataHash = BinaryPrimitives.ReadUInt64LittleEndian(source[10..]);
        var seed = BinaryPrimitives.ReadUInt64LittleEndian(source[18..]);
        var width = BinaryPrimitives.ReadInt32LittleEndian(source[30..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(source[34..]);
        var tickCount = BinaryPrimitives.ReadInt32LittleEndian(source[46..]);
        if (tickCount < 0)
        {
            throw new InvalidDataException($"Negative tick count {tickCount}.");
        }

        var offset = HeaderSize;
        Require(source[offset..], 1);
        var hasMap = source[offset++];
        if (hasMap > 1)
        {
            throw new InvalidDataException($"Invalid map flag {hasMap}.");
        }

        MapGrid? map = null;
        if (hasMap == 1)
        {
            if (width <= 0 || height <= 0 || (long)width * height > MaxMapCells)
            {
                throw new InvalidDataException($"Invalid map size {width}x{height}.");
            }

            var cells = width * height;
            var corners = (width + 1) * (height + 1);
            Require(source[offset..], 3 * cells + corners + 4);
            var objectCount = BinaryPrimitives.ReadUInt32LittleEndian(source[(offset + 3 * cells + corners)..]);
            if (objectCount > (uint)cells * 4)
            {
                throw new InvalidDataException($"Invalid map object count {objectCount}.");
            }

            var objectsStart = offset + 3 * cells + corners + 4;
            Require(source[objectsStart..], ObjectSize * (int)objectCount);
            var objects = new MapObjectState[objectCount];
            for (var i = 0; i < objects.Length; i++)
            {
                var at = source[(objectsStart + ObjectSize * i)..];
                if (at[10] > 1)
                {
                    throw new InvalidDataException($"Invalid destroyed flag {at[10]} of map object {i + 1}.");
                }

                objects[i] = new MapObjectState(
                    (MapObjectKind)at[0],
                    new CellPos(BinaryPrimitives.ReadUInt16LittleEndian(at[1..]), BinaryPrimitives.ReadUInt16LittleEndian(at[3..])),
                    at[5],
                    BinaryPrimitives.ReadInt32LittleEndian(at[6..]),
                    at[10] == 1);
            }

            var bridgesStart = objectsStart + ObjectSize * (int)objectCount;
            Require(source[bridgesStart..], 4);
            var bridgeCount = BinaryPrimitives.ReadUInt32LittleEndian(source[bridgesStart..]);
            if (bridgeCount > (uint)cells)
            {
                throw new InvalidDataException($"Invalid bridge count {bridgeCount}.");
            }

            var bridges = new BridgeState[bridgeCount];
            var next = bridgesStart + 4;
            for (var i = 0; i < bridges.Length; i++)
            {
                Require(source[next..], BridgeHeaderSize);
                var from = new CellPos(BinaryPrimitives.ReadUInt16LittleEndian(source[(next + 1)..]), BinaryPrimitives.ReadUInt16LittleEndian(source[(next + 3)..]));
                var to = new CellPos(BinaryPrimitives.ReadUInt16LittleEndian(source[(next + 5)..]), BinaryPrimitives.ReadUInt16LittleEndian(source[(next + 7)..]));
                var kind = (BridgeKind)source[next];
                next += BridgeHeaderSize;

                // Ends far off the map would make a huge segment count: FromLayers rejects them, but only after reading.
                var segments = from.X < width && to.X < width && from.Y < height && to.Y < height ? Bridge.Length(from, to) : 0;
                Require(source[next..], 4 * segments);
                var hp = new int[segments];
                for (var s = 0; s < segments; s++)
                {
                    hp[s] = BinaryPrimitives.ReadInt32LittleEndian(source[(next + 4 * s)..]);
                }

                next += 4 * segments;
                bridges[i] = new BridgeState(kind, from, to, System.Collections.Immutable.ImmutableArray.Create(hp));
            }

            Require(source[next..], 4);
            var buildingCount = BinaryPrimitives.ReadUInt32LittleEndian(source[next..]);
            if (buildingCount > (uint)cells)
            {
                throw new InvalidDataException($"Invalid building count {buildingCount}.");
            }

            next += 4;
            Require(source[next..], BuildingSize * (int)buildingCount);
            var buildings = new BuildingState[buildingCount];
            for (var i = 0; i < buildings.Length; i++)
            {
                var at = source[(next + BuildingSize * i)..];
                buildings[i] = new BuildingState(
                    (World.BuildingKind)at[0],
                    new CellPos(BinaryPrimitives.ReadUInt16LittleEndian(at[1..]), BinaryPrimitives.ReadUInt16LittleEndian(at[3..])),
                    new World.PlayerId(at[5]),
                    at[6]);
            }

            next += BuildingSize * (int)buildingCount;
            try
            {
                map = MapGrid.FromLayers(
                    width, height, source.Slice(offset, cells), source.Slice(offset + cells, cells), source.Slice(offset + 2 * cells, corners), objects, bridges,
                    buildings, source.Slice(offset + 2 * cells + corners, cells));
            }
            catch (ArgumentException error)
            {
                throw new InvalidDataException($"Invalid map data: {error.Message}", error);
            }

            offset = next;
        }

        var credits = BinaryPrimitives.ReadInt32LittleEndian(source[42..]);
        var setup = ReadMatch(source, ref offset, seed, credits);
        var settings = (map is null ? new SimulationSettings(seed) : new SimulationSettings(seed, map)) with
        {
            TicksPerSecond = BinaryPrimitives.ReadInt32LittleEndian(source[26..]),
            MapWidthCells = width,
            MapHeightCells = height,
            HashInterval = BinaryPrimitives.ReadInt32LittleEndian(source[38..]),
            StartingCredits = credits,
            Match = setup,
        };

        var commands = CommandSerializer.ReadBatch(source[offset..], out var commandBytes);
        offset += commandBytes;

        Require(source[offset..], 4);
        var checksumCount = BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
        offset += 4;
        if ((ulong)checksumCount * ChecksumSize != (ulong)(source.Length - offset))
        {
            throw new InvalidDataException(
                $"Checksum section has {source.Length - offset} bytes, expected {checksumCount} × {ChecksumSize}.");
        }

        var checksums = new List<StateChecksum>((int)checksumCount);
        for (var i = 0; i < checksumCount; i++)
        {
            checksums.Add(new StateChecksum(
                BinaryPrimitives.ReadInt32LittleEndian(source[offset..]),
                BinaryPrimitives.ReadUInt64LittleEndian(source[(offset + 4)..])));
            offset += ChecksumSize;
        }

        return new Replay(simVersion, dataHash, settings, tickCount, commands, checksums);
    }

    /// <summary>The match block: one zero byte without a setup.</summary>
    private static byte[] WriteMatch(MatchSetup? setup)
    {
        if (setup is null)
        {
            return [0];
        }

        var bytes = new List<byte> { 1 };
        AddText(bytes, setup.MapId);
        bytes.Add(setup.Options.ShortGame ? (byte)1 : (byte)0);
        bytes.Add((byte)setup.Options.GameSpeed);
        bytes.Add((byte)setup.Players.Length);
        foreach (var player in setup.Players)
        {
            bytes.Add((byte)player.Slot);
            bytes.Add((byte)player.Kind);
            bytes.Add((byte)player.Difficulty);
            AddText(bytes, player.Personality);
            AddText(bytes, player.Faction);
            bytes.Add((byte)player.Color);
            bytes.Add((byte)player.Team);
            bytes.Add((byte)player.StartIndex);
            AddUInt16(bytes, (ushort)player.Start.X);
            AddUInt16(bytes, (ushort)player.Start.Y);
        }

        bytes.Add((byte)setup.MapStarts.Length);
        foreach (var start in setup.MapStarts)
        {
            AddUInt16(bytes, (ushort)start.X);
            AddUInt16(bytes, (ushort)start.Y);
        }

        return bytes.ToArray();
    }

    private static MatchSetup? ReadMatch(ReadOnlySpan<byte> source, ref int offset, ulong seed, int credits)
    {
        Require(source[offset..], 1);
        var hasMatch = source[offset++];
        if (hasMatch > 1)
        {
            throw new InvalidDataException($"Invalid match flag {hasMatch}.");
        }

        if (hasMatch == 0)
        {
            return null;
        }

        var mapId = ReadText(source, ref offset);
        Require(source[offset..], 3);
        var shortGame = source[offset];
        var speed = source[offset + 1];
        var count = source[offset + 2];
        offset += 3;
        if (shortGame > 1)
        {
            throw new InvalidDataException($"Invalid short game flag {shortGame}.");
        }

        var players = System.Collections.Immutable.ImmutableArray.CreateBuilder<MatchPlayer>(count);
        for (var i = 0; i < count; i++)
        {
            Require(source[offset..], 3);
            var slot = source[offset];
            var kind = source[offset + 1];
            var difficulty = source[offset + 2];
            offset += 3;
            if (kind > (byte)PlayerKind.Ai || difficulty is < (byte)AiDifficulty.Medium or > (byte)AiDifficulty.Brutal)
            {
                throw new InvalidDataException($"Invalid kind {kind} or difficulty {difficulty} of player {i + 1}.");
            }

            var personality = ReadText(source, ref offset);
            var faction = ReadText(source, ref offset);
            Require(source[offset..], 7);
            players.Add(new MatchPlayer(
                slot, (PlayerKind)kind, (AiDifficulty)difficulty, faction, source[offset], source[offset + 1], source[offset + 2],
                new CellPos(BinaryPrimitives.ReadUInt16LittleEndian(source[(offset + 3)..]), BinaryPrimitives.ReadUInt16LittleEndian(source[(offset + 5)..])))
            {
                Personality = personality,
            });
            offset += 7;
        }

        Require(source[offset..], 1);
        var startCount = source[offset++];
        Require(source[offset..], 4 * startCount);
        var starts = System.Collections.Immutable.ImmutableArray.CreateBuilder<CellPos>(startCount);
        for (var i = 0; i < startCount; i++)
        {
            starts.Add(new CellPos(BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]), BinaryPrimitives.ReadUInt16LittleEndian(source[(offset + 2)..])));
            offset += 4;
        }

        return new MatchSetup(mapId, seed, new MatchOptions(credits, shortGame == 1, speed), players.MoveToImmutable(), starts.MoveToImmutable());
    }

    private static void AddText(List<byte> bytes, string text)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(text);
        AddUInt16(bytes, (ushort)utf8.Length);
        bytes.AddRange(utf8);
    }

    private static void AddUInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)value);
        bytes.Add((byte)(value >> 8));
    }

    private static string ReadText(ReadOnlySpan<byte> source, ref int offset)
    {
        Require(source[offset..], 2);
        var length = BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]);
        offset += 2;
        Require(source[offset..], length);
        var text = System.Text.Encoding.UTF8.GetString(source.Slice(offset, length));
        offset += length;
        return text;
    }

    private static void Require(ReadOnlySpan<byte> source, int length)
    {
        if (source.Length < length)
        {
            throw new InvalidDataException($"Unexpected end of replay data: need {length} bytes, have {source.Length}.");
        }
    }
}
