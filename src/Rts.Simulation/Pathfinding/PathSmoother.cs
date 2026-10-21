using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Simulation.Pathfinding;

/// <summary>
/// Cells touched by the straight segment between two cell centres ("supercover", docs/implementation/M2_unit_control.md, 4.5).
/// Pure integer walk in half-cell units. When the segment passes exactly through a grid corner, both cells beside that
/// corner are included, so a line can never slip between two diagonal obstacles.
/// </summary>
public static class GridLine
{
    /// <summary>Every cell the segment from the centre of <paramref name="from"/> to the centre of <paramref name="to"/> touches, in order.</summary>
    public static List<CellPos> Cells(CellPos from, CellPos to)
    {
        var cells = new List<CellPos>();
        Walk(from, to, cells);
        return cells;
    }

    internal static void Walk(CellPos from, CellPos to, List<CellPos> cells)
    {
        cells.Clear();
        var nx = IntMath.Abs(to.X - from.X);
        var ny = IntMath.Abs(to.Y - from.Y);
        var sx = IntMath.Sign(to.X - from.X);
        var sy = IntMath.Sign(to.Y - from.Y);
        var x = from.X;
        var y = from.Y;
        cells.Add(from);

        for (int ix = 0, iy = 0; ix < nx || iy < ny;)
        {
            // Which grid line comes first: the next vertical one at t = (0.5 + ix) / nx or the next horizontal one at
            // t = (0.5 + iy) / ny. Cross-multiplied to stay in integers.
            var decision = (1L + 2 * ix) * ny - (1L + 2 * iy) * nx;
            if (decision == 0)
            {
                // Exactly through a corner: the segment touches both cells beside it.
                cells.Add(new CellPos(x + sx, y));
                cells.Add(new CellPos(x, y + sy));
                x += sx;
                y += sy;
                ix++;
                iy++;
            }
            else if (decision < 0)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }

            cells.Add(new CellPos(x, y));
        }
    }
}

/// <summary>
/// String pulling on an A* path (docs/implementation/M2_unit_control.md, 4.5): from an anchor the path is extended cell by cell
/// while the straight segment to that cell stays clear; where it stops being clear, the previous cell becomes a waypoint and the
/// next anchor. A segment is clear when every cell it touches (<see cref="GridLine"/>) can be entered and none of them is slower
/// than the slowest cell of the path part it replaces, so smoothing never cuts across rough land the search went around, nor
/// leaves a road the search followed. One forward pass, integer only.
/// </summary>
public static class PathSmoother
{
    public static CellPath Smooth(MapGrid map, CellPath path, Locomotor locomotor)
    {
        var cells = path.Cells;
        if (cells.Length <= 2)
        {
            return path with { Waypoints = cells };
        }

        var waypoints = ImmutableArray.CreateBuilder<CellPos>();
        var line = new List<CellPos>();
        waypoints.Add(cells[0]);
        var anchor = 0;
        var slowest = map.SpeedPercent(cells[1], locomotor);
        for (var k = 2; k < cells.Length; k++)
        {
            var slowestWithK = IntMath.Min(slowest, map.SpeedPercent(cells[k], locomotor));
            if (IsClear(map, cells[anchor], cells[k], locomotor, slowestWithK, line))
            {
                slowest = slowestWithK;
                continue;
            }

            // cells[k - 1] was the last cell seen from the anchor: it becomes a waypoint. The step from it to cells[k]
            // is an A* step and therefore always allowed.
            anchor = k - 1;
            waypoints.Add(cells[anchor]);
            slowest = map.SpeedPercent(cells[k], locomotor);
        }

        waypoints.Add(cells[^1]);
        return path with { Waypoints = waypoints.ToImmutable() };
    }

    /// <summary>
    /// True when a unit can drive straight from the centre of <paramref name="from"/> to the centre of <paramref name="to"/>:
    /// every touched cell after the first can be entered at <paramref name="minPercent"/> or faster. The first cell is where
    /// the unit already is.
    /// </summary>
    public static bool IsClear(MapGrid map, CellPos from, CellPos to, Locomotor locomotor, int minPercent) =>
        IsClear(map, from, to, locomotor, minPercent, new List<CellPos>());

    private static bool IsClear(MapGrid map, CellPos from, CellPos to, Locomotor locomotor, int minPercent, List<CellPos> line)
    {
        GridLine.Walk(from, to, line);
        for (var i = 1; i < line.Count; i++)
        {
            var percent = map.SpeedPercent(line[i], locomotor);
            if (percent == 0 || percent < minPercent)
            {
                return false;
            }
        }

        return true;
    }
}
