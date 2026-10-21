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

/// <summary>The cell gets another land type (a bridge falls into the water, step 6).</summary>
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

/// <summary>A corner gets another height level (destructible relief, later; tests now).</summary>
public sealed record SetCornerLevelEdit(int X, int Y, int Level) : MapEdit
{
    internal override (CellPos From, CellPos To) Area(MapGrid map) =>
        (new CellPos(Math.IntMath.Max(0, X - 1), Math.IntMath.Max(0, Y - 1)), new CellPos(Math.IntMath.Min(map.Width - 1, X), Math.IntMath.Min(map.Height - 1, Y)));

    internal override void Apply(MapGrid map) => map.SetCornerLevel(X, Y, Level);
}
