using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rts.Client.Terrain;

/// <summary>A stone of a cliff: its centre on the ground, its size across (cells), its turn round the vertical (radians), its shape 0…2.</summary>
public readonly record struct CliffRock(Vector3 At, float Size, float Turn, int Variant);

/// <summary>
/// The stones of the cliffs (docs/map_elements/01_relief/appearance.md: scree at the foot, a broken edge on top): a cliff cell
/// is a wall in its middle (<see cref="TerrainHeights.CliffHeight"/>), and above and below the wall it shows level ground nobody can
/// drive onto. Stones there tell it is rock, as the cliff tiles of RA2 do: small ones along the rim, more and bigger ones at the
/// foot. Only a picture, the rules never see them. Pure math without Godot, the same stones for the same heights every time.
/// </summary>
public static class CliffRocks
{
    /// <summary>Spots tried per cell: a grid of this many across, each moved a little.</summary>
    public const int Tries = 4;

    /// <summary>How far round a spot the wall may start for a stone to lie there, in cells.</summary>
    public const float Reach = 0.16f;

    private const float LevelSlack = 0.005f;

    /// <summary>
    /// The stones of every cliff cell of the map (the skirt has none), but of the cells <paramref name="skip"/> keeps clear
    /// (bridge ends, where a stone would come through the deck).
    /// </summary>
    public static List<CliffRock> Place(TerrainHeights heights, Func<int, int, bool> skip)
    {
        var rocks = new List<CliffRock>();
        for (var cellZ = 0; cellZ < heights.HeightCells; cellZ++)
        {
            for (var cellX = 0; cellX < heights.WidthCells; cellX++)
            {
                if (heights.IsCliff(cellX, cellZ) && !skip(cellX, cellZ))
                {
                    PlaceInCell(heights, cellX, cellZ, rocks);
                }
            }
        }

        return rocks;
    }

    /// <summary>
    /// The stones scattered over level ground (<c>ClimateLook.Stones</c>: the rocks among RA2's snow, the owner, 2026-10-30): on
    /// about <paramref name="share"/> of the cells <paramref name="free"/> allows, a heap of one to three stones, smaller than the
    /// scree of a cliff. No cliff cell gets them, it has its own.
    /// </summary>
    public static List<CliffRock> Scatter(TerrainHeights heights, float share, Func<int, int, bool> free)
    {
        var stones = new List<CliffRock>();
        if (share <= 0f)
        {
            return stones;
        }

        for (var cellZ = 0; cellZ < heights.HeightCells; cellZ++)
        {
            for (var cellX = 0; cellX < heights.WidthCells; cellX++)
            {
                if (Hash(cellX, cellZ, 0, 6) >= share || heights.IsCliff(cellX, cellZ) || !free(cellX, cellZ))
                {
                    continue;
                }

                var count = 1 + (int)(Hash(cellX, cellZ, 0, 7) * 3f);
                for (var i = 0; i < count; i++)
                {
                    var x = cellX + 0.2f + 0.6f * Hash(cellX, cellZ, i, 8);
                    var z = cellZ + 0.2f + 0.6f * Hash(cellX, cellZ, i, 9);
                    var size = 0.3f + 0.3f * Hash(cellX, cellZ, i, 10);
                    stones.Add(new CliffRock(new Vector3(x, heights.HeightAt(x, z), z), size, Hash(cellX, cellZ, i, 11) * MathF.Tau, (int)(Hash(cellX, cellZ, i, 12) * 3f) % 3));
                }
            }
        }

        return stones;
    }

    private static void PlaceInCell(TerrainHeights heights, int cellX, int cellZ, List<CliffRock> rocks)
    {
        var a = heights.VertexHeight(cellX, cellZ);
        var b = heights.VertexHeight(cellX + 1, cellZ);
        var c = heights.VertexHeight(cellX, cellZ + 1);
        var d = heights.VertexHeight(cellX + 1, cellZ + 1);
        var low = MathF.Min(MathF.Min(a, b), MathF.Min(c, d));
        var high = MathF.Max(MathF.Max(a, b), MathF.Max(c, d));
        for (var i = 0; i < Tries * Tries; i++)
        {
            var x = cellX + (i % Tries + 0.15f + 0.7f * Hash(cellX, cellZ, i, 0)) / Tries;
            var z = cellZ + (i / Tries + 0.15f + 0.7f * Hash(cellX, cellZ, i, 1)) / Tries;
            var y = heights.HeightAt(x, z);
            var onTop = y >= high - LevelSlack;
            var atFoot = y <= low + LevelSlack;
            if (!(onTop || atFoot) || !NearWall(heights, x, z, y))
            {
                continue;
            }

            // The rim keeps fewer and smaller stones; the scree at the foot more, now and then a boulder.
            var chance = Hash(cellX, cellZ, i, 2);
            if (chance > (onTop ? 0.45f : 0.65f))
            {
                continue;
            }

            var grow = Hash(cellX, cellZ, i, 3);
            var size = onTop ? 0.3f + 0.25f * grow : chance < 0.08f ? 0.9f + 0.3f * grow : 0.35f + 0.45f * grow;
            rocks.Add(new CliffRock(new Vector3(x, y, z), size, Hash(cellX, cellZ, i, 4) * MathF.Tau, (int)(Hash(cellX, cellZ, i, 5) * 3f) % 3));
        }
    }

    // True when the ground drops or rises sharply within Reach of the point: the edge of the wall, on top or at its foot.
    private static bool NearWall(TerrainHeights heights, float x, float z, float y)
    {
        for (var k = 0; k < 8; k++)
        {
            var angle = k * MathF.PI / 4f;
            if (MathF.Abs(heights.HeightAt(x + Reach * MathF.Cos(angle), z + Reach * MathF.Sin(angle)) - y) > 0.15f)
            {
                return true;
            }
        }

        return false;
    }

    // 0…1 from a cell, a spot of it and what it is asked for.
    private static float Hash(int x, int z, int spot, int salt)
    {
        unchecked
        {
            var h = (uint)x * 374761393u + (uint)z * 668265263u + (uint)spot * 2246822519u + (uint)salt * 3266489917u;
            h = (h ^ (h >> 15)) * 2654435761u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
