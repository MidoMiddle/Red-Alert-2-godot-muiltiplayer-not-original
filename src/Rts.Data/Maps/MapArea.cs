using Rts.Simulation.Geometry;
using Rts.Simulation.Math;

namespace Rts.Data.Maps;

/// <summary>
/// The playable area of a map (docs/implementation/M7_maps.md, 4.9; RA2's LocalSize): a rectangle on the screen, cut out of the
/// diamond the cells of the map make there. In the screen's axes of a cell's centre, <c>v = x − z</c> runs to the right (30 px a
/// step) and <c>u = x + z + 1</c> down (15 px a step); a cell plays when <see cref="Left"/> ≤ v ≤ <see cref="Right"/> and
/// <see cref="Top"/> ≤ u ≤ <see cref="Bottom"/>. Cells outside are scenery: the rules block them, the camera never shows them.
/// </summary>
public sealed record MapArea(int Left, int Top, int Right, int Bottom)
{
    /// <summary>The largest square in the screen's axes inside a map of <paramref name="n"/> × <paramref name="n"/> cells: about n²/2 cells, the same under every symmetry of the generator.</summary>
    public static MapArea Inscribed(int n) => new(-n / 2, n / 2, n / 2, n + n / 2);

    public static int U(CellPos cell) => cell.X + cell.Y + 1;

    public static int V(CellPos cell) => cell.X - cell.Y;

    public bool Contains(CellPos cell) => V(cell) >= Left && V(cell) <= Right && U(cell) >= Top && U(cell) <= Bottom;

    /// <summary>Cells from <paramref name="cell"/> to the nearest side of the area, in steps of its axes; negative outside.</summary>
    public int Margin(CellPos cell) =>
        IntMath.Min(IntMath.Min(V(cell) - Left, Right - V(cell)), IntMath.Min(U(cell) - Top, Bottom - U(cell)));
}
