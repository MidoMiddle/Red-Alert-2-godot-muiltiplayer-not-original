using System.Collections.Generic;
using Rts.Simulation.Geometry;

namespace Rts.Data.Maps.Editing;

public enum BrushShape
{
    Circle,
    Square,
}

/// <summary>
/// The brush of the editor (docs/implementation/M7_maps.md, 4.13): a circle or square of <see cref="Size"/> 1…10 around a
/// centre. A point lies under a circle when <c>dx² + dy² ≤ (Size − 1)²</c>, under a square when both offsets are at most
/// <c>Size − 1</c>. Height tools use the corners under it, the land tool the cells; nothing outside the map.
/// </summary>
public sealed record Brush(BrushShape Shape, int Size)
{
    public const int MinSize = 1;
    public const int MaxSize = 10;

    /// <summary>The corners under the brush centred on corner (<paramref name="x"/>, <paramref name="y"/>), row by row.</summary>
    public List<(int X, int Y)> Corners(EditableMap map, int x, int y)
    {
        var corners = new List<(int X, int Y)>();
        foreach (var (dx, dy) in Offsets())
        {
            if (map.ContainsCorner(x + dx, y + dy))
            {
                corners.Add((x + dx, y + dy));
            }
        }

        return corners;
    }

    /// <summary>The cells under the brush centred on <paramref name="cell"/>, row by row.</summary>
    public List<CellPos> Cells(EditableMap map, CellPos cell)
    {
        var cells = new List<CellPos>();
        foreach (var (dx, dy) in Offsets())
        {
            var under = new CellPos(cell.X + dx, cell.Y + dy);
            if (map.ContainsCell(under))
            {
                cells.Add(under);
            }
        }

        return cells;
    }

    private IEnumerable<(int Dx, int Dy)> Offsets()
    {
        var reach = Size - 1;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                if (Shape == BrushShape.Square || dx * dx + dy * dy <= reach * reach)
                {
                    yield return (dx, dy);
                }
            }
        }
    }
}
