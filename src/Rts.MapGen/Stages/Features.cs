using System.Collections.Generic;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stages 6, 8 and 9 (docs/implementation/M7_maps.md, 4.14): an oil derrick by the contested ore of every image, where the
/// rules allow it; rough patches on flat clear land; and the forest — pines where the forest noise is above the setting's
/// threshold, not within <see cref="ForestClearance"/> cells of a start or ore, not on water, beach, rock, cliffs or buildings,
/// and not on or next to a road. Everything is sampled at the canonical cell or copied to the images, so it stays symmetric. Villages by the
/// roads, which the plan allows, are left to the editor.
/// </summary>
internal static class Features
{
    public const int ForestClearance = 6;
    private const int RoughPercent = 15;

    public static void Buildings(GenMap map, CellPos contested)
    {
        var ground = MapChecks.BaseGrid(map.ToDefinition("work", ""));
        foreach (var (dx, dy) in new[] { (3, 0), (0, 3), (-4, 0), (0, -4), (3, 3), (-4, -4) })
        {
            var corner = new CellPos(contested.X + dx, contested.Y + dy);
            var images = new List<StructurePlacement>();
            for (var k = 0; k < map.Symmetry.Order; k++)
            {
                // The north-west cell of the turned footprint is its smallest corner after the turn.
                var (minX, minY) = (int.MaxValue, int.MaxValue);
                foreach (var cell in MapChecks.Footprint(new StructurePlacement("tech_oil_derrick", corner, 0, 0)))
                {
                    var image = map.Symmetry.Cell(k, cell);
                    (minX, minY) = (IntMath.Min(minX, image.X), IntMath.Min(minY, image.Y));
                }

                var placement = new StructurePlacement("tech_oil_derrick", new CellPos(minX, minY), 0, 0);
                if (!images.Contains(placement))
                {
                    images.Add(placement);
                }
            }

            var placed = new List<StructurePlacement>(map.Structures);
            var fits = true;
            foreach (var placement in images)
            {
                fits &= MapChecks.BuildingProblem(ground, placement, placed) is null && NoOre(map, placement);
                placed.Add(placement);
            }

            if (fits)
            {
                map.Structures.AddRange(images);
                return;
            }
        }
    }

    public static void Rough(GenMap map)
    {
        for (var y = 0; y < map.N; y++)
        {
            for (var x = 0; x < map.N; x++)
            {
                var cell = new CellPos(x, y);
                var canonical = map.Symmetry.CanonicalCell(cell);
                if (map.LandAt(cell) == LandType.Clear && map.IsFlat(cell) && map.Ore[y * map.N + x] == 0
                    && IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Rough) > IntNoise.Max * (100 - RoughPercent) / 100)
                {
                    map.SetLand(cell, LandType.Rough);
                }
            }
        }
    }

    public static void Forest(GenMap map)
    {
        var threshold = map.Settings.Forest switch { Amount.Low => 74, Amount.Medium => 66, _ => 58 };
        var ore = new List<CellPos>();
        for (var i = 0; i < map.Ore.Length; i++)
        {
            if (map.Ore[i] > 0)
            {
                ore.Add(new CellPos(i % map.N, i / map.N));
            }
        }

        var built = new HashSet<CellPos>();
        foreach (var structure in map.Structures)
        {
            built.UnionWith(MapChecks.Footprint(structure));
        }

        for (var y = 0; y < map.N; y++)
        {
            for (var x = 0; x < map.N; x++)
            {
                var cell = new CellPos(x, y);
                var canonical = map.Symmetry.CanonicalCell(cell);
                if (IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Forest) <= IntNoise.Max * threshold / 100
                    || map.LandAt(cell) is not (LandType.Clear or LandType.Rough) || map.IsCliff(cell) || built.Contains(cell)
                    || map.StartDistanceSquared(cell) < (Starts.FlatRadius + 2) * (Starts.FlatRadius + 2) || Near(ore, cell, ForestClearance)
                    || NextToRoad(map, cell))
                {
                    continue;
                }

                var variant = (int)(IntNoise.Hash(canonical.X, canonical.Y, map.Seed, Channels.Variant) % 4);
                map.Objects.Add(new MapObjectPlacement("pine_tree", cell, variant));
            }
        }
    }

    // A tree beside a road could close a diagonal step of it (units never cut corners): the forest keeps one cell away.
    private static bool NextToRoad(GenMap map, CellPos cell)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (map.Contains(next) && map.LandAt(next) == LandType.Road)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Near(List<CellPos> cells, CellPos cell, int distance)
    {
        foreach (var other in cells)
        {
            var (dx, dy) = (other.X - cell.X, other.Y - cell.Y);
            if (dx * dx + dy * dy < distance * distance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NoOre(GenMap map, StructurePlacement placement)
    {
        foreach (var cell in MapChecks.Footprint(placement))
        {
            if (!map.Contains(cell) || map.Ore[cell.Y * map.N + cell.X] > 0)
            {
                return false;
            }
        }

        return true;
    }
}
