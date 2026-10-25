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

        // The main road of a town is asphalt; elsewhere it is the unpaved road of the climate (docs/map_elements/04_roads).
        var kind = map.Settings.Climate == ClimateGrounds.Urban ? LandType.Road : LandType.Track;
        var grid = map.Grid();
        var path = AStar.FindPath(grid, start, target, Locomotor.Wheel, map.N * map.N);
        foreach (var cell in path.Cells)
        {
            for (var k = 0; k < map.Symmetry.Order; k++)
            {
                var image = map.Symmetry.Cell(k, cell);
                if (!map.IsWater(image))
                {
                    map.SetLand(image, kind);
                }
            }
        }
    }
}
