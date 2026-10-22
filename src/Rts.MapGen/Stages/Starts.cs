using Rts.Data.Maps;
using Rts.Simulation.Geometry;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 2, starts (docs/implementation/M7_maps.md, 4.14): the first player's start a fifth of the map in from its side (left
/// half for a mirror, the north-west for turns), moved a little by the seed; the others are its images. The ground within
/// <see cref="FlatRadius"/> cells of every start goes down to level 0, so every base stands on flat open ground.
/// </summary>
internal static class Starts
{
    public const int FlatRadius = 7;

    public static void Place(GenMap map)
    {
        var n = map.N;
        var jitter = (int)(IntNoise.Hash(0, 0, map.Seed, Channels.Starts) % (uint)(n / 16));
        var drift = (int)(IntNoise.Hash(1, 0, map.Seed, Channels.Starts) % (uint)(n / 8)) - n / 16;
        var first = map.Settings.Symmetry == MapSymmetry.Mirror
            ? new CellPos(n / 5 + jitter, n / 2 - 1 + drift)
            : new CellPos(n / 5 + jitter, n / 5 + (jitter + drift + n) % (n / 16));
        for (var k = 0; k < map.Symmetry.Order; k++)
        {
            map.Starts.Add(new MapStart(k + 1, map.Symmetry.Cell(k, first)));
        }

        var disc = new bool[map.Levels.Length];
        foreach (var start in map.Starts)
        {
            Relief.MarkDisc(map, disc, start.Cell, FlatRadius);
        }

        for (var i = 0; i < disc.Length; i++)
        {
            if (disc[i])
            {
                map.Levels[i] = 0;
            }
        }
    }
}
