using Rts.Data.Maps;
using Rts.Simulation.Geometry;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 2, starts (docs/implementation/M7_maps.md, 4.14), inside the playable area (a square in the screen's axes whose
/// corners are the middles of the grid's sides): the first player's start a fifth of the grid in from its west side, near the
/// top-left corner of the screen, for a mirror and for four players; at the left of the screen for 180°, five sixteenths in. Moved a little by the
/// seed; the others are its images. The ground within
/// <see cref="FlatRadius"/> cells of every start goes down to level 0, so every base stands on flat open ground.
/// </summary>
internal static class Starts
{
    // The owner's wish (2026-10-24): room for a whole base, about 25 cells across.
    public const int FlatRadius = 12;

    public static void Place(GenMap map)
    {
        var n = map.N;
        var jitter = (int)(IntNoise.Hash(0, 0, map.Seed, Channels.Starts) % (uint)(n / 16));
        var drift = (int)(IntNoise.Hash(1, 0, map.Seed, Channels.Starts) % (uint)(n / 8)) - n / 16;
        // For 180° the start lies along the diagonal of the grid, the long axis of the screen: about as far from the other as with
        // a mirror (55 to 72 cells on a map of 96), its base inside the playable area; the seed moves it by half as much.
        var first = map.Settings.Symmetry == MapSymmetry.Rotate180
            ? new CellPos(n * 5 / 16 + jitter / 2, n * 21 / 32 + drift / 2)
            : new CellPos(n / 5 + jitter, n / 2 - 1 + drift);
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
