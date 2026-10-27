using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Map;

/// <summary>Kinds of bridges. Values are part of the rules (replays, hash): never renumber.</summary>
public enum BridgeKind : byte
{
    WoodLow = 1,

    /// <summary>A high bridge of concrete, asphalt on top, between two plateaus (2026-10-26): ships and the valley pass under it.</summary>
    ConcreteHigh = 2,
}

/// <summary>
/// What a kind of bridge is (docs/implementation/M7_maps.md, 4.6; map_elements/05_bridges): hit points of each segment and how many
/// cells wide it is across — three tanks side by side on a low bridge (the owner's wish, 2026-10-24). A <paramref name="High"/>
/// bridge has a deck at the level of the plateaus it joins, over whatever lies below (<see cref="Decks"/>).
/// </summary>
public sealed record BridgeType(BridgeKind Kind, string Id, int SegmentHp, int Width, bool High = false);

/// <summary>
/// The bridges, kept in code until the data files of M3 (map_objects.yaml) like the map objects. Hit points are a rough guide
/// until the balance of M4. The low bridge of wood over water, and the high bridge of concrete between two plateaus (2026-10-26:
/// RA2's BridgeStrength of 1500 a segment, three cells wide as RA2 draws them). An engineer mends a bridge through its repair hut
/// (loose ends, stage 2).
/// </summary>
public static class BridgeTypes
{
    private static readonly BridgeType[] Types =
    [
        new(BridgeKind.WoodLow, "bridge_wood_low", SegmentHp: 600, Width: 3),
        new(BridgeKind.ConcreteHigh, "bridge_concrete_high", SegmentHp: 1500, Width: 3, High: true),
    ];

    public static IReadOnlyList<BridgeType> All => Types;

    /// <summary>The highest number of a kind the engine knows.</summary>
    public static int Highest => (int)BridgeKind.ConcreteHigh;

    /// <summary>The kind the engine knows by the id <paramref name="id"/>.</summary>
    public static bool TryGetKind(string id, out BridgeKind kind)
    {
        if (TryGetById(id, out var type))
        {
            kind = type.Kind;
            return true;
        }

        kind = default;
        return false;
    }

    public static bool IsKnown(BridgeKind kind) => kind is >= BridgeKind.WoodLow and <= BridgeKind.ConcreteHigh;

    public static BridgeType Get(BridgeKind kind) =>
        IsKnown(kind) ? Types[(int)kind - 1] : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown bridge kind.");

    /// <summary>The type of a map file id such as <c>bridge_wood_low</c>.</summary>
    public static bool TryGetById(string id, out BridgeType type)
    {
        foreach (var candidate in Types)
        {
            if (candidate.Id == id)
            {
                type = candidate;
                return true;
            }
        }

        type = null!;
        return false;
    }
}

/// <summary>
/// How a bridge starts a match (or stands in a replay): ends included, along X or along Y, and the hit points of each segment
/// from <see cref="From"/> to <see cref="To"/>. Ids follow the order of the list, from 1.
/// </summary>
public sealed record BridgeState(BridgeKind Kind, CellPos From, CellPos To, ImmutableArray<int> SegmentHp)
{
    /// <summary>A new bridge of <paramref name="kind"/> with every segment at full hit points.</summary>
    public static BridgeState New(BridgeKind kind, CellPos from, CellPos to)
    {
        var hp = new int[Bridge.Length(from, to)];
        System.Array.Fill(hp, BridgeTypes.Get(kind).SegmentHp);
        return new BridgeState(kind, from, to, ImmutableArray.Create(hp));
    }
}

/// <summary>
/// A low bridge over water (docs/implementation/M7_maps.md, 4.6): a straight chain of segments from shore to shore along its middle
/// line, one per cell of it. A segment is a span across, as wide as the type (<see cref="BridgeType.Width"/>, three tanks side by
/// side): the middle cell and the water cells beside it that no other bridge takes. While a segment stands its cells are a road
/// for ground movers and closed to ships, which never pass under a low bridge; a destroyed segment leaves the water it stood on,
/// and its neighbours stand on (no chain reaction). Part of the map, like the map objects: ids from the map's own counter, part of
/// the state hash.
/// </summary>
public sealed class Bridge
{
    private readonly BridgeSegment[] _segments;

    internal Bridge(int id, BridgeKind kind, CellPos from, CellPos to, ImmutableArray<int> hp, IReadOnlyList<ImmutableArray<CellPos>> cells)
    {
        Id = id;
        Kind = kind;
        From = from;
        To = to;
        _segments = new BridgeSegment[hp.Length];
        for (var i = 0; i < _segments.Length; i++)
        {
            _segments[i] = new BridgeSegment(this, i, cells[i], hp[i]);
        }
    }

    public int Id { get; }

    public BridgeKind Kind { get; }

    public BridgeType Type => BridgeTypes.Get(Kind);

    public CellPos From { get; }

    public CellPos To { get; }

    /// <summary>The segments from <see cref="From"/> to <see cref="To"/>.</summary>
    public IReadOnlyList<BridgeSegment> Segments => _segments;

    /// <summary>Cells from <paramref name="from"/> to <paramref name="to"/>, both included, on a straight line.</summary>
    internal static int Length(CellPos from, CellPos to) =>
        Math.IntMath.Max(Math.IntMath.Abs(to.X - from.X), Math.IntMath.Abs(to.Y - from.Y)) + 1;

    /// <summary>The step from one segment to the next: one cell along X or along Y, none for a bridge of one cell.</summary>
    internal static (int Dx, int Dy) Step(CellPos from, CellPos to) =>
        (Math.IntMath.Sign(to.X - from.X), Math.IntMath.Sign(to.Y - from.Y));

    internal BridgeState State()
    {
        var hp = new int[_segments.Length];
        for (var i = 0; i < hp.Length; i++)
        {
            hp[i] = _segments[i].Hp;
        }

        return new BridgeState(Kind, From, To, ImmutableArray.Create(hp));
    }

    internal bool ContentEquals(Bridge other)
    {
        if (Id != other.Id || Kind != other.Kind || From != other.From || To != other.To || _segments.Length != other._segments.Length)
        {
            return false;
        }

        for (var i = 0; i < _segments.Length; i++)
        {
            if (_segments[i].Hp != other._segments[i].Hp)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>One span of a <see cref="Bridge"/> across it. Hit points 0 means destroyed: its cells are water again.</summary>
public sealed class BridgeSegment
{
    internal BridgeSegment(Bridge bridge, int index, ImmutableArray<CellPos> cells, int hp)
    {
        Bridge = bridge;
        Index = index;
        Cells = cells;
        Hp = hp;
    }

    public Bridge Bridge { get; }

    /// <summary>Place in the chain, 0 at <see cref="Map.Bridge.From"/>.</summary>
    public int Index { get; }

    /// <summary>The cell on the middle line.</summary>
    public CellPos Cell => Cells[0];

    /// <summary>Its cells: the middle one first, then those beside it, from one side to the other.</summary>
    public ImmutableArray<CellPos> Cells { get; }

    public int Hp { get; internal set; }

    public bool IsIntact => Hp > 0;
}
