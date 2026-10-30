using System.Collections.Generic;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 7, roads (docs/implementation/M7_maps.md, 4.14): the path of a wheeled unit (A*) from the first start to the shore at
/// the nearest end of a bridge, or to the centre when there are none — on a map of islands down to the beach of its first ramp
/// (<see cref="GenMap.Landing"/>); its cells and their images become road. Water and bridge cells stay as they are.
/// </summary>
internal static class Roads
{
    public static void Place(GenMap map)
    {
        var start = map.Starts[0].Cell;
        var target = map.Landing ?? new CellPos(map.N / 2, map.N / 2);
        var best = long.MaxValue;
        foreach (var bridge in map.Bridges)
        {
            var (dx, dy) = (IntMath.Sign(bridge.To.X - bridge.From.X), IntMath.Sign(bridge.To.Y - bridge.From.Y));
            foreach (var shore in new[] { new CellPos(bridge.From.X - dx, bridge.From.Y - dy), new CellPos(bridge.To.X + dx, bridge.To.Y + dy) })
            {
                var (sx, sy) = (shore.X - start.X, shore.Y - start.Y);
                var distance = (long)sx * sx + (long)sy * sy;
                if (distance < best)
                {
                    (best, target) = (distance, shore);
                }
            }
        }

        // The main road of a town is asphalt; elsewhere it is the unpaved road of the climate (docs/map_elements/04_roads). On a big map
        // it is a highway, three cells wide, outside the bases.
        var kind = map.Settings.Climate == ClimateGrounds.Urban ? LandType.Road : LandType.Track;
        var grid = map.Grid();
        var path = AStar.FindPath(grid, start, target, Locomotor.Wheel, map.N * map.N);
        var highway = map.Settings.Size >= HighwaySize;
        var cells = path.Cells;
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            var wide = highway && map.StartDistanceSquared(cell) > HighwayFromStart * HighwayFromStart;
            foreach (var under in wide ? Widened(cells, i) : [cell])
            {
                for (var k = 0; k < map.Symmetry.Order; k++)
                {
                    var image = map.Symmetry.Cell(k, under);
                    if (map.Contains(image) && !map.IsWater(image) && (wide || map.LandAt(image) != LandType.Highway)
                        && (under == cell || (map.LandAt(image) != LandType.Rock && !map.IsCliff(image))))
                    {
                        map.SetLand(image, wide ? LandType.Highway : kind);
                    }
                }
            }
        }

        if (highway && IntNoise.Hash(0, 0, map.Seed, Channels.Highway) % 2 == 0)
        {
            Guardrails(map);
        }

