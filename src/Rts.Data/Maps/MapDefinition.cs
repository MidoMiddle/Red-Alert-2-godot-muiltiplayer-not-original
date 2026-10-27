using System;
using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Data.Maps;

/// <summary>Where player <paramref name="Slot"/> (1…8) starts.</summary>
public sealed record MapStart(int Slot, CellPos Cell);

/// <summary>A code of the texture layer and the climate material it stands for (visual only).</summary>
public sealed record TextureEntry(char Code, string Material);

/// <summary>A map object (tree, rock, fence…) on a cell; <paramref name="Variant"/> picks the model, for the look only.</summary>
public sealed record MapObjectPlacement(string Type, CellPos Cell, int Variant);

/// <summary>A low bridge from <paramref name="From"/> to <paramref name="To"/>, both ends included, along X or along Y.</summary>
public sealed record BridgePlacement(string Type, CellPos From, CellPos To);

/// <summary>A building placed by the map; <paramref name="Owner"/> 0 is the neutral player, 1…8 a start slot; facing 0…7 in 45° steps.</summary>
public sealed record StructurePlacement(string Type, CellPos Cell, int Owner, int Facing);

/// <summary>
/// A map as read from a <c>.rtsmap</c> file (docs/implementation/M7_maps.md, 4.9): immutable, checked when built. Heights are
/// stored per cell corner, <c>(W+1) × (H+1)</c>, levels 0…14; land, texture and ore per cell, row by row from the north-west.
/// The climate, the lighting, the texture and the object variants are visual: the simulation never reads them, but the lobby
/// hash covers them.
/// </summary>
public sealed class MapDefinition
{
    public const string FormatVersion = "rtsmap/1";
    public const int MinSize = 8;
    public const int MaxSize = 512;
    public const int MaxHeight = 14;

    /// <summary>Ore per cell: 0 none, 1–9 ore density, 10–12 gems.</summary>
    public const int MaxOre = 12;

    public MapDefinition(
        string id,
        string name,
        string author,
        string description,
        int width,
        int height,
        string climate,
        int players,
        ImmutableArray<MapStart> starts,
        ImmutableArray<TextureEntry> textureLegend,
        ImmutableArray<byte> heights,
        ImmutableArray<LandType> land,
        ImmutableArray<char> texture,
        ImmutableArray<byte> ore,
        ImmutableArray<MapObjectPlacement> objects,
        ImmutableArray<BridgePlacement> bridges,
        ImmutableArray<StructurePlacement> structures,
        string lighting = MapLighting.Default,
        MapArea? playable = null,
        string origin = MapOrigin.Default)
    {
        Require(MapNames.IsId(id), $"id '{id}' must be lowercase letters, digits, '-' or '_'");
        Require(MapNames.IsText(name) && MapNames.IsText(author) && MapNames.IsText(description), "name, author and description are single lines");
        Require(width is >= MinSize and <= MaxSize && height is >= MinSize and <= MaxSize, $"size {width}x{height} is outside {MinSize}…{MaxSize}");
        Require(MapNames.IsId(climate), $"climate '{climate}' must be lowercase letters, digits, '-' or '_'");
        Require(MapOrigin.IsKnown(origin), $"origin '{origin}' is none of {string.Join(", ", MapOrigin.Ids)}");
        Require(MapLighting.IsKnown(lighting), $"lighting '{lighting}' is none of {string.Join(", ", MapLighting.Ids)}");
        Require(players is >= 1 and <= PlayerId.MaxPlayers, $"players {players} is outside 1…{PlayerId.MaxPlayers}");
        Require(heights.Length == (width + 1) * (height + 1) && heights.All(h => h <= MaxHeight), "heights must be (W+1)×(H+1) levels 0…14");
        Require(land.Length == width * height && land.All(LandSpeeds.IsKnown), "land must be W×H known land types");
        Require(!textureLegend.IsEmpty && textureLegend.Select(e => e.Code).Distinct().Count() == textureLegend.Length, "the texture legend needs codes, each once");
        Require(textureLegend.All(e => MapNames.IsLegendCode(e.Code) && MapNames.IsId(e.Material)), "legend codes are letters or digits, materials ids");
        Require(texture.Length == width * height && texture.All(c => textureLegend.Any(e => e.Code == c)), "texture must be W×H codes of the legend");
        Require(ore.Length == width * height && ore.All(o => o <= MaxOre), "ore must be W×H values 0…12");

        Id = id;
        Name = name;
        Author = author;
        Description = description;
        Width = width;
        Height = height;
        Climate = climate;
        Lighting = lighting;
        Origin = origin;
        Playable = playable;
        Players = players;
        Starts = starts.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        TextureLegend = textureLegend.Sort((a, b) => a.Code.CompareTo(b.Code));
        Heights = heights;
        Land = land;
        Texture = texture;
        Ore = ore;
        Objects = objects.Sort(ComparePlacements);
        Bridges = bridges.Sort(CompareBridges);
        Structures = structures.Sort(CompareStructures);

        Require(playable is null || (playable.Left <= playable.Right && playable.Top <= playable.Bottom && AreaCells() > 0),
            "the playable area needs at least one cell of the map");
        Require(Starts.Select(s => s.Slot).Distinct().Count() == Starts.Length && Starts.All(s => s.Slot >= 1 && s.Slot <= players && Contains(s.Cell)),
            "starts need slots 1…players, each once, on the map");
        Require(Objects.All(o => MapNames.IsId(o.Type) && Contains(o.Cell) && o.Variant is >= 0 and <= 255), "objects need an id type, a cell on the map, variant 0…255");
        Require(Bridges.All(b => MapNames.IsId(b.Type) && Contains(b.From) && Contains(b.To) && (b.From.X == b.To.X || b.From.Y == b.To.Y)),
            "bridges need an id type and both ends on the map, along X or Y");
        Require(Structures.All(s => MapNames.IsId(s.Type) && Contains(s.Cell) && s.Owner >= 0 && s.Owner <= players && s.Facing is >= 0 and <= 7),
            "structures need an id type, a cell on the map, owner neutral or a slot, facing 0…7");
    }

