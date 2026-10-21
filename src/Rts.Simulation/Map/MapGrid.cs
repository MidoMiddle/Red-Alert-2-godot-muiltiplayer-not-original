using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Map;

/// <summary>How a map object starts a match (or stands in a replay): ids follow the order of the list, from 1.</summary>
public readonly record struct MapObjectState(MapObjectKind Kind, CellPos Cell, int Variant, int Hp, bool Destroyed)
{
    /// <summary>A new object of <paramref name="kind"/> with full hit points.</summary>
    public static MapObjectState New(MapObjectKind kind, CellPos cell, int variant) => new(kind, cell, variant, MapObjectTypes.Get(kind).MaxHp, false);
}

/// <summary>
/// The map as the simulation sees it (docs/implementation/M2_unit_control.md, 4.3; M7_maps.md, 4.1–4.7): per cell a logical
/// <see cref="LandType"/> and a plain obstacle flag (tests), the corner heights, and the map objects (trees, boulders,
/// fences, walls, lamp posts). Structure of arrays indexed <c>y * Width + x</c>, cheap to hash and to scan. Part of the
/// simulation state: trees fall and fences are crushed during a match.
/// </summary>
public sealed class MapGrid
{
    private readonly LandType[] _land;
    private readonly bool[] _blocked;
    private readonly List<MapObject> _objects = new();
    private readonly Dictionary<int, List<MapObject>> _objectsByCell = new();
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
    public static MapGrid FromLayers(
        int width, int height, ReadOnlySpan<byte> land, ReadOnlySpan<byte> blocked, ReadOnlySpan<byte> heights = default,
        IReadOnlyList<MapObjectState>? objects = null)
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

        foreach (var state in objects ?? [])
        {
            if (!MapObjectTypes.IsKnown(state.Kind) || !grid.Contains(state.Cell) || state.Variant is < 0 or > 255
                || state.Hp < 0 || state.Hp > MapObjectTypes.Get(state.Kind).MaxHp)
            {
                throw new ArgumentException($"Map object {grid._objects.Count + 1} is not valid: {state}.");
            }

            var mapObject = new MapObject(grid._objects.Count + 1, state.Kind, state.Cell, state.Variant, state.Hp, state.Destroyed);
            grid._objects.Add(mapObject);
            var index = grid.IndexOf(state.Cell);
            if (!grid._objectsByCell.TryGetValue(index, out var inCell))
            {
                inCell = new List<MapObject>();
                grid._objectsByCell.Add(index, inCell);
            }

            inCell.Add(mapObject);
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

    /// <summary>True when something on the cell stops at least one kind of mover (a tree, boulder, fence, wall, or the plain flag).</summary>
    public bool IsBlocked(CellPos cell) => ObstacleMask(cell) != 0;

    /// <summary>The movers stopped on the cell, one bit per <see cref="Locomotor"/>: the plain flag stops all, objects their own.</summary>
    public byte ObstacleMask(CellPos cell)
    {
        var index = IndexOf(cell);
        var mask = _blocked[index] ? MapObjectTypes.AllMovers : (byte)0;
        if (_objectsByCell.TryGetValue(index, out var inCell))
        {
            foreach (var mapObject in inCell)
            {
                mask |= mapObject.IsAlive ? mapObject.Type.BlockMask : (byte)0;
            }
        }

        return mask;
    }

    /// <summary>Every map object, destroyed ones (stumps and the like) included, in id order.</summary>
    public IReadOnlyList<MapObject> Objects => _objects;

    /// <summary>The objects on a cell, in id order; empty when there are none.</summary>
    public IReadOnlyList<MapObject> ObjectsAt(CellPos cell) =>
        _objectsByCell.TryGetValue(IndexOf(cell), out var inCell) ? inCell : [];

    /// <summary>The objects as they stand now, for a copy or a replay.</summary>
    public MapObjectState[] ObjectStates()
    {
        var states = new MapObjectState[_objects.Count];
        for (var i = 0; i < states.Length; i++)
        {
            var o = _objects[i];
            states[i] = new MapObjectState(o.Kind, o.Cell, o.Variant, o.Hp, o.Destroyed);
        }

        return states;
    }

    /// <summary>
    /// Speed multiplier in percent for <paramref name="locomotor"/> on the cell, slopes included; 0 when it cannot enter
    /// (outside the map, a cliff, impassable land, an obstacle). See <see cref="LocomotorRules.CellPercent"/>.
    /// </summary>
    public int SpeedPercent(CellPos cell, Locomotor locomotor) =>
        Contains(cell) ? _passability.Percent(cell.Y * Width + cell.X, locomotor) : 0;

    /// <summary>True when a unit with <paramref name="locomotor"/> may stand on the cell: inside the map, not a cliff, passable land, no obstacle.</summary>
    public bool CanEnter(CellPos cell, Locomotor locomotor) => SpeedPercent(cell, locomotor) > 0;

    /// <summary>An independent copy: the match changes its own map, never the one in the settings.</summary>
    public MapGrid Clone() => FromLayers(Width, Height, LandBytes(), BlockedBytes(), _heights.LevelBytes(), ObjectStates());

    /// <summary>True when both maps have the same size and the same cells.</summary>
    public bool ContentEquals(MapGrid other) =>
        Width == other.Width
        && Height == other.Height
        && _land.AsSpan().SequenceEqual(other._land)
        && _blocked.AsSpan().SequenceEqual(other._blocked)
        && _heights.ContentEquals(other._heights)
        && ObjectsEqual(other);

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

    /// <summary>The plain obstacle flags as bytes (0 or 1) in cell index order, the replay layout; map objects are listed apart.</summary>
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

    /// <summary>Takes hit points off an object without destroying it (the hash sees it; nothing else changes).</summary>
    internal void SetObjectHp(int id, int hp)
    {
        _objects[id - 1].Hp = hp;
        _digest = null;
    }

    /// <summary>Destroys an object: a tree becomes a stump, others stop counting; the cell opens to whoever it blocked.</summary>
    internal void DestroyObject(int id)
    {
        var mapObject = _objects[id - 1];
        mapObject.Hp = 0;
        mapObject.Destroyed = true;
        _passability.Update(this, mapObject.Cell);
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

    /// <summary>
    /// Grows whenever a cell changes, so caches built from the map (step costs of A*, the client's map objects) know they are stale.
    /// Not part of the state: two equal maps may have different versions.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// FNV-1a of the land layer, the obstacle layer, the corner heights and the map objects, in index order, for the state hash. Computed once and kept
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

        // Rules v16 (M7 step 5): the map objects, in id order.
        hash.Add(_objects.Count);
        foreach (var o in _objects)
        {
            hash.Add(o.Id);
            hash.Add((byte)o.Kind);
            hash.Add(o.Cell.X);
            hash.Add(o.Cell.Y);
            hash.Add(o.Variant);
            hash.Add(o.Hp);
            hash.Add(o.Destroyed);
        }

        return hash.Value;
    }

    private bool ObjectsEqual(MapGrid other)
    {
        if (_objects.Count != other._objects.Count)
        {
            return false;
        }

        for (var i = 0; i < _objects.Count; i++)
        {
            if (!_objects[i].ContentEquals(other._objects[i]))
            {
                return false;
            }
        }

        return true;
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
