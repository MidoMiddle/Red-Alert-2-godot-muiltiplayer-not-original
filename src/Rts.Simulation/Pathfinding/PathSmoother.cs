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
/// leaves a road the search followed. One forward pass, integer only. With high bridges (<see cref="Decks"/>) a segment keeps to
/// one level: where the path gets on or off a deck both cells are waypoints, and a segment on a deck stays on that deck.
/// </summary>
public static class PathSmoother
{
    /// <param name="avoid">Cells the search kept out of (taken by other units); a shortcut must keep out of them too.</param>
    public static CellPath Smooth(MapGrid map, CellPath path, Locomotor locomotor, ImmutableArray<CellPos> avoid = default, bool startOnDeck = false)
    {
        var avoidSet = new HashSet<CellPos>(avoid.IsDefault ? [] : avoid);
        var cells = path.Cells;
        if (cells.Length <= 2)
        {
            return path with { Waypoints = cells };
        }

        var levels = map.DeckCount > 0 ? Decks.Levels(map, cells, startOnDeck, locomotor) : null;
        var waypoints = ImmutableArray.CreateBuilder<CellPos>();
        var line = new List<CellPos>();
        waypoints.Add(cells[0]);
        var anchor = 0;
        var slowest = Speed(map, cells, levels, 1, locomotor);
        for (var k = 2; k < cells.Length; k++)
        {
            if (levels is not null && (levels[k] != levels[anchor] || levels[k - 1] != levels[anchor]))
            {
                // On or off a deck: the step is the search's own, so the cell before it is a waypoint (and, after, the next anchor).
                if (anchor != k - 1)
                {
                    anchor = k - 1;
                    waypoints.Add(cells[anchor]);
                }

                slowest = Speed(map, cells, levels, k, locomotor);
                if (levels[k] != levels[k - 1])
                {
                    anchor = k;
                    waypoints.Add(cells[anchor]);
                    if (k + 1 < cells.Length)
                    {
                        slowest = Speed(map, cells, levels, k + 1, locomotor);
                    }
                }

                continue;
            }

            var slowestWithK = IntMath.Min(slowest, Speed(map, cells, levels, k, locomotor));
            var clear = levels is not null && levels[anchor]
                ? IsClearOnDeck(map, cells[anchor], cells[k], locomotor, slowestWithK, line, avoidSet)
                : IsClear(map, cells[anchor], cells[k], locomotor, slowestWithK, line, avoidSet);
            if (clear)
            {
                slowest = slowestWithK;
                continue;
            }

            // cells[k - 1] was the last cell seen from the anchor: it becomes a waypoint. The step from it to cells[k]
            // is an A* step and therefore always allowed.
            anchor = k - 1;
            waypoints.Add(cells[anchor]);
            slowest = Speed(map, cells, levels, k, locomotor);
        }

        if (waypoints[^1] == cells[^1])
        {
            return path with { Waypoints = waypoints.ToImmutable() };
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
        IsClear(map, from, to, locomotor, minPercent, new List<CellPos>(), new HashSet<CellPos>());

    // The speed of path cell k on its level.
    private static int Speed(MapGrid map, ImmutableArray<CellPos> cells, bool[]? levels, int k, Locomotor locomotor) =>
        levels is null ? map.SpeedPercent(cells[k], locomotor) : Decks.SpeedPercent(map, cells[k], levels[k], locomotor);

    // As IsClear, on a deck: every touched cell is a cell of the same deck, standing and fast enough.
    private static bool IsClearOnDeck(
        MapGrid map, CellPos from, CellPos to, Locomotor locomotor, int minPercent, List<CellPos> line, HashSet<CellPos> avoid)
    {
        var bridge = map.DeckSegment(map.DeckSlot(from)).Bridge;
        GridLine.Walk(from, to, line);
        for (var i = 1; i < line.Count; i++)
        {
            var slot = map.DeckSlot(line[i]);
            if (slot < 0 || map.DeckSegment(slot).Bridge != bridge)
            {
                return false;
            }

            var percent = map.DeckSpeedPercent(slot, locomotor);
            if (percent == 0 || percent < minPercent || avoid.Contains(line[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClear(
        MapGrid map, CellPos from, CellPos to, Locomotor locomotor, int minPercent, List<CellPos> line, HashSet<CellPos> avoid)
    {
        GridLine.Walk(from, to, line);
        for (var i = 1; i < line.Count; i++)
        {
            var percent = map.SpeedPercent(line[i], locomotor);
            if (percent == 0 || percent < minPercent || avoid.Contains(line[i]))
            {
                return false;
            }
        }

        return true;
    }
}