    public string Id { get; }

    public string Name { get; }

    public string Author { get; }

    public string Description { get; }

    public int Width { get; }

    public int Height { get; }

    public string Climate { get; }

    /// <summary>The time of day, one of <see cref="MapLighting.Ids"/>: visual only.</summary>
    public string Lighting { get; }

    /// <summary>Where the map comes from, one of <see cref="MapOrigin.Ids"/>: for the menus only.</summary>
    public string Origin { get; }

    /// <summary>The playable area, or null when every cell plays (<see cref="MapArea"/>).</summary>
    public MapArea? Playable { get; }

    public int Players { get; }

    /// <summary>In slot order.</summary>
    public ImmutableArray<MapStart> Starts { get; }

    /// <summary>In code order.</summary>
    public ImmutableArray<TextureEntry> TextureLegend { get; }

    public ImmutableArray<byte> Heights { get; }

    public ImmutableArray<LandType> Land { get; }

    public ImmutableArray<char> Texture { get; }

    public ImmutableArray<byte> Ore { get; }

    /// <summary>In (type, y, x, variant) order.</summary>
    public ImmutableArray<MapObjectPlacement> Objects { get; }

    /// <summary>In (type, from, to) order.</summary>
    public ImmutableArray<BridgePlacement> Bridges { get; }

    /// <summary>In (type, y, x, owner, facing) order.</summary>
    public ImmutableArray<StructurePlacement> Structures { get; }

    public bool Contains(CellPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    /// <summary>Height level of the corner at (<paramref name="x"/>, <paramref name="y"/>), 0 ≤ x ≤ W, 0 ≤ y ≤ H.</summary>
    public int CornerHeight(int x, int y) => Heights[y * (Width + 1) + x];

    public LandType LandAt(CellPos cell) => Land[cell.Y * Width + cell.X];

    /// <summary>True for a cell of the map that plays: inside <see cref="Playable"/>, or anywhere without it.</summary>
    public bool Plays(CellPos cell) => Contains(cell) && (Playable is null || Playable.Contains(cell));

    /// <summary>The obstacle layer of the rules: 1 on every cell outside <see cref="Playable"/>, so nothing goes or builds there.</summary>
    public byte[] BlockedLayer()
    {
        var blocked = new byte[Width * Height];
        if (Playable is { } area)
        {
            for (var i = 0; i < blocked.Length; i++)
            {
                blocked[i] = area.Contains(new CellPos(i % Width, i / Width)) ? (byte)0 : (byte)1;
            }
        }

        return blocked;
    }

    /// <summary>
    /// The map as the simulation plays it: corner heights and land as they are, the map objects (M7 step 5), the bridges (step 6) and
    /// the structures (step 7) by the kinds of the data <paramref name="types"/> (M3 step 14a: <c>GameRules.MapTypes</c>), at full hit
    /// points, in the order of <see cref="Objects"/>, <see cref="Bridges"/> and <see cref="Structures"/>. The texture stays out: it is
    /// visual. Throws on a kind the types do not define, on a bridge that does not run over water from shore to shore and on a building
    /// out of place.
    /// </summary>
    public MapGrid ToMapGrid(MapTypes types)
    {
        var land = new byte[Width * Height];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)Land[i];
        }