        Streets(map, cells);
    }

    /// <summary>The highway: the main road of a map this size or bigger (docs/map_elements/04_roads).</summary>
    public const int HighwaySize = 96;

    /// <summary>Within this many cells of a start the main road stays a road: the highway begins outside the base.</summary>
    public const int HighwayFromStart = Starts.FlatRadius + 2;

    // Cell i of the path and its neighbours across the way it goes: three cells wide, straight or on the diagonal.
    private static CellPos[] Widened(System.Collections.Immutable.ImmutableArray<CellPos> path, int i)
    {
        var (from, to) = (path[IntMath.Max(0, i - 1)], path[IntMath.Min(path.Length - 1, i + 1)]);
        var (dx, dy) = (IntMath.Sign(to.X - from.X), IntMath.Sign(to.Y - from.Y));
        var cell = path[i];
        return (dx, dy) switch
        {
            (0, _) => [cell, new CellPos(cell.X - 1, cell.Y), new CellPos(cell.X + 1, cell.Y)],
            (_, 0) => [cell, new CellPos(cell.X, cell.Y - 1), new CellPos(cell.X, cell.Y + 1)],
            _ => [cell, new CellPos(cell.X + dx, cell.Y), new CellPos(cell.X, cell.Y + dy),
                new CellPos(cell.X - dx, cell.Y), new CellPos(cell.X, cell.Y - dy)],
        };
    }

    /// <summary>
    /// A guardrail along both sides of the highway (half the big maps, by the seed): on the cells beside it that are open ground,
    /// a gap where a road, a track or a path meets it, near the bases and in the ore zone (nothing stands there, the owner's rule of
    /// 2026-10-25), so infantry crosses only there (docs/map_elements/04_roads).
    /// </summary>
    private static void Guardrails(GenMap map)
    {
        var placed = new HashSet<CellPos>();
        for (var y = 0; y < map.N; y++)
        {
            for (var x = 0; x < map.N; x++)
            {
                var cell = new CellPos(x, y);
                if (map.LandAt(cell) is not (LandType.Clear or LandType.Rough) || map.IsCliff(cell) || placed.Contains(cell) || map.NearOre(cell, 0)
                    || map.StartDistanceSquared(cell) <= HighwayFromStart * HighwayFromStart || !Beside(map, cell, LandType.Highway)
                    || Beside(map, cell, LandType.Road) || Beside(map, cell, LandType.Track) || Beside(map, cell, LandType.Path))
                {
                    continue;
                }

                foreach (var image in map.Symmetry.Images(cell))
                {
                    if (placed.Add(image))
                    {
                        map.Objects.Add(new MapObjectPlacement(Guardrail, image, 0));
                    }
                }
            }
        }
    }

    /// <summary>The map object of the highway's guardrail.</summary>
    public const string Guardrail = "highway_guardrail";

    /// <summary>
    /// The streets of a town (the urban climate): from every few cells of its main road (or highway) outside the bases, a street a cell
    /// wide straight out on either side, as far as open ground goes and at least a few cells long; cobbles near the centre of the map,
    /// old asphalt further out (docs/map_elements/04_roads). The houses of the town then stand along them (<see cref="Features.Town"/>),
    /// the pavement beside them (<see cref="Features.Ground"/>).
    /// </summary>
    private static void Streets(GenMap map, System.Collections.Immutable.ImmutableArray<CellPos> path)
    {
        if (map.Settings.Climate != ClimateGrounds.Urban)
        {
            return;
        }

        var ground = ClimateGrounds.Of(map.Settings.Climate);
        var centre = map.N / 2;
        var cobble = map.N / CobbleShare;
        var outside = 0;
        foreach (var from in path)
        {
            if (map.StartDistanceSquared(from) <= (Starts.FlatRadius + 2) * (Starts.FlatRadius + 2) || ++outside % StreetStep != 0)
            {
                continue;
            }

            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                // Out across the road: past its other cells (a highway is three wide), then onto open ground.
                var street = new List<CellPos>();
                var cell = from;
                while (map.Contains(cell) && map.LandAt(cell) is LandType.Road or LandType.Highway)
                {
                    cell = new CellPos(cell.X + dx, cell.Y + dy);
                }

                while (street.Count < StreetLength && map.Contains(cell) && map.LandAt(cell) == LandType.Clear && !map.IsCliff(cell)
                       && !map.NearOre(cell, 1) && map.StartDistanceSquared(cell) > (Starts.FlatRadius + 2) * (Starts.FlatRadius + 2))
                {
                    street.Add(cell);
                    cell = new CellPos(cell.X + dx, cell.Y + dy);
                }

                if (street.Count < StreetMin)
                {
                    continue;
                }

                foreach (var at in street)
                {
                    var (cx, cy) = (at.X - centre, at.Y - centre);
                    var material = cx * cx + cy * cy <= cobble * cobble ? ground.Cobble : ground.Street;
                    foreach (var image in map.Symmetry.Images(at))
                    {
                        map.SetLand(image, LandType.Road);
                        map.Paint[image.Y * map.N + image.X] = material;
                    }
                }
            }
        }
    }

    // A street every this many cells along the main road outside the bases, this long at most and at least; cobbles within a sixth of
    // the grid of the centre, the old town.
    private const int StreetStep = 8;
    private const int StreetLength = 10;
    private const int StreetMin = 4;
    private const int CobbleShare = 6;

    private static bool Beside(GenMap map, CellPos cell, LandType land)
    {
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var next = new CellPos(cell.X + dx, cell.Y + dy);
            if (map.Contains(next) && map.LandAt(next) == land)
            {
                return true;
            }
        }

        return false;
    }
}
