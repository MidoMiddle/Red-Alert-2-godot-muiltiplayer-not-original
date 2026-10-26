using Rts.Simulation.Geometry;

namespace Rts.Simulation.Map;

/// <summary>One change of the map during a match (docs/implementation/M7_maps.md, 4.4), applied by <c>GameWorld.ChangeMap</c>.</summary>
public abstract record MapEdit
{
    private protected MapEdit()
    {
    }

    /// <summary>The cells whose speed the edit can change, inclusive: one cell, or the up to four cells around a corner.</summary>
    internal abstract (CellPos From, CellPos To) Area(MapGrid map);

    internal abstract void Apply(MapGrid map);
}

/// <summary>The cell gets another land type (tests; terrain changes later).</summary>
public sealed record SetLandEdit(CellPos Cell, LandType Land) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) => (Cell, Cell);

    internal override void Apply(MapGrid map) => map.SetLand(Cell, Land);
}

/// <summary>An obstacle on the cell appears or goes (a tree is felled, step 5; a building, step 7).</summary>
public sealed record SetObstacleEdit(CellPos Cell, bool Blocked) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) => (Cell, Cell);

    internal override void Apply(MapGrid map) => map.SetBlocked(Cell, Blocked);
}

/// <summary>A map object is destroyed (shot down or crushed): the cell opens to whoever it blocked.</summary>
public sealed record DestroyObjectEdit(int ObjectId) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) => (map.Objects[ObjectId - 1].Cell, map.Objects[ObjectId - 1].Cell);

    internal override void Apply(MapGrid map) => map.DestroyObject(ObjectId);
}

/// <summary>A segment of a bridge is destroyed: its cells are water again, open to ships, closed to the ground movers.</summary>
public sealed record DestroyBridgeSegmentEdit(int BridgeId, int Segment) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) => Span(map.Bridges[BridgeId - 1].Segments[Segment]);

    internal override void Apply(MapGrid map) => map.DestroySegment(map.Bridges[BridgeId - 1].Segments[Segment]);

    /// <summary>The box round the cells of a span across a bridge.</summary>
    internal static (CellPos From, CellPos To) Span(BridgeSegment segment)
    {
        var (from, to) = (segment.Cell, segment.Cell);
        foreach (var cell in segment.Cells)
        {
            from = new CellPos(Math.IntMath.Min(from.X, cell.X), Math.IntMath.Min(from.Y, cell.Y));
            to = new CellPos(Math.IntMath.Max(to.X, cell.X), Math.IntMath.Max(to.Y, cell.Y));
        }

        return (from, to);
    }
}

/// <summary>A destroyed segment of a bridge is mended (loose ends, stage 2): its cells are a road again, closed to ships.</summary>
public sealed record RepairBridgeSegmentEdit(int BridgeId, int Segment) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) => DestroyBridgeSegmentEdit.Span(map.Bridges[BridgeId - 1].Segments[Segment]);

    internal override void Apply(MapGrid map) => map.RepairSegment(map.Bridges[BridgeId - 1].Segments[Segment]);
}

/// <summary>
/// A building is placed: the cells from <paramref name="From"/> to <paramref name="To"/> (its footprint) close to every mover, but for its
/// <paramref name="Dock"/> (RA2: the pad of a refinery), open to drive on.
/// </summary>
public sealed record TakeBuildingEdit(CellPos From, CellPos To, CellPos? Dock = null) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) => (From, To);
    internal override void Apply(MapGrid map) => map.TakeBuilding(From, To, Dock);
}

/// <summary>A building fell: the cells from <paramref name="From"/> to <paramref name="To"/> (its footprint) open; no ruins in M7.</summary>
public sealed record ClearBuildingEdit(CellPos From, CellPos To) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) => (From, To);

    internal override void Apply(MapGrid map) => map.ClearBuilding(From, To);
}

/// <summary>A corner gets another height level (destructible relief, later; tests now).</summary>
public sealed record SetCornerLevelEdit(int X, int Y, int Level) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) =>
        (new CellPos(Math.IntMath.Max(0, X - 1), Math.IntMath.Max(0, Y - 1)), new CellPos(Math.IntMath.Min(map.Width - 1, X), Math.IntMath.Min(map.Height - 1, Y)));

    internal override void Apply(MapGrid map) => map.SetCornerLevel(X, Y, Level);
}
