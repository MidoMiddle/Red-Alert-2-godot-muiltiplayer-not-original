using System.Collections.Generic;
using System.Linq;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 4, bridges (docs/implementation/M7_maps.md, 4.14): straight across the river from shore to shore, at least two
/// crossings, symmetric. With a mirror the river runs down the axis: two rows, a third and two thirds down, each bridge its
/// own mirror image. With turns: one row of the first arm three eighths down, inside the playable area, and its images. A row that does not take a bridge
/// (checked with the load rules, <see cref="MapChecks.BridgeProblem"/>) gives way to the next one.
/// </summary>
internal static class Bridges
{
    private const string Type = "bridge_wood_low";

    public static void Place(GenMap map)
    {
        var n = map.N;
        var ground = MapChecks.BaseGrid(map.ToDefinition("work", ""), map.Types);
        if (map.Settings.Symmetry == MapSymmetry.Mirror)
        {
            foreach (var row in new[] { n / 3, 2 * n / 3 })
            {
                TryRows(map, ground, row, from: n / 2 - 1, mirrored: true);
            }

            return;
        }

        TryRows(map, ground, 3 * n / 8, from: -1, mirrored: false);
    }

    private static void TryRows(GenMap map, MapGrid ground, int row, int from, bool mirrored)
    {
        foreach (var offset in new[] { 0, 1, -1, 2, -2, 3, -3, 4, -4 })
        {
            var y = row + offset;
            var x = from >= 0 ? from : FirstWater(map, y);
            if (x < 0 || !map.IsWater(new CellPos(x, y)))
            {
                continue;
            }

            var (left, right) = (x, x);
            while (left > 0 && map.IsWater(new CellPos(left - 1, y)))
            {
                left--;
            }

            while (right < map.N - 1 && map.IsWater(new CellPos(right + 1, y)))
            {
                right++;
            }

            var bridge = new BridgePlacement(Type, new CellPos(left, y), new CellPos(right, y));
            var images = Images(map, bridge, mirrored);
            if (images.TrueForAll(b => MapChecks.BridgeProblem(ground, b, map.Bridges) is null) && Separate(images))
            {
                map.Bridges.AddRange(images);
                return;
            }
        }
    }

    /// <summary>
    /// A repair hut by each end of every bridge (loose ends, stage 2; 2026-10-30), symmetric, a tree felled for one where no cell by the
    /// end is free: see <see cref="BridgeHuts"/>.
    /// </summary>
    public static void Huts(GenMap map)
    {
        var before = map.ToDefinition("work", "");
        var hutted = BridgeHuts.AddTo(before, map.Types, map.Symmetry.Images);
        map.Structures.AddRange(hutted.Structures.Where(structure => !before.Structures.Contains(structure)));
        if (hutted.Objects.Length != before.Objects.Length)
        {
            var kept = hutted.Objects.ToHashSet();
            map.Objects.RemoveAll(placement => !kept.Contains(placement));
        }
    }

    private static List<BridgePlacement> Images(GenMap map, BridgePlacement bridge, bool mirrored)
    {
        var images = new List<BridgePlacement> { bridge };
        if (mirrored)
        {
            return images;
        }

        for (var k = 1; k < map.Symmetry.Order; k++)
        {
            var from = map.Symmetry.Cell(k, bridge.From);
            var to = map.Symmetry.Cell(k, bridge.To);
            var image = from.Y < to.Y || (from.Y == to.Y && from.X <= to.X) ? new BridgePlacement(Type, from, to) : new BridgePlacement(Type, to, from);
            if (!images.Contains(image))
            {
                images.Add(image);
            }
        }

        return images;
    }

    /// <summary>True when no two of the bridges share a cell.</summary>
    private static bool Separate(List<BridgePlacement> bridges)
    {
        var cells = new HashSet<CellPos>();
        foreach (var bridge in bridges)
        {
            foreach (var cell in MapChecks.BridgeCells(bridge))
            {
                if (!cells.Add(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int FirstWater(GenMap map, int y)
    {
        if (y < 0 || y >= map.N)
        {
            return -1;
        }

        for (var x = 0; x < map.N; x++)
        {
            if (map.LandAt(new CellPos(x, y)) is LandType.Shallow or LandType.Deep)
            {
                return x;
            }
        }

        return -1;
    }
}
