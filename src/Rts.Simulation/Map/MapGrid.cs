using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Math;

namespace Rts.Simulation.Map;

/// <summary>How a map object starts a match (or stands in a replay): ids follow the order of the list, from 1.</summary>
public readonly record struct MapObjectState(MapObjectKind Kind, CellPos Cell, int Variant, int Hp, bool Destroyed)
{
    /// <summary>A new object of the kind <paramref name="type"/> with full hit points.</summary>
    public static MapObjectState New(MapObjectType type, CellPos cell, int variant) => new(type.Kind, cell, variant, type.MaxHp, false);
}

/// <summary>
/// A building the map places at the start of a match (M7 step 7): north-west cell, owner (0 neutral), facing 0…7 — of a kind of the map,
/// or, with kind 0, a building of a faction's data named by <paramref name="Def"/> with its footprint (<paramref name="Width"/> ×
/// <paramref name="Height"/>), as RA2's maps place the Neutral house's defenses and tech (2026-10-27). The footprint is the data's,
/// written here so the map needs no rules to stand; the match checks it against its rules (<see cref="World.GameWorld"/>).
/// </summary>
public sealed record BuildingState(World.BuildingKind Kind, CellPos Cell, World.PlayerId Owner, int Facing, string? Def = null, int Width = 0, int Height = 0)
{
    /// <summary>A building of a faction's data, neutral, at <paramref name="cell"/>.</summary>
    public static BuildingState OfData(string def, Rules.CellSize size, CellPos cell, int facing) =>
        new(0, cell, World.PlayerId.Neutral, facing, def, size.Width, size.Height);

    /// <summary>Whether it is a building of a faction's data rather than of a kind of the map.</summary>
    public bool IsOfData => Kind == 0;
}

/// <summary>
/// The map as the simulation sees it (docs/implementation/M2_unit_control.md, 4.3; M7_maps.md, 4.1–4.7): per cell a logical
/// <see cref="LandType"/> and a plain obstacle flag (tests), the corner heights, the map objects (trees, boulders,
/// fences, walls, lamp posts), the low bridges, and the buildings the map places with the cells they take. Structure of arrays
/// indexed <c>y * Width + x</c>, cheap to hash and to scan. Part of the simulation state: trees fall, fences are crushed,
/// bridges are blown up and buildings destroyed during a match.
/// </summary>
public sealed class MapGrid
{
    private readonly LandType[] _land;
    private readonly bool[] _blocked;
    private readonly List<MapObject> _objects = new();
    private readonly Dictionary<int, List<MapObject>> _objectsByCell = new();
    private readonly List<MapObject> _sources = new();
    private readonly List<Bridge> _bridges = new();
    private readonly Dictionary<int, BridgeSegment> _segmentsByCell = new();

    // The decks of the high bridges (2026-10-26), derived from the bridges as they are added: a slot per deck cell in the order of
    // the bridges and their segments; the cell, the segment and the level of every slot; for every end cell the shore it leads to,
    // and for every shore cell the end cells it leads onto. Empty without high bridges.
    private readonly List<int> _deckCell = new();
    private readonly List<BridgeSegment> _deckSegment = new();
    private readonly List<int> _deckLevel = new();
    private readonly List<int> _deckExit = new();
    private readonly Dictionary<int, int> _deckSlotByCell = new();
    private readonly Dictionary<int, List<int>> _deckEntries = new();
    private readonly List<BuildingState> _startBuildings = new();
    private readonly bool[] _building;

    // The cells of buildings left open to drive on (RA2: a refinery's dock is in its footprint): taken for building, open for movers.
    private readonly SortedSet<int> _docks = [];
    private readonly byte[] _ore;
    private CornerHeightGrid _heights;
    private Passability _passability;
    private ulong? _digest;
    private int[]? _highwaySums;

    /// <summary>
    /// A map of <paramref name="width"/> × <paramref name="height"/> clear cells. <paramref name="speeds"/> are the land speeds of
    /// the match; a map outside a match goes by <see cref="LandSpeeds.Reference"/>. <paramref name="types"/> are the kinds of map
    /// objects, bridges and buildings of the data (<see cref="MapTypes"/>; none without them).
    /// </summary>
    public MapGrid(int width, int height, LandSpeeds? speeds = null, MapTypes? types = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Map size must be positive, got {width}x{height}.");
        }

