using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Map;

/// <summary>Kinds of bridges. Values are part of the rules (replays, hash): never renumber.</summary>
public enum BridgeKind : byte
{
    WoodLow = 1,
}

/// <summary>What a kind of bridge is (docs/implementation/M7_maps.md, 4.6; map_elements/05_bridges): hit points of each segment.</summary>
public sealed record BridgeType(BridgeKind Kind, string Id, int SegmentHp);

/// <summary>
/// The bridges, kept in code until the data files of M3 (map_objects.yaml) like the map objects. Hit points are a rough guide
/// until the balance of M4. Only low bridges for now; high bridges come in M11. An engineer mends a bridge through its repair hut
/// (loose ends, stage 2).
/// </summary>
public static class BridgeTypes
{
    private static readonly BridgeType[] Types =
    [
        new(BridgeKind.WoodLow, "bridge_wood_low", SegmentHp: 600),
    ];

    public static IReadOnlyList<BridgeType> All => Types;

    public static bool IsKnown(BridgeKind kind) => kind == BridgeKind.WoodLow;

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
/// A low bridge over water (docs/implementation/M7_maps.md, 4.6): a straight chain of segments, one per cell, from shore to
/// shore. While a segment stands its cell is a road for ground movers and closed to ships, which never pass under a low
/// bridge; a destroyed segment leaves the water it stood on, and its neighbours stand on (no chain reaction). Part of the
/// map, like the map objects: ids from the map's own counter, part of the state hash.
/// </summary>
public sealed class Bridge
{
    private readonly BridgeSegment[] _segments;

    internal Bridge(int id, BridgeKind kind, CellPos from, CellPos to, ImmutableArray<int> hp)
    {
        Id = id;
        Kind = kind;
        From = from;
        To = to;
        _segments = new BridgeSegment[hp.Length];
        var (dx, dy) = Step(from, to);
        for (var i = 0; i < _segments.Length; i++)
        {
            _segments[i] = new BridgeSegment(this, i, new CellPos(from.X + dx * i, from.Y + dy * i), hp[i]);
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

/// <summary>One cell of a <see cref="Bridge"/>. Hit points 0 means destroyed: the cell is water again.</summary>
public sealed class BridgeSegment
{
    internal BridgeSegment(Bridge bridge, int index, CellPos cell, int hp)
    {
        Bridge = bridge;
        Index = index;
        Cell = cell;
        Hp = hp;
    }

    public Bridge Bridge { get; }

    /// <summary>Place in the chain, 0 at <see cref="Map.Bridge.From"/>.</summary>
    public int Index { get; }

    public CellPos Cell { get; }

    public int Hp { get; internal set; }

    public bool IsIntact => Hp > 0;
}
