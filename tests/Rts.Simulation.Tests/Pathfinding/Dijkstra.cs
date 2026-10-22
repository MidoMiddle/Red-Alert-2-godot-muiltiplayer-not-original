using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;

namespace Rts.Simulation.Tests.Pathfinding;

/// <summary>
/// Reference answer for the A* tests, never used by the game: plain Dijkstra over the same graph and costs.
/// It expands in every direction, so it is slow, but its distances are optimal by construction.
/// </summary>
internal static class Dijkstra
{
    /// <summary>Cheapest cost from <paramref name="start"/> to every cell; -1 where unreachable.</summary>
    public static int[,] Distances(MapGrid map, CellPos start, Locomotor locomotor)
    {
        var distance = new int[map.Width, map.Height];
        var done = new bool[map.Width, map.Height];
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                distance[x, y] = -1;
            }
        }

        distance[start.X, start.Y] = 0;
        while (true)
        {
            // O(n²) minimum search: small maps only, and obviously correct.
            CellPos? current = null;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (!done[x, y] && distance[x, y] >= 0 && (current is null || distance[x, y] < distance[current.Value.X, current.Value.Y]))
                    {
                        current = new CellPos(x, y);
                    }
                }
            }

            if (current is not { } cell)
            {
                return distance;
            }

            done[cell.X, cell.Y] = true;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if ((dx == 0 && dy == 0) || !AStar.CanStep(map, cell, dx, dy, locomotor))
                    {
                        continue;
                    }

                    var next = new CellPos(cell.X + dx, cell.Y + dy);
                    var cost = distance[cell.X, cell.Y] + LandSpeeds.Reference.StepCost(locomotor, map.LandAt(next), dx != 0 && dy != 0);
                    if (distance[next.X, next.Y] < 0 || cost < distance[next.X, next.Y])
                    {
                        distance[next.X, next.Y] = cost;
                    }
                }
            }
        }
    }
}