        Width = width;
        Height = height;
        Speeds = speeds ?? LandSpeeds.Reference;
        Types = types ?? MapTypes.Empty;
        _land = new LandType[width * height];
        Array.Fill(_land, LandType.Clear);
        _blocked = new bool[width * height];
        _building = new bool[width * height];
        _ore = new byte[width * height];
        _heights = new CornerHeightGrid(width, height);
        _passability = new Passability(this);
    }

    /// <summary>
    /// A map from raw layers, one byte per cell in index order: land type values and obstacle flags (0 or 1), and the corner
    /// levels (<c>(W+1) × (H+1)</c>; an empty layer means flat ground), then the map objects, the bridges and the buildings of the
    /// start. Used by the replay reader and the map loader; throws on unknown land types, flags or levels, on a bridge that does
    /// not run over water from shore to shore (see <see cref="TryAddBridge"/>) and on a building out of place (see
    /// <see cref="TryAddBuilding"/>). <paramref name="ore"/> is one byte per cell (<see cref="OreCell.Packed"/>; empty: no ore).
    /// <paramref name="speeds"/> and <paramref name="types"/> as in the constructor; an object, bridge or building of a kind the types do
    /// not define is refused.
    /// </summary>
    public static MapGrid FromLayers(
        int width, int height, ReadOnlySpan<byte> land, ReadOnlySpan<byte> blocked, ReadOnlySpan<byte> heights = default,
        IReadOnlyList<MapObjectState>? objects = null, IReadOnlyList<BridgeState>? bridges = null, IReadOnlyList<BuildingState>? buildings = null,
        ReadOnlySpan<byte> ore = default, LandSpeeds? speeds = null, MapTypes? types = null)
    {
        var grid = new MapGrid(width, height, speeds, types);
        if (land.Length != grid._land.Length || blocked.Length != grid._blocked.Length)
        {
            throw new ArgumentException($"Layers must have {grid._land.Length} cells, got {land.Length} and {blocked.Length}.");
        }

        for (var i = 0; i < land.Length; i++)
        {
            var type = (LandType)land[i];
            if (!LandSpeeds.IsKnown(type) || blocked[i] > 1)
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
            if (!grid.Types.IsKnown(state.Kind))
            {
                throw new ArgumentException($"Map object {grid._objects.Count + 1} is of the kind {(int)state.Kind}, which the map types do not define.");
            }

            if (!grid.Contains(state.Cell) || state.Variant is < 0 or > 255 || state.Hp < 0 || state.Hp > grid.Types.Get(state.Kind).MaxHp)
            {
                throw new ArgumentException($"Map object {grid._objects.Count + 1} is not valid: {state}.");
            }

            var mapObject = new MapObject(grid._objects.Count + 1, grid.Types.Get(state.Kind), state.Cell, state.Variant, state.Hp, state.Destroyed);
            grid._objects.Add(mapObject);
            if (state.Kind is MapObjectKind.OreSource or MapObjectKind.GemSource)
            {
                grid._sources.Add(mapObject);
            }

            var index = grid.IndexOf(state.Cell);
            if (!grid._objectsByCell.TryGetValue(index, out var inCell))
            {
                inCell = new List<MapObject>();
                grid._objectsByCell.Add(index, inCell);
            }

            inCell.Add(mapObject);
        }

        foreach (var state in bridges ?? [])
        {
            if (grid.TryAddBridge(state) is { } problem)
            {
                throw new ArgumentException($"Bridge {grid._bridges.Count + 1} is not valid: {problem}.");
            }
        }

        if (!ore.IsEmpty)
        {
            if (ore.Length != grid._ore.Length)
            {
                throw new ArgumentException($"The ore layer must have {grid._ore.Length} cells, got {ore.Length}.");
            }

            for (var i = 0; i < ore.Length; i++)
            {
                grid._ore[i] = OreCell.IsValidPacked(ore[i]) ? ore[i] : throw new ArgumentException($"Cell {i} has an unknown ore byte {ore[i]}.");
            }
        }

        foreach (var state in buildings ?? [])
        {
            if (grid.TryAddBuilding(state) is { } problem)
            {
                throw new ArgumentException($"Building {grid._startBuildings.Count + 1} is not valid: {problem}.");
            }
        }

        grid._passability = new Passability(grid);
        grid._digest = null;
        grid.Version++;
        return grid;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>
    /// How fast each locomotor goes on each land type of this map: the rules of the match (<see cref="Rules.GameRules.Land"/>), or
    /// <see cref="LandSpeeds.Reference"/> for a map outside a match. Rules, not map content: not in the digest, the replay or
    /// <see cref="ContentEquals"/>.
    /// </summary>
    public LandSpeeds Speeds { get; }

    /// <summary>
    /// The kinds of map objects, bridges and buildings of this map: those of the data of the match (<see cref="Rules.GameRules.MapTypes"/>),
    /// or the ones a map outside a match was read with. Rules, not map content: not in the digest, the replay or <see cref="ContentEquals"/>.
    /// </summary>
    public MapTypes Types { get; }

    public bool Contains(CellPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    /// <summary>The land layer: under a bridge, the water it spans.</summary>
    public LandType LandAt(CellPos cell) => _land[IndexOf(cell)];

    /// <summary>
    /// Whether a cell of highway lies in the rectangle of <paramref name="a"/> and <paramref name="b"/> grown by <paramref name="margin"/>
    /// cells: the path search counts the highway in its heuristic only then (<see cref="LandSpeeds.MinStepCosts"/>). A table of sums of
    /// the highway cells, built once and again after the land changes, answers it in four reads.
    /// </summary>
    public bool HighwayNear(CellPos a, CellPos b, int margin)
    {
        var sums = _highwaySums ??= HighwaySums();
        var x0 = IntMath.Max(0, IntMath.Min(a.X, b.X) - margin);
        var y0 = IntMath.Max(0, IntMath.Min(a.Y, b.Y) - margin);
        var x1 = IntMath.Min(Width, IntMath.Max(a.X, b.X) + margin + 1);
        var y1 = IntMath.Min(Height, IntMath.Max(a.Y, b.Y) + margin + 1);
        if (x0 >= x1 || y0 >= y1)
        {
            return false;
        }

        var row = Width + 1;
        return sums[y1 * row + x1] - sums[y0 * row + x1] - sums[y1 * row + x0] + sums[y0 * row + x0] > 0;
    }

    // sums[(y + 1) * (Width + 1) + x + 1]: the highway cells with both coordinates up to x and y.
    private int[] HighwaySums()
    {
        var row = Width + 1;
        var sums = new int[row * (Height + 1)];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var here = _land[y * Width + x] == LandType.Highway ? 1 : 0;
                sums[(y + 1) * row + x + 1] = here + sums[y * row + x + 1] + sums[(y + 1) * row + x] - sums[y * row + x];
            }
        }

        return sums;
    }

    /// <summary>
    /// The land a unit on the ground drives on (M7 step 6): a road over a standing low bridge segment, the land layer elsewhere —
    /// under a high bridge too, whose deck is a level of its own (<see cref="Decks"/>).
    /// </summary>
    public LandType SurfaceAt(CellPos cell)
    {
        var index = IndexOf(cell);
        return _segmentsByCell.TryGetValue(index, out var segment) && segment.IsIntact && !segment.Bridge.Type.High ? LandType.Road : _land[index];
    }

    /// <summary>Cells of deck of the high bridges: the second level of the map (<see cref="Decks"/>).</summary>
    public int DeckCount => _deckCell.Count;

    /// <summary>Nodes of the path graph: a node per cell, then one per cell of deck.</summary>
    public int NodeCount => Width * Height + _deckCell.Count;

    /// <summary>The slot of the deck over the cell, or -1 when no high bridge crosses it.</summary>
    public int DeckSlot(CellPos cell) => Contains(cell) && _deckSlotByCell.TryGetValue(IndexOf(cell), out var slot) ? slot : -1;

    public bool IsDeck(CellPos cell) => DeckSlot(cell) >= 0;

    /// <summary>The cell of a slot of deck.</summary>
    public CellPos DeckCell(int slot) => new(_deckCell[slot] % Width, _deckCell[slot] / Width);

    /// <summary>The segment a slot of deck belongs to.</summary>
    public BridgeSegment DeckSegment(int slot) => _deckSegment[slot];

    /// <summary>The level of the deck of a slot: that of the plateaus its bridge joins.</summary>
    public int DeckLevel(int slot) => _deckLevel[slot];

    /// <summary>The level of the deck over the cell (the cell must be a deck cell).</summary>
    public int DeckLevel(CellPos cell) => _deckLevel[DeckSlot(cell)];

    /// <summary>The shore cell an end cell of deck leads off to, as a cell index, or -1 for a slot inside the deck.</summary>
    public int DeckExit(int slot) => _deckExit[slot];

    /// <summary>The slots of deck a shore cell leads onto (a straight step along its bridge), empty for any other cell.</summary>
    public IReadOnlyList<int> DeckEntries(int cellIndex) => _deckEntries.TryGetValue(cellIndex, out var slots) ? slots : [];

    /// <summary>Speed on a slot of deck in percent: the road for every ground mover while its segment stands, 0 for ships.</summary>
    public int DeckSpeedPercent(int slot, Locomotor locomotor) =>
        _deckSegment[slot].IsIntact && locomotor != Locomotor.Naval ? Speeds.CellPercent(locomotor, LandType.Road, CellShape.Flat) : 0;

    /// <summary>The bridge segment on the cell, standing or destroyed; null when no bridge crosses it.</summary>
    public BridgeSegment? BridgeAt(CellPos cell) => _segmentsByCell.GetValueOrDefault(IndexOf(cell));

    /// <summary>The buildings the map places at the start, in order: the world makes them its first entities.</summary>
    public IReadOnlyList<BuildingState> StartBuildings => _startBuildings;

    /// <summary>True when a standing building takes the cell.</summary>
    public bool HasBuilding(CellPos cell) => _building[IndexOf(cell)];

    /// <summary>Every bridge, in id order.</summary>
    public IReadOnlyList<Bridge> Bridges => _bridges;

    /// <summary>The bridges as they stand now, for a copy or a replay.</summary>
    public BridgeState[] BridgeStates()
    {
        var states = new BridgeState[_bridges.Count];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = _bridges[i].State();
        }

        return states;
    }

    /// <summary>The corner heights (docs/implementation/M7_maps.md, 4.1). Read only from outside; the match changes them through the map.</summary>
    public CornerHeightGrid Heights => _heights;

    /// <summary>True when something on the cell stops at least one kind of mover (a tree, boulder, fence, wall, or the plain flag).</summary>
    public bool IsBlocked(CellPos cell) => ObstacleMask(cell) != 0;

    /// <summary>
    /// The movers stopped on the cell, one bit per <see cref="Locomotor"/>: the plain flag and a standing building stop all,
    /// objects their own.
    /// </summary>
    public byte ObstacleMask(CellPos cell)
    {
        var index = IndexOf(cell);
        var mask = _blocked[index] || (_building[index] && !_docks.Contains(index)) ? MapObjectTypes.AllMovers : (byte)0;
        if (_objectsByCell.TryGetValue(index, out var inCell))
        {
            foreach (var mapObject in inCell)
            {
                mask |= mapObject.IsAlive ? mapObject.Type.BlockMask : (byte)0;
            }
        }

        return mask;
    }

    /// <summary>
    /// The slowest speed the standing objects on the cell give <paramref name="locomotor"/>, in percent: a snowdrift slows, a
    /// tree or a fence does not. 100 when no object slows it.
    /// </summary>
    public int ObjectSpeedPercent(CellPos cell, Locomotor locomotor)
    {
        var percent = 100;
        if (_objectsByCell.TryGetValue(IndexOf(cell), out var inCell))
        {
            foreach (var mapObject in inCell)
            {
                var speed = mapObject.Type.SpeedPercentFor(locomotor);
                if (mapObject.IsAlive && speed < percent)
                {
                    percent = speed;
                }
            }
        }

        return percent;
    }

    /// <summary>Every map object, destroyed ones (stumps and the like) included, in id order.</summary>
    public IReadOnlyList<MapObject> Objects => _objects;

    /// <summary>The ore and gem sources among <see cref="Objects"/> (M3 step 16), in id order.</summary>
    public IReadOnlyList<MapObject> Sources => _sources;

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
    /// (outside the map, a cliff, impassable land, an obstacle). See <see cref="LandSpeeds.CellPercent"/>.
    /// </summary>
    public int SpeedPercent(CellPos cell, Locomotor locomotor) =>
        Contains(cell) ? _passability.Percent(cell.Y * Width + cell.X, locomotor) : 0;

    /// <summary>True when a unit with <paramref name="locomotor"/> may stand on the cell: inside the map, not a cliff, passable land, no obstacle.</summary>
    public bool CanEnter(CellPos cell, Locomotor locomotor) => SpeedPercent(cell, locomotor) > 0;

    /// <summary>
    /// An independent copy: the match changes its own map, never the one in the settings. Buildings destroyed in the match stay
    /// down in the copy. The copy goes by <paramref name="speeds"/> and <paramref name="types"/>, or by those of this map.
    /// </summary>
    public MapGrid Clone(LandSpeeds? speeds = null, MapTypes? types = null)
    {
        // The blocked cells come after the start buildings: the ruins of one that fell (RA2) lie where it stood.
        var copy = FromLayers(
            Width, Height, LandBytes(), new byte[Width * Height], _heights.LevelBytes(), ObjectStates(), BridgeStates(), _startBuildings, _ore,
            speeds ?? Speeds, types ?? Types);
        _blocked.CopyTo(copy._blocked, 0);
        _building.CopyTo(copy._building, 0);
        copy._passability = new Passability(copy);

        return copy;
    }

    /// <summary>True when both maps have the same size and the same cells.</summary>
    public bool ContentEquals(MapGrid other) =>
        Width == other.Width
        && Height == other.Height
        && _land.AsSpan().SequenceEqual(other._land)
        && _blocked.AsSpan().SequenceEqual(other._blocked)
        && _heights.ContentEquals(other._heights)
        && ObjectsEqual(other)
        && BridgesEqual(other)
        && System.Linq.Enumerable.SequenceEqual(_startBuildings, other._startBuildings)
        && _building.AsSpan().SequenceEqual(other._building)
        && _ore.AsSpan().SequenceEqual(other._ore);

    /// <summary>The ore of the cell (M3 step 9); empty off the map.</summary>
    public OreCell OreAt(CellPos cell) => Contains(cell) ? OreCell.FromPacked(_ore[IndexOf(cell)]) : OreCell.Empty;

    /// <summary>Ore layer as bytes in cell index order (<see cref="OreCell.Packed"/>), the replay layout.</summary>
    public byte[] OreBytes() => (byte[])_ore.Clone();

    /// <summary>
    /// Counts every change of the ore, so the client redraws it. Ore takes no part in movement, so it leaves <see cref="Version"/> and the
    /// path costs alone.
    /// </summary>
    public int OreVersion { get; private set; }

    /// <summary>The ore of a cell grows, spreads or is harvested (M3 steps 9–10).</summary>
    internal void SetOre(CellPos cell, OreCell ore)
    {
        _ore[IndexOf(cell)] = ore.Packed;
        _digest = null;
        OreVersion++;
    }

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
        if (!LandSpeeds.IsKnown(land))
        {
            throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type.");
        }

        _land[IndexOf(cell)] = land;
        _passability.Update(this, cell);
        _digest = null;
        _highwaySums = null;
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
        ObjectDeaths++;
    }

    /// <summary>
    /// Counts the objects destroyed (<see cref="DestroyObject"/>), each of which also grows <see cref="Version"/>: the client hides a
    /// crushed lamp post or a felled tree at once and rebuilds the rest of the map only when <see cref="LayoutVersion"/> grows. Not part
    /// of the state.
    /// </summary>
    public int ObjectDeaths { get; private set; }

    /// <summary>Grows with every change of the map but an object's destruction: the land, the heights, the bridges, the buildings.</summary>
    public int LayoutVersion => Version - ObjectDeaths;

    /// <summary>
    /// Counts the changes of the cells buildings take (<see cref="SetBuildingCells"/>: one placed, sold, destroyed, packed up), each of
    /// which also grows <see cref="Version"/>. Not part of the state.
    /// </summary>
    public int BuildingChanges { get; private set; }

    /// <summary>
    /// Grows with every change of the ground and what stands on it for good: the land, the heights, the bridges — not an object
    /// destroyed, not a building placed or gone. The trees, the terrain and the drills of the client are rebuilt by it: by
    /// <see cref="LayoutVersion"/> every building of a match rebuilt the 25 000 trees of a forest map, a frame of 140 ms (the performance
    /// log, 2026-10-28).
    /// </summary>
    public int GroundVersion => LayoutVersion - BuildingChanges;

    /// <summary>
    /// Adds a bridge (docs/implementation/M7_maps.md, 4.6) and returns null, or returns what is wrong with it and adds nothing:
    /// it must run along X or along Y over water cells (shallow or deep) that no other bridge takes, with a shore beyond both
    /// ends, a cell on the map that is not water; a bridge of one cell needs that along X or along Y. Hit points are per segment,
    /// 0 (destroyed) up to the type's.
    /// </summary>
    internal string? TryAddBridge(BridgeState state)
    {
        if (!Types.IsKnown(state.Kind))
        {
            return $"unknown bridge kind {(int)state.Kind}";
        }

        var (from, to) = (state.From, state.To);
        if (!Contains(from) || !Contains(to))
        {
            return $"end {Text(Contains(from) ? to : from)} is outside the {Width}x{Height} map";
        }

        if (from.X != to.X && from.Y != to.Y)
        {
            return $"{Text(from)} to {Text(to)} runs neither along X nor along Y";
        }

        var length = Bridge.Length(from, to);
        if (state.SegmentHp.IsDefault || state.SegmentHp.Length != length)
        {
            return $"{length} segments need {length} hit point values";
        }

        var (dx, dy) = Bridge.Step(from, to);
        if (Types.Get(state.Kind).High)
        {
            return TryAddHighBridge(state, dx, dy, length);
        }

        for (var i = 0; i < length; i++)
        {
            var cell = new CellPos(from.X + dx * i, from.Y + dy * i);
            if (!IsWater(_land[IndexOf(cell)]))
            {
                return $"cell {Text(cell)} is not water";
            }

            if (_segmentsByCell.ContainsKey(IndexOf(cell)))
            {
                return $"cell {Text(cell)} already has a bridge";
            }

            if (state.SegmentHp[i] < 0 || state.SegmentHp[i] > Types.Get(state.Kind).SegmentHp)
            {
                return $"segment {i} has {state.SegmentHp[i]} hit points";
            }
        }

        var (alongX, alongY) = length > 1 ? (dx, dy) : (1, 0);
        var shore = ShoreProblem(from, to, alongX, alongY);
        if (shore is not null && length == 1 && ShoreProblem(from, to, 0, 1) is null)
        {
            (shore, alongX, alongY) = (null, 0, 1);
        }

        if (shore is not null)
        {
            return shore;
        }

        // The spans across: the middle cell, and beside it the water cells no bridge takes, as wide as the type.
        var (acrossX, acrossY) = (alongY, alongX);
        var half = (Types.Get(state.Kind).Width - 1) / 2;
        var spans = new System.Collections.Immutable.ImmutableArray<CellPos>[length];
        for (var i = 0; i < length; i++)
        {
            var middle = new CellPos(from.X + dx * i, from.Y + dy * i);
            var cells = System.Collections.Immutable.ImmutableArray.CreateBuilder<CellPos>();
            cells.Add(middle);
            for (var k = -half; k <= half; k++)
            {
                var beside = new CellPos(middle.X + acrossX * k, middle.Y + acrossY * k);
                if (k != 0 && Contains(beside) && IsWater(_land[IndexOf(beside)]) && !_blocked[IndexOf(beside)]
                    && !_segmentsByCell.ContainsKey(IndexOf(beside)))
                {
                    cells.Add(beside);
                }
            }

            spans[i] = cells.ToImmutable();
        }

        var bridge = new Bridge(_bridges.Count + 1, Types.Get(state.Kind), from, to, state.SegmentHp, spans);
        _bridges.Add(bridge);
        foreach (var segment in bridge.Segments)
        {
            foreach (var cell in segment.Cells)
            {
                _segmentsByCell.Add(IndexOf(cell), segment);
                _passability.Update(this, cell);
            }
        }

        _digest = null;
        Version++;
        return null;
    }

    /// <summary>
    /// A high bridge (2026-10-26; docs/map_elements/05_bridges): straight, three cells or longer, as wide as its type, from a
    /// shore to a shore — the cells just beyond both ends, one a lane, on the map, flat, dry land without a bridge, all at one
    /// level L of 2 or more. Under the deck the cells may be anything, water or land, but none rises above L and every one has a
    /// corner at L − 2 or lower; the end cells are cliffs (or rock), so nobody gets on from the ground. The deck lies at L.
    /// </summary>
    private string? TryAddHighBridge(BridgeState state, int dx, int dy, int length)
    {
        var (from, to) = (state.From, state.To);
        var type = Types.Get(state.Kind);
        if (length < 3)
        {
            return $"a high bridge spans three cells or more, {Text(from)} to {Text(to)} is {length}";
        }

        var (acrossX, acrossY) = (dy, dx);
        var half = (type.Width - 1) / 2;
        var level = -1;
        for (var k = -half; k <= half; k++)
        {
            foreach (var shore in (ReadOnlySpan<CellPos>)[
                         new CellPos(from.X - dx + acrossX * k, from.Y - dy + acrossY * k), new CellPos(to.X + dx + acrossX * k, to.Y + dy + acrossY * k)])
            {
                if (!Contains(shore) || IsWater(_land[IndexOf(shore)]) || _land[IndexOf(shore)] == LandType.Rock
                    || _heights.ShapeAt(shore) != CellShape.Flat || _segmentsByCell.ContainsKey(IndexOf(shore)))
                {
                    return $"no shore at {Text(shore)}: a high bridge runs from flat dry land to flat dry land";
                }

                var at = _heights.CellLevel(shore);
                if (level >= 0 && at != level)
                {
                    return $"the shores are at levels {level} and {at}: a high bridge joins two plateaus of one level";
                }

                level = at;
            }
        }

        if (level < 2)
        {
            return $"the shores are at level {level}: a high bridge stands at level 2 or higher";
        }

        var spans = new System.Collections.Immutable.ImmutableArray<CellPos>[length];
        for (var i = 0; i < length; i++)
        {
            var middle = new CellPos(from.X + dx * i, from.Y + dy * i);
            if (state.SegmentHp[i] < 0 || state.SegmentHp[i] > type.SegmentHp)
            {
                return $"segment {i} has {state.SegmentHp[i]} hit points";
            }

            var cells = System.Collections.Immutable.ImmutableArray.CreateBuilder<CellPos>();
            foreach (var k in Lanes(half))
            {
                var cell = new CellPos(middle.X + acrossX * k, middle.Y + acrossY * k);
                if (!Contains(cell) || _segmentsByCell.ContainsKey(IndexOf(cell)))
                {
                    return Contains(cell) ? $"cell {Text(cell)} already has a bridge" : $"cell {Text(cell)} is outside the {Width}x{Height} map";
                }

                var (low, high) = CornerRange(cell);
                if (high > level || low > level - 2)
                {
                    return $"cell {Text(cell)} is not low enough under a deck at level {level}";
                }

                var end = i == 0 || i == length - 1;
                if (end && _heights.ShapeAt(cell) != CellShape.Cliff && _land[IndexOf(cell)] != LandType.Rock)
                {
                    return $"the end cell {Text(cell)} is not a cliff: one could get on the deck from below";
                }

                cells.Add(cell);
            }

            spans[i] = cells.ToImmutable();
        }

        var bridge = new Bridge(_bridges.Count + 1, Types.Get(state.Kind), from, to, state.SegmentHp, spans);
        _bridges.Add(bridge);
        foreach (var segment in bridge.Segments)
        {
            for (var c = 0; c < segment.Cells.Length; c++)
            {
                var cell = segment.Cells[c];
                var index = IndexOf(cell);
                var slot = _deckCell.Count;
                _segmentsByCell.Add(index, segment);
                _deckCell.Add(index);
                _deckSegment.Add(segment);
                _deckLevel.Add(level);
                _deckSlotByCell.Add(index, slot);

                // An end cell leads off to its shore, straight along the bridge, and the shore leads onto it.
                var exit = -1;
                if (segment.Index == 0 || segment.Index == bridge.Segments.Count - 1)
                {
                    var outward = segment.Index == 0 ? -1 : 1;
                    var shore = new CellPos(cell.X + dx * outward, cell.Y + dy * outward);
                    exit = IndexOf(shore);
                    if (!_deckEntries.TryGetValue(exit, out var entries))
                    {
                        entries = new List<int>();
                        _deckEntries.Add(exit, entries);
                    }

                    entries.Add(slot);
                }

                _deckExit.Add(exit);
                _passability.Update(this, cell);
            }
        }

        _digest = null;
        Version++;
        return null;
    }

    // The lanes across a bridge, the middle one first, then from one side to the other, as the cells of a segment go.
    private static IEnumerable<int> Lanes(int half)
    {
        yield return 0;
        for (var k = -half; k <= half; k++)
        {
            if (k != 0)
            {
                yield return k;
            }
        }
    }

    private (int Low, int High) CornerRange(CellPos cell)
    {
        var (low, high) = (int.MaxValue, int.MinValue);
        for (var y = cell.Y; y <= cell.Y + 1; y++)
        {
            for (var x = cell.X; x <= cell.X + 1; x++)
            {
                var corner = _heights.CornerLevel(x, y);
                (low, high) = (Math.IntMath.Min(low, corner), Math.IntMath.Max(high, corner));
            }
        }

        return (low, high);
    }

    /// <summary>
    /// Adds a building of the start (docs/implementation/M7_maps.md, 4.8) and takes its cells, or returns what is wrong with it
    /// and adds nothing: a known kind, owner 0…8, facing 0…7, and a footprint on the map over flat cells of one level, not
    /// water or rock, with no obstacle, map object, bridge or other building.
    /// </summary>
    internal string? TryAddBuilding(BuildingState state)
    {
        if (state.IsOfData)
        {
            // A building of a faction's data on the map: the Neutral house's, as on the maps of RA2.
            if (string.IsNullOrEmpty(state.Def) || state.Def.Length > 64 || !System.Linq.Enumerable.All(state.Def, c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return "a building of the data needs the id of its definition";
            }

            if (!state.Owner.IsNeutral)
            {
                return $"{state.Def} is a building of a faction: a map places it for nobody (neutral) only";
            }

            if (state.Width is < 1 or > 6 || state.Height is < 1 or > 6)
            {
                return $"{state.Def} must have a footprint of 1 to 6 cells a side";
            }
        }
        else if (!Types.IsKnown(state.Kind))
        {
            return $"unknown building kind {(int)state.Kind}";
        }

        if (state.Owner.Value is < 0 or > World.PlayerId.MaxPlayers || state.Facing is < 0 or > 7)
        {
            return $"owner {state.Owner.Value} must be 0…{World.PlayerId.MaxPlayers} and facing {state.Facing} 0…7";
        }

        var (id, width, height) = state.IsOfData ? (state.Def!, state.Width, state.Height) : (Types.Get(state.Kind).Id, Types.Get(state.Kind).Width, Types.Get(state.Kind).Height);
        var far = new CellPos(state.Cell.X + width - 1, state.Cell.Y + height - 1);
        if (!Contains(state.Cell) || !Contains(far))
        {
            return $"{id} at {Text(state.Cell)} reaches outside the {Width}x{Height} map";
        }

        var level = _heights.CellLevel(state.Cell);
        for (var y = state.Cell.Y; y <= far.Y; y++)
        {
            for (var x = state.Cell.X; x <= far.X; x++)
            {
                var cell = new CellPos(x, y);
                var index = IndexOf(cell);
                if (_heights.ShapeAt(cell) != CellShape.Flat || _heights.CellLevel(cell) != level)
                {
                    return $"cell {Text(cell)} is not flat ground at the level of the rest";
                }

                if (IsWater(_land[index]) || _land[index] == LandType.Rock)
                {
                    return $"cell {Text(cell)} is {_land[index]}";
                }

                if (_blocked[index] || _building[index] || _objectsByCell.ContainsKey(index) || _segmentsByCell.ContainsKey(index))
                {
                    return $"cell {Text(cell)} is taken";
                }
            }
        }

        _startBuildings.Add(state);
        SetBuildingCells(state.Cell, far, true);
        return null;
    }

    /// <summary>A building fell: its cells open (no ruins in M7).</summary>
    internal void ClearBuilding(CellPos from, CellPos to)
    {
        _docks.RemoveWhere(index => index % Width >= from.X && index % Width <= to.X && index / Width >= from.Y && index / Width <= to.Y);
        SetBuildingCells(from, to, false);
    }

    /// <summary>
    /// A building of a player is placed (M3): its cells close, but for its <paramref name="dock"/> (RA2: the pad of a refinery), which
    /// stays the building's and open to drive on. Where it may stand is checked before.
    /// </summary>
    internal void TakeBuilding(CellPos from, CellPos to, CellPos? dock = null)
    {
        if (dock is { } open)
        {
            _docks.Add(IndexOf(open));
        }

        SetBuildingCells(from, to, true);
    }

    /// <summary>Whether the cell is the dock of a building: the building's, and open to drive on.</summary>
    public bool IsDock(CellPos cell) => Contains(cell) && _docks.Contains(IndexOf(cell));

    /// <summary>Takes hit points off a bridge segment without destroying it (the hash sees it; nothing else changes).</summary>
    internal void SetSegmentHp(BridgeSegment segment, int hp)
    {
        segment.Hp = hp;
        _digest = null;
    }

    /// <summary>Destroys a bridge segment: its cells are the water under it again.</summary>
    internal void DestroySegment(BridgeSegment segment)
    {
        segment.Hp = 0;
        foreach (var cell in segment.Cells)
        {
            _passability.Update(this, cell);
        }

        _digest = null;
        Version++;
    }

    /// <summary>Mends a destroyed bridge segment whole (loose ends, stage 2): its cell is the road of the bridge again.</summary>
    internal void RepairSegment(BridgeSegment segment)
    {
        segment.Hp = segment.Bridge.Type.SegmentHp;
        foreach (var cell in segment.Cells)
        {
            _passability.Update(this, cell);
        }

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
    /// FNV-1a of the land layer, the obstacle layer, the corner heights, the map objects, the bridges, the buildings and the ore, in index
    /// order, for the state hash. Computed once and kept
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

        // Rules v17 (M7 step 6): the bridges, in id order, with the hit points of every segment.
        hash.Add(_bridges.Count);
        foreach (var bridge in _bridges)
        {
            hash.Add(bridge.Id);
            hash.Add((byte)bridge.Kind);
            hash.Add(bridge.From.X);
            hash.Add(bridge.From.Y);
            hash.Add(bridge.To.X);
            hash.Add(bridge.To.Y);
            foreach (var segment in bridge.Segments)
            {
                hash.Add(segment.Hp);
            }
        }

        // Rules v18 (M7 step 7): the buildings of the start and the cells standing buildings take.
        hash.Add(_startBuildings.Count);
        foreach (var building in _startBuildings)
        {
            hash.Add((byte)building.Kind);
            hash.Add(building.Cell.X);
            hash.Add(building.Cell.Y);
            hash.Add(building.Owner.Value);
            hash.Add(building.Facing);
            if (building.IsOfData)
            {
                // Rules v110: a building of a faction's data, by its id and footprint (a kind of the map has none of these).
                var id = System.Text.Encoding.UTF8.GetBytes(building.Def!);
                hash.Add(id.Length);
                hash.Add(id);
                hash.Add(building.Width);
                hash.Add(building.Height);
            }
        }

        // Rules v99: the docks open in the footprints of buildings.
        hash.Add(_docks.Count);
        foreach (var dock in _docks)
        {
            hash.Add(dock);
        }

        foreach (var taken in _building)
        {
            hash.Add(taken);
        }

        // Rules v22 (M3 step 9): the ore of every cell.
        hash.Add(_ore);
        return hash.Value;
    }

    private void SetBuildingCells(CellPos from, CellPos to, bool taken)
    {
        for (var y = from.Y; y <= to.Y; y++)
        {
            for (var x = from.X; x <= to.X; x++)
            {
                _building[y * Width + x] = taken;
                _passability.Update(this, new CellPos(x, y));
            }
        }

        _digest = null;
        Version++;
        BuildingChanges++;
    }

    private static string Text(CellPos cell) => $"{cell.X},{cell.Y}";

    private static bool IsWater(LandType land) => land is LandType.Shallow or LandType.Deep;

    /// <summary>Null when the cells just before <paramref name="from"/> and just after <paramref name="to"/> are shore.</summary>
    private string? ShoreProblem(CellPos from, CellPos to, int dx, int dy)
    {
        foreach (var beyond in (ReadOnlySpan<CellPos>)[new CellPos(from.X - dx, from.Y - dy), new CellPos(to.X + dx, to.Y + dy)])
        {
            if (!Contains(beyond) || IsWater(_land[IndexOf(beyond)]))
            {
                return $"no shore at {Text(beyond)}, a bridge runs from shore to shore";
            }
        }

        return null;
    }

    private bool BridgesEqual(MapGrid other)
    {
        if (_bridges.Count != other._bridges.Count)
        {
            return false;
        }

        for (var i = 0; i < _bridges.Count; i++)
        {
            if (!_bridges[i].ContentEquals(other._bridges[i]))
            {
                return false;
            }
        }

        return true;
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