        var objects = new MapObjectState[Objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            var placement = Objects[i];
            if (!types.TryGetObject(placement.Type, out var type))
            {
                throw new ArgumentException($"Unknown map object '{placement.Type}' at {placement.Cell.X},{placement.Cell.Y}.");
            }

            objects[i] = MapObjectState.New(type, placement.Cell, placement.Variant);
        }

        var bridges = new BridgeState[Bridges.Length];
        for (var i = 0; i < bridges.Length; i++)
        {
            var placement = Bridges[i];
            if (!types.TryGetBridge(placement.Type, out var type))
            {
                throw new ArgumentException($"Unknown bridge '{placement.Type}' at {placement.From.X},{placement.From.Y}.");
            }

            bridges[i] = BridgeState.New(type, placement.From, placement.To);
        }

        var buildings = new BuildingState[Structures.Length];
        for (var i = 0; i < buildings.Length; i++)
        {
            var placement = Structures[i];
            // A kind of the map, or a building of a faction's data the map places for nobody (2026-10-27, as on the maps of RA2).
            buildings[i] = MapChecks.StateOf(placement, types)
                ?? throw new ArgumentException($"Unknown building '{placement.Type}' at {placement.Cell.X},{placement.Cell.Y}.");
        }

        var ore = new byte[Width * Height];
        for (var i = 0; i < ore.Length; i++)
        {
            ore[i] = OreOf(Ore[i]).Packed;
        }

        return MapGrid.FromLayers(Width, Height, land, BlockedLayer(), Heights.AsSpan(), objects, bridges, buildings, ore, types: types);
    }

    /// <summary>
    /// The ore of a cell of the map file in the match (M3 step 9): a density 1…9 of ore is spread over the 12 stages,
    /// <c>round((d − 1) × 11 / 8)</c>, so 1 is stage 0 and 9 is stage 11; gems A, B, C (10…12) are stages 3, 7 and 11.
    /// </summary>
    public static OreCell OreOf(byte value) => value switch
    {
        0 => OreCell.Empty,
        <= 9 => new OreCell(OreKind.Ore, (int)IntMath.DivRound((value - 1) * 11L, 8)),
        _ => new OreCell(OreKind.Gems, 4 * (value - 10) + 3),
    };

    /// <summary>Same content field by field (the canonical text of equal maps is equal too).</summary>
    public bool ContentEquals(MapDefinition other) =>
        Id == other.Id && Name == other.Name && Author == other.Author && Description == other.Description
        && Width == other.Width && Height == other.Height && Climate == other.Climate && Lighting == other.Lighting && Origin == other.Origin && Playable == other.Playable && Players == other.Players
        && Starts.SequenceEqual(other.Starts) && TextureLegend.SequenceEqual(other.TextureLegend)
        && Heights.SequenceEqual(other.Heights) && Land.SequenceEqual(other.Land) && Texture.SequenceEqual(other.Texture)
        && Ore.SequenceEqual(other.Ore) && Objects.SequenceEqual(other.Objects) && Bridges.SequenceEqual(other.Bridges)
        && Structures.SequenceEqual(other.Structures);

    private int AreaCells()
    {
        var count = 0;
        for (var i = 0; i < Width * Height; i++)
        {
            count += Playable!.Contains(new CellPos(i % Width, i / Width)) ? 1 : 0;
        }

        return count;
    }

    private static int ComparePlacements(MapObjectPlacement a, MapObjectPlacement b) =>
        Compare(string.CompareOrdinal(a.Type, b.Type), a.Cell.Y.CompareTo(b.Cell.Y), a.Cell.X.CompareTo(b.Cell.X), a.Variant.CompareTo(b.Variant));

    private static int CompareBridges(BridgePlacement a, BridgePlacement b) =>
        Compare(string.CompareOrdinal(a.Type, b.Type), a.From.Y.CompareTo(b.From.Y), a.From.X.CompareTo(b.From.X), a.To.Y.CompareTo(b.To.Y), a.To.X.CompareTo(b.To.X));

    private static int CompareStructures(StructurePlacement a, StructurePlacement b) =>
        Compare(string.CompareOrdinal(a.Type, b.Type), a.Cell.Y.CompareTo(b.Cell.Y), a.Cell.X.CompareTo(b.Cell.X), a.Owner.CompareTo(b.Owner), a.Facing.CompareTo(b.Facing));

    private static int Compare(params int[] keys)
    {
        foreach (var key in keys)
        {
            if (key != 0)
            {
                return key;
            }
        }

        return 0;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException($"Invalid map: {message}.");
        }
    }
}
