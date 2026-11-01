using System.Collections.Generic;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.MapGen.Stages;

/// <summary>
/// A quay on a shore of the sea (docs/naval_elements/09_quay_wall; the owner, 2026-11-01: «a harbour on the coast»): <see cref="Length"/>
/// cells along the water, raised to <see cref="Level"/> — the quay wall is the cliff of two levels straight into the water — and
/// <see cref="Depth"/> rows of a paved apron behind it, at one level, where the harbour's buildings stand. The coast runs along the
/// screen, across the cells, and the quay along them: it fills the water within its site and digs out the low ground before its
/// wall, so it juts out into the sea as a pier. The corners round the apron come up to level 1 at least, so it slopes down to the
/// land about it and the units drive up; where water stays beside it, its sides are walls too. The quay's cells and the apron get the
/// quay material of the climate (<see cref="ClimateGround.Quay"/>), whose walls the client draws straight in its concrete; a quay
/// edge (<c>quay_edge</c>; the client picks by the cell which one has a ladder) stands on every cell of the wall. One quay and its
/// images, on the site that fills and digs least: inside the playable area, nothing standing there, no road on the wall, clear of
/// the ore and the bases, the ground at most a level over the apron, <see cref="Front"/> rows of water before the wall, the images
/// <see cref="Apart"/> cells apart. A map with no such site (a small one of four players, steep mountains) has no quay.
/// </summary>
internal static class Harbour
{
    public const int Length = 9;
    public const int Depth = 4;
    public const int Level = 2;
    public const int Front = 3;

    // How many cells of water the quay may fill: the coast runs along the screen, across the cells, and the quay along them.
    private const int MaxFill = 4 * Length;

    // How many cells of low ground before the wall may be dug out to the water.
    private const int MaxDig = 2 * Length;

    // How far apart the quay and its images keep: two quays across the axis with a notch between them would look like one broken.
    private const int Apart = 4;

    // Seaward of the wall (−inland), along it.
    private static readonly (int AlongX, int AlongY, int InX, int InY)[] Sides = [(1, 0, 0, 1), (1, 0, 0, -1), (0, 1, 1, 0), (0, 1, -1, 0)];

    /// <summary>
    /// The quay and its images; false when no site of the map fits one. On a map made before (<paramref name="clearing"/>) the small
    /// things of its dressing and its trees give way to it; the drills, the buildings and the bridges never.
    /// </summary>
    public static bool Build(GenMap map, bool clearing = false)
    {
        var taken = Taken(map, clearing);
        var candidates = new List<(int Order, CellPos Cell)>();
        for (var y = 1; y < map.N - 1; y++)
        {
            for (var x = 1; x < map.N - 1; x++)
            {
                var cell = new CellPos(x, y);
                if (map.Symmetry.CanonicalCell(cell) == cell && map.LandAt(cell) == LandType.Beach)
                {
                    candidates.Add(((int)(IntNoise.Hash(x, y, map.Seed, Channels.Dressing * 16 + 12) % 100_000), cell));
                }
            }
        }

        // The site that fills and digs least, the first of the seed's order among the equal.
        candidates.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Cell.Y != b.Cell.Y ? a.Cell.Y.CompareTo(b.Cell.Y) : a.Cell.X.CompareTo(b.Cell.X));
        Quay? best = null;
        var least = int.MaxValue;
        foreach (var (order, cell) in candidates)
        {
            for (var turn = 0; turn < Sides.Length; turn++)
            {
                var (ax, ay, ix, iy) = Sides[(order + turn) % Sides.Length];
                var quay = new Quay(cell, ax, ay, ix, iy);
                if (Cost(map, quay, taken) is var cost and >= 0 && cost < least)
                {
                    (best, least) = (quay, cost);
                }
            }
        }

        if (best is not { } site)
        {
            return false;
        }

