namespace Rts.Simulation.Geometry;

/// <summary>Map cell coordinates.</summary>
public readonly record struct CellPos(int X, int Y)
{
    /// <summary>Top-left corner of the cell in leptons.</summary>
    public WorldPos Origin => new(X * WorldPos.LeptonsPerCell, Y * WorldPos.LeptonsPerCell);

    /// <summary>Center of the cell in leptons.</summary>
    public WorldPos Center => new(
        X * WorldPos.LeptonsPerCell + WorldPos.LeptonsPerCell / 2,
        Y * WorldPos.LeptonsPerCell + WorldPos.LeptonsPerCell / 2);
}
