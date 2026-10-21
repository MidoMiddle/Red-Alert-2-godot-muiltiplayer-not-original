using System;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Map;

/// <summary>
/// The map as the simulation sees it (docs/implementation/M2_unit_control.md, 4.3): per cell a logical
/// <see cref="LandType"/> and a static obstacle flag (a tree now; rocks, fences and other map objects later).
/// Structure of arrays indexed <c>y * Width + x</c>, cheap to hash and to scan. Part of the simulation state:
/// trees and bridges will be destroyed during a match.
/// </summary>
public sealed class MapGrid
{
    private readonly LandType[] _land;
    private readonly bool[] _blocked;
    private CornerHeightGrid _heights;
    private Passability _passability;
    private ulong? _digest;

    /// <summary>A map of <paramref name="width"/> × <paramref name="height"/> clear cells.</summary>
    public MapGrid(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Map size must be positive, got {width}x{height}.");
        }

        Width = width;
        Height = height;
        _land = new LandType[width * height];
        Array.Fill(_land, LandType.Clear);
        _blocked = new bool[width * height];
        _heights = new CornerHeightGrid(width, height);
        _passability = new Passability(this);
    }

    /// <summary>
    /// A map from raw layers, one byte per cell in index order: land type values and obstacle flags (0 or 1), and the corner
    /// levels (<c>(W+1) × (H+1)</c>; an empty layer means flat ground). Used by the replay reader and the map loader; throws on
    /// unknown land types, flags or levels.
    /// </summary>
    public static MapGrid FromLayers(int width, int height, ReadOnlySpan<byte> land, ReadOnlySpan<byte> blocked, ReadOnlySpan<byte> heights = default)
    {
        var grid = new MapGrid(width, height);
        if (land.Length != grid._land.Length || blocked.Length != grid._blocked.Length)
        {
            throw new ArgumentException($"Layers must have {grid._land.Length} cells, got {land.Length} and {blocked.Length}.");
        }

        for (var i = 0; i < land.Length; i++)
        {
            var type = (LandType)land[i];
            if (!LocomotorRules.IsKnown(type) || blocked[i] > 1)
            {
                throw new ArgumentException($"Cell {i} has an unknown land type {land[i]} or obstacle flag {blocked[i]}.");
            }

            grid._land[i] = type;
            grid._blocked[i] = blocked[i] == 1;
        }

        if (!heights.IsEmpty)
        {
            grid._heights = CornerHeightGrid.FromLayer(width, height, heights);
        }

        grid._passability = new Passability(grid);
        grid._digest = null;
        grid.Version++;
        return grid;
    }

    public int Width { get; }

    public int Height { get; }

    public bool Contains(CellPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    public LandType LandAt(CellPos cell) => _land[IndexOf(cell)];

    /// <summary>The corner heights (docs/implementation/M7_maps.md, 4.1). Read only from outside; the match changes them through the map.</summary>
    public CornerHeightGrid Heights => _heights;

    /// <summary>True when a static obstacle (a tree) stands on the cell.</summary>
    public bool IsBlocked(CellPos cell) => _blocked[IndexOf(cell)];

    /// <summary>
    /// Speed multiplier in percent for <paramref name="locomotor"/> on the cell, slopes included; 0 when it cannot enter
    /// (outside the map, a cliff, impassable land, an obstacle). See <see cref="LocomotorRules.CellPercent"/>.
    /// </summary>
    public int SpeedPercent(CellPos cell, Locomotor locomotor) =>
        Contains(cell) ? _passability.Percent(cell.Y * Width + cell.X, locomotor) : 0;

    /// <summary>True when a unit with <paramref name="locomotor"/> may stand on the cell: inside the map, not a cliff, passable land, no obstacle.</summary>
    public bool CanEnter(CellPos cell, Locomotor locomotor) => SpeedPercent(cell, locomotor) > 0;

    /// <summary>An independent copy: the match changes its own map, never the one in the settings.</summary>
    public MapGrid Clone() => FromLayers(Width, Height, LandBytes(), BlockedBytes(), _heights.LevelBytes());

    /// <summary>True when both maps have the same size and the same cells.</summary>
    public bool ContentEquals(MapGrid other) =>
        Width == other.Width
        && Height == other.Height
        && _land.AsSpan().SequenceEqual(other._land)
        && _blocked.AsSpan().SequenceEqual(other._blocked)
        && _heights.ContentEquals(other._heights);

    /// <summary>Land layer as bytes in cell index order, the replay layout.</summary>
    public byte[] LandBytes()
    {
        var bytes = new byte[_land.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)_land[i];
        }

        return bytes;
    }

    /// <summary>Obstacle layer as bytes (0 or 1) in cell index order, the replay layout.</summary>
    public byte[] BlockedBytes()
    {
        var bytes = new byte[_blocked.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = _blocked[i] ? (byte)1 : (byte)0;
        }

        return bytes;
    }

    internal void SetLand(CellPos cell, LandType land)
    {
        if (!LocomotorRules.IsKnown(land))
        {
            throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type.");
        }

        _land[IndexOf(cell)] = land;
        _passability.Update(this, cell);
        _digest = null;
        Version++;
    }

    internal void SetBlocked(CellPos cell, bool blocked)
    {
        _blocked[IndexOf(cell)] = blocked;
        _passability.Update(this, cell);
        _digest = null;
        Version++;
    }

    /// <summary>Sets the level of one corner (tests now; destructible relief later).</summary>
    internal void SetCornerLevel(int x, int y, int level)
    {
        _heights.SetCornerLevel(x, y, level);

        // A corner belongs to up to four cells.
        for (var cy = y - 1; cy <= y; cy++)
        {
            for (var cx = x - 1; cx <= x; cx++)
            {
                if (Contains(new CellPos(cx, cy)))
                {
                    _passability.Update(this, new CellPos(cx, cy));
                }
            }
        }

        _digest = null;
        Version++;
    }

    /// <summary>Grows whenever a cell changes, so caches built from the map (step costs of A*) know they are stale.</summary>
    internal int Version { get; private set; }

    /// <summary>
    /// FNV-1a of the land layer, the obstacle layer and the corner heights, in index order, for the state hash. Computed once and kept
    /// until a cell changes, so the per-tick hash does not walk the whole map (docs/implementation/M2_unit_control.md, 4.15).
    /// </summary>
    internal ulong Digest => _digest ??= ComputeDigest();

    /// <summary>Land of every cell in index order, for the state hash.</summary>
    internal ReadOnlySpan<LandType> LandCells => _land;

    /// <summary>Obstacle flag of every cell in index order, for the state hash.</summary>
    internal ReadOnlySpan<bool> BlockedCells => _blocked;

    private ulong ComputeDigest()
    {
        var hash = new Fnv1a64();
        foreach (var land in _land)
        {
            hash.Add((byte)land);
        }

        foreach (var blocked in _blocked)
        {
            hash.Add(blocked);
        }

        // Rules v13 (M7 step 2): the corner heights follow the cell layers.
        hash.Add(_heights.Levels);

        return hash.Value;
    }

    private int IndexOf(CellPos cell)
    {
        if (!Contains(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, $"Cell is outside the {Width}x{Height} map.");
        }

        return cell.Y * Width + cell.X;
    }
}