        Lay(map, site, clearing);
        map.Quays.Add(site);
        return true;
    }

    // The cells of buildings, objects and bridges.
    private static HashSet<CellPos> Taken(GenMap map, bool clearing)
    {
        var taken = new HashSet<CellPos>();
        foreach (var structure in map.Structures)
        {
            taken.UnionWith(MapChecks.Footprint(structure, map.Types));
        }

        foreach (var mapObject in map.Objects)
        {
            if (!clearing || !Clears(mapObject))
            {
                taken.Add(mapObject.Cell);
            }
        }

        foreach (var bridge in map.Bridges)
        {
            taken.UnionWith(MapChecks.BridgeCells(bridge));
        }

        return taken;
    }

    // How many cells the quay fills and digs, or −1 when it does not fit there.
    private static int Cost(GenMap map, Quay quay, HashSet<CellPos> taken)
    {
        // Before the wall water, or low ground dug out to it.
        var clearance = (Starts.FlatRadius + 2) * (Starts.FlatRadius + 2);
        var dig = 0;
        for (var v = -Front; v < 0; v++)
        {
            for (var u = 0; u < Length; u++)
            {
                var cell = quay.Cell(u, v);
                if (!map.Contains(cell) || taken.Contains(cell)
                    || (!map.IsWater(cell) && (GenMap.IsRoad(map.LandAt(cell)) || High(map, cell) > 1 || map.NearOre(cell, 0)
                                               || map.StartDistanceSquared(cell) <= clearance)))
                {
                    return -1;
                }

                dig += map.IsWater(cell) ? 0 : 1;
            }
        }

        if (dig > MaxDig)
        {
            return -1;
        }

        var fill = 0;
        for (var v = 0; v <= Depth + 1; v++)
        {
            for (var u = -1; u <= Length; u++)
            {
                var cell = quay.Cell(u, v);
                if (!map.Contains(cell) || !map.Area.Contains(cell) || taken.Contains(cell)
                    || map.NearOre(cell, GenMap.OreClearance) || map.StartDistanceSquared(cell) <= clearance
                    || (v == 0 && GenMap.IsRoad(map.LandAt(cell))) || High(map, cell) > Level + 1)
                {
                    return -1;
                }

                fill += map.IsWater(cell) ? 1 : 0;
            }
        }

        if (fill > MaxFill)
        {
            return -1;
        }

        // The images apart: none within Apart cells of another's site.
        var site = new HashSet<CellPos>();
        for (var k = 0; k < map.Symmetry.Order; k++)
        {
            var image = new List<CellPos>();
            var near = new List<CellPos>();
            for (var v = -Front - Apart; v <= Depth + 1 + Apart; v++)
            {
                for (var u = -1 - Apart; u <= Length + Apart; u++)
                {
                    var cell = map.Symmetry.Cell(k, quay.Cell(u, v));
                    near.Add(cell);
                    if (v >= -Front && v <= Depth + 1 && u >= -1 && u <= Length)
                    {
                        image.Add(cell);
                    }
                }
            }

            if (image.Exists(site.Contains))
            {
                return -1;
            }

            near.ForEach(cell => site.Add(cell));
        }

        return fill + dig;
    }

    // The heights, the ground and the edges of the quay, on it and its images.
    // A small thing of the dressing or a tree, which a quay laid on a map made before takes away.
    private static bool Clears(MapObjectPlacement mapObject) => mapObject.Type is not (Drills.OreSource or Drills.GemSource or "quay_edge");

    private static void Lay(GenMap map, Quay quay, bool clearing)
    {
        if (clearing)
        {
            var site = new HashSet<CellPos>();
            for (var v = -Front; v <= Depth + 1; v++)
            {
                for (var u = -1; u <= Length; u++)
                {
                    foreach (var image in map.Symmetry.Images(quay.Cell(u, v)))
                    {
                        site.Add(image);
                    }
                }
            }

            map.Objects.RemoveAll(o => Clears(o) && site.Contains(o.Cell));
        }

        // The low ground before the wall dug out to the water, down to its level.
        for (var v = -Front; v < 0; v++)
        {
            for (var u = 0; u < Length; u++)
            {
                foreach (var cell in map.Symmetry.Images(quay.Cell(u, v)))
                {
                    if (!map.IsWater(cell))
                    {
                        map.SetLand(cell, LandType.Shallow);
                        map.Paint[cell.Y * map.N + cell.X] = null;
                        foreach (var (x, y) in Corners(cell))
                        {
                            map.SetCorner(x, y, 0);
                        }
                    }
                }
            }
        }

        // The shallows within the site filled first: the water left decides which corners stay down.
        for (var v = 0; v <= Depth + 1; v++)
        {
            for (var u = -1; u <= Length; u++)
            {
                foreach (var cell in map.Symmetry.Images(quay.Cell(u, v)))
                {
                    if (map.IsWater(cell))
                    {
                        map.SetLand(cell, LandType.Clear);
                        map.Paint[cell.Y * map.N + cell.X] = null;
                    }
                }
            }
        }

        var levels = new SortedDictionary<(int X, int Y), int>();
        for (var v = 1; v <= Depth; v++)
        {
            for (var u = 0; u < Length; u++)
            {
                foreach (var corner in Corners(quay.Cell(u, v)))
                {
                    levels[corner] = Level;
                }
            }
        }

        // Round the apron, up to level 1 where the ground is lower — not a corner of the water.
        for (var v = 0; v <= Depth + 1; v++)
        {
            for (var u = -1; u <= Length; u++)
            {
                foreach (var corner in Corners(quay.Cell(u, v)))
                {
                    if (!levels.ContainsKey(corner) && !TouchesWater(map, corner))
                    {
                        levels[corner] = IntMath.Max(1, map.Corner(corner.X, corner.Y));
                    }
                }
            }
        }

        var ground = ClimateGrounds.Of(map.Settings.Climate);
        for (var k = 0; k < map.Symmetry.Order; k++)
        {
            foreach (var ((x, y), level) in levels)
            {
                var (ix, iy) = map.Symmetry.Corner(k, x, y);
                map.SetCorner(ix, iy, level);
            }
        }

        for (var v = 0; v <= Depth + 1; v++)
        {
            for (var u = -1; u <= Length; u++)
            {
                var apron = u >= 0 && u < Length && v <= Depth;
                foreach (var cell in map.Symmetry.Images(quay.Cell(u, v)))
                {
                    var road = GenMap.IsRoad(map.LandAt(cell));
                    if ((apron || map.IsCliff(cell)) && !road)
                    {
                        // The quay and its ends: the paving of the harbour.
                        map.SetLand(cell, LandType.Clear);
                        map.Paint[cell.Y * map.N + cell.X] = ground.Quay;
                        map.Paved[cell.Y * map.N + cell.X] = true;
                    }
                    else if (map.LandAt(cell) == LandType.Beach)
                    {
                        // Up the slope round the apron: no sand.
                        map.SetLand(cell, LandType.Clear);
                    }
                }
            }
        }

        for (var u = 0; u < Length; u++)
        {
            foreach (var cell in map.Symmetry.Images(quay.Cell(u, 0)))
            {
                map.Objects.Add(new MapObjectPlacement("quay_edge", cell, 0));
            }
        }
    }

    private static (int X, int Y)[] Corners(CellPos cell) =>
        [(cell.X, cell.Y), (cell.X + 1, cell.Y), (cell.X, cell.Y + 1), (cell.X + 1, cell.Y + 1)];

    private static bool TouchesWater(GenMap map, (int X, int Y) corner)
    {
        for (var dy = -1; dy <= 0; dy++)
        {
            for (var dx = -1; dx <= 0; dx++)
            {
                var cell = new CellPos(corner.X + dx, corner.Y + dy);
                if (map.Contains(cell) && map.IsWater(cell))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int High(GenMap map, CellPos cell) =>
        IntMath.Max(IntMath.Max(map.Corner(cell.X, cell.Y), map.Corner(cell.X + 1, cell.Y)),
            IntMath.Max(map.Corner(cell.X, cell.Y + 1), map.Corner(cell.X + 1, cell.Y + 1)));
}

/// <summary>A quay of <see cref="Harbour"/>: the cell (u, v) is <c>u</c> cells along the wall from its first, <c>v</c> inland (0: the wall, below 0: the water before it).</summary>
internal readonly record struct Quay(CellPos Origin, int AlongX, int AlongY, int InX, int InY)
{
    public CellPos Cell(int u, int v) => new(Origin.X + u * AlongX + v * InX, Origin.Y + u * AlongY + v * InY);
}
