using Rts.Simulation.Geometry;

namespace Rts.MapGen;

/// <summary>
/// The symmetry of a square map of N cells a side (docs/implementation/M7_maps.md, 4.14): the images of a point under the
/// group — two for <see cref="MapSymmetry.Mirror"/> and <see cref="MapSymmetry.Rotate180"/>, four for
/// <see cref="MapSymmetry.Rotate90"/> — for cells (0…N−1) and for corners (0…N). Image k of the first player's start is the
/// start of player k + 1. <see cref="Canonical"/> picks one point of every orbit, so whatever is sampled there (noise) comes
/// out symmetric by itself.
/// </summary>
public sealed class Symmetry
{
    public Symmetry(MapSymmetry kind, int size)
    {
        Kind = kind;
        Size = size;
    }

    public MapSymmetry Kind { get; }

    public int Size { get; }

    /// <summary>How many images every point has (some may coincide).</summary>
    public int Order => Kind == MapSymmetry.Rotate90 ? 4 : 2;

    /// <summary>Image <paramref name="k"/> (0 = itself) of the cell (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public CellPos Cell(int k, CellPos cell)
    {
        var (x, y) = Image(k, cell.X, cell.Y, Size - 1);
        return new CellPos(x, y);
    }

    /// <summary>Image <paramref name="k"/> of the corner (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public (int X, int Y) Corner(int k, int x, int y) => Image(k, x, y, Size);

    /// <summary>The one point of the cell's orbit that stands for all of it: the smallest by row, then column.</summary>
    public CellPos CanonicalCell(CellPos cell)
    {
        var (x, y) = Canonical(cell.X, cell.Y, Size - 1);
        return new CellPos(x, y);
    }

    public (int X, int Y) CanonicalCorner(int x, int y) => Canonical(x, y, Size);

    private (int X, int Y) Canonical(int x, int y, int max)
    {
        var best = (x, y);
        for (var k = 1; k < Order; k++)
        {
            var image = Image(k, x, y, max);
            if (image.Item2 < best.Item2 || (image.Item2 == best.Item2 && image.Item1 < best.Item1))
            {
                best = image;
            }
        }

        return best;
    }

    private (int X, int Y) Image(int k, int x, int y, int max) => (Kind, k % Order) switch
    {
        (_, 0) => (x, y),
        (MapSymmetry.Mirror, _) => (max - x, y),
        (MapSymmetry.Rotate180, _) => (max - x, max - y),
        (_, 1) => (max - y, x),
        (_, 2) => (max - x, max - y),
        _ => (y, max - x),
    };
}
