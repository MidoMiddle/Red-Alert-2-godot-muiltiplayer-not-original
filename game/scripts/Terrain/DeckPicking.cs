using System.Numerics;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// Where a ray from the camera meets the top of a high bridge's deck (docs/implementation/M7_maps.md, 4.6): the clicks on a bridge
/// over a valley land on the bridge, not on the valley floor behind it. Only standing segments count. Godot-free, so tests cover it.
/// </summary>
public static class DeckPicking
{
    /// <summary>
    /// The nearest point where the ray from <paramref name="origin"/> along <paramref name="direction"/> meets the top of a standing
    /// deck, nearer than <paramref name="limit"/> along the ray; false when it meets none.
    /// </summary>
    public static bool TryHit(MapGrid map, Vector3 origin, Vector3 direction, float limit, out Vector3 hit)
    {
        hit = default;
        if (map.DeckCount == 0 || direction.Y >= 0f)
        {
            return false;
        }

        var best = limit;
        for (var slot = 0; slot < map.DeckCount; slot++)
        {
            if (!map.DeckSegment(slot).IsIntact)
            {
                continue;
            }

            var top = map.DeckLevel(slot) * TerrainHeights.LevelHeight;
            var along = (top - origin.Y) / direction.Y;
            if (along < 0f || along >= best)
            {
                continue;
            }

            var point = origin + direction * along;
            var cell = map.DeckCell(slot);
            if (point.X >= cell.X && point.X < cell.X + 1 && point.Z >= cell.Y && point.Z < cell.Y + 1)
            {
                best = along;
                hit = point;
            }
        }

        return best < limit;
    }
}
