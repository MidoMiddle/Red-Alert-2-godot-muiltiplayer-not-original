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
/// and not on or next to a road. Then the look of the clear ground (<see cref="Ground"/>). Everything is sampled at the canonical
/// cell or copied to the images, so it stays symmetric. Villages by the roads, which the plan allows, are left to the editor.
/// </summary>
internal static class Features
{
    public const int ForestClearance = 6;
    private const int RoughPercent = 15;
    private const int GrassPercent = 14;
    private const int DirtPercent = 8;

    // Trodden ground around a base: within this many cells of a start, dirt where the noise of the dirt is above its median.
    private const int TroddenRadius = Starts.FlatRadius - 1;

    // On the coast the sand reaches this many cells inland from the water, past the wet sand of the beach, where the land is low.
    private const int CoastSand = 3;

    public static void Buildings(GenMap map, CellPos contested)
    {
        var ground = MapChecks.BaseGrid(map.ToDefinition("work", ""));
        // Out of the ore zone round the contested field — its ore and what its gem drill throws — and clear of it (GenMap.OreClearance).
        foreach (var (dx, dy) in new[] { (6, 0), (0, 6), (-7, 0), (0, -7), (5, 5), (-6, -6), (8, 0), (0, 8), (-9, 0), (0, -9) })
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

    /// <summary>
    /// The houses of a town (the urban climate, as RA2's Urban maps): neutral houses, apartment blocks and sheds two cells off the roads,
    /// away from the bases and the ore, with their images under the symmetry, as many as <see cref="TownHouses"/> per image; a house whose
    /// images do not fit is left out. Soldiers can sit in the bigger ones once garrisons of the map come.
    /// </summary>
    public static void Town(GenMap map)
    {
        if (map.Settings.Climate != ClimateGrounds.Urban)
        {
            return;
        }

        var ground = MapChecks.BaseGrid(map.ToDefinition("work", ""));
        var kinds = new[]
        {
            "neutral_house_a", "neutral_apartment", "neutral_shop", "neutral_office", "neutral_house_a", "neutral_school", "neutral_church",
            "neutral_cafe", "neutral_bank", "neutral_hotel", "neutral_shed", "neutral_fire_station", "neutral_skyscraper", "neutral_kiosk",
            "neutral_auto_repair", "neutral_fountain", "neutral_boiler_house",
        };
        var candidates = new List<(int Order, CellPos Cell)>();
        for (var y = 2; y < map.N - 4; y++)
        {
            for (var x = 2; x < map.N - 4; x++)
            {
                var cell = new CellPos(x, y);
                if (map.Symmetry.CanonicalCell(cell) == cell && NearRoad(map, cell, 2) && !NearRoad(map, cell, 1)
                    && map.StartDistanceSquared(cell) > (Starts.FlatRadius + 4) * (Starts.FlatRadius + 4))
                {
                    candidates.Add(((int)(IntNoise.Hash(x, y, map.Seed, Channels.Town) % 100_000), cell));
                }
            }
        }

        candidates.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Cell.Y != b.Cell.Y ? a.Cell.Y.CompareTo(b.Cell.Y) : a.Cell.X.CompareTo(b.Cell.X));
        var built = 0;
        foreach (var (order, corner) in candidates)
        {
            if (built >= TownHouses)
            {
                break;
            }

            var kind = kinds[order % kinds.Length];
            var images = new List<StructurePlacement>();
            for (var k = 0; k < map.Symmetry.Order; k++)
            {
                // The north-west cell of the turned footprint is its smallest corner after the turn.
                var (minX, minY) = (int.MaxValue, int.MaxValue);
                foreach (var cell in MapChecks.Footprint(new StructurePlacement(kind, corner, 0, 0)))
                {
                    var image = map.Symmetry.Cell(k, cell);
                    (minX, minY) = (IntMath.Min(minX, image.X), IntMath.Min(minY, image.Y));
                }

                var placement = new StructurePlacement(kind, new CellPos(minX, minY), 0, order % 8);
                if (!images.Exists(other => other.Cell == placement.Cell))
                {
                    images.Add(placement);
                }
            }

            var placed = new List<StructurePlacement>(map.Structures);
            var fits = true;
            foreach (var placement in images)
            {
                fits &= MapChecks.BuildingProblem(ground, placement, placed) is null && NoOre(map, placement)
                    && MapChecks.Footprint(placement).TrueForAll(cell => !NearRoad(map, cell, 0) && map.LandAt(cell) == LandType.Clear);
                placed.Add(placement);
            }

            if (fits)
            {
                map.Structures.AddRange(images);
                built++;
            }
        }
    }

