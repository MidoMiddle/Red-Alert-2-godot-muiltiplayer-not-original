using Rts.Simulation.Math;

namespace Rts.Simulation.Geometry;

/// <summary>
/// Exact position in the world, in leptons: 256 leptons = 1 cell, as in RA2.
/// Also used as a vector (difference of two positions).
/// </summary>
public readonly record struct WorldPos(int X, int Y)
{
    public const int LeptonsPerCell = 256;

    public static readonly WorldPos Zero = new(0, 0);

    /// <summary>Cell containing this position. Rounds toward negative infinity: -1 lepton is in cell -1.</summary>
    public CellPos ToCell() => new(
        (int)IntMath.DivFloor(X, LeptonsPerCell),
        (int)IntMath.DivFloor(Y, LeptonsPerCell));

    /// <summary>Squared length as a vector, exact for any int coordinates.</summary>
    public long LengthSquared => (long)X * X + (long)Y * Y;

    public static WorldPos operator +(WorldPos a, WorldPos b) => new(a.X + b.X, a.Y + b.Y);

    public static WorldPos operator -(WorldPos a, WorldPos b) => new(a.X - b.X, a.Y - b.Y);
}