    /// <summary>How many houses a town has per image of the symmetry.</summary>
    public const int TownHouses = 20;

    // Whether a road lies within reach cells of the cell (0: the cell is a road).
    private static bool NearRoad(GenMap map, CellPos cell, int reach)
    {
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var near = new CellPos(cell.X + dx, cell.Y + dy);
                if (map.Contains(near) && map.LandAt(near) == LandType.Road)
                {
                    return true;
                }
            }
        }

        return false;
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

    /// <summary>
    /// The look of the clear ground, which the rules never see, by the climate (<see cref="ClimateGrounds"/>): patches of its first kind
    /// (dirt in the snow, dry clay in the sand) and of its second (grass under the snow, a meadow, paving, gravel) where their noises are
    /// among the highest <see cref="DirtPercent"/> and <see cref="GrassPercent"/> of the clear cells, and the first kind trodden around
    /// the bases; in a town the pavement along the roads; on the coast sand along the water too. Land, heights and everything else stay as
/// they are.
    /// </summary>
    public static void Ground(GenMap map)
    {
        var clear = new List<(CellPos Cell, int Dirt, int Grass)>();
        for (var y = 0; y < map.N; y++)
        {
            for (var x = 0; x < map.N; x++)
            {
                var cell = new CellPos(x, y);
                if (map.LandAt(cell) == LandType.Clear && !map.IsCliff(cell))
                {
                    var canonical = map.Symmetry.CanonicalCell(cell);
                    clear.Add((cell, IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Dirt), IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Grass)));
                }
            }
        }

        if (clear.Count == 0)
        {
            return;
        }

        // Thresholds by rank, not by value: the sum of the octaves gathers around the middle, so a fixed value would give few cells.
        var ground = ClimateGrounds.Of(map.Settings.Climate);
        var dirtLine = Percentile(clear.ConvertAll(c => c.Dirt), 100 - DirtPercent);
        var grassLine = Percentile(clear.ConvertAll(c => c.Grass), 100 - GrassPercent);
        var troddenLine = Percentile(clear.ConvertAll(c => c.Dirt), 50);
        foreach (var (cell, dirt, grass) in clear)
        {
            var trodden = map.StartDistanceSquared(cell) <= TroddenRadius * TroddenRadius && dirt > troddenLine;
            if (ground.Sidewalk != ground.Road && NextToRoad(map, cell))
            {
                map.Paint[cell.Y * map.N + cell.X] = ground.Sidewalk;
            }
            else if (trodden || dirt > dirtLine || (map.Settings.Climate == ClimateGrounds.Coast && Low(map, cell) && NearWater(map, cell, CoastSand)))
            {
                map.Paint[cell.Y * map.N + cell.X] = ground.PatchA;
            }
            else if (grass > grassLine)
            {
                map.Paint[cell.Y * map.N + cell.X] = ground.PatchB;
            }
        }
    }

    // Whether the cell lies down by the sea, not on the top of a cliff over it: no corner above level 1.
    private static bool Low(GenMap map, CellPos cell) =>
        map.Corner(cell.X, cell.Y) <= 1 && map.Corner(cell.X + 1, cell.Y) <= 1 && map.Corner(cell.X, cell.Y + 1) <= 1 && map.Corner(cell.X + 1, cell.Y + 1) <= 1;

    // Whether water lies within reach cells of the cell.
    private static bool NearWater(GenMap map, CellPos cell, int reach)
    {
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var near = new CellPos(cell.X + dx, cell.Y + dy);
                if (map.Contains(near) && map.IsWater(near))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The value below which <paramref name="percent"/> of the values lie.
    private static int Percentile(List<int> values, int percent)
    {
        values.Sort();
        return values[IntMath.Min(values.Count - 1, values.Count * percent / 100)];
    }

    public static void Forest(GenMap map)
    {
        // The desert, the town and the palm groves of the coast have fewer trees than the woods of winter and summer.
        var threshold = (map.Settings.Forest switch { Amount.Low => 74, Amount.Medium => 66, _ => 58 })
            + (map.Settings.Climate switch { ClimateGrounds.Desert => 12, ClimateGrounds.Urban => 6, ClimateGrounds.Coast => 3, _ => 0 });
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

        // The dressing stands there already (Dressing): no tree on it.
        foreach (var mapObject in map.Objects)
        {
            built.Add(mapObject.Cell);
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

    // No cell of the building within GenMap.OreClearance of the ore zone.
    private static bool NoOre(GenMap map, StructurePlacement placement)
    {
        foreach (var cell in MapChecks.Footprint(placement))
        {
            if (!map.Contains(cell) || map.NearOre(cell, GenMap.OreClearance))
            {
                return false;
            }
        }

        return true;
    }
}
