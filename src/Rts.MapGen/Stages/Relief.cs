using Rts.Simulation.Math;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 1, heights (docs/implementation/M7_maps.md, 4.14): noise sampled at the canonical corner of every orbit, quantised by
/// the relief — flat 0; hilly 0…4; mountainous terraces 0, 2, 4, 6, 8 (cliffs between them). <see cref="Soften"/> lowers
/// corners to at most one level above their lowest neighbour, all at once (Jacobi), until nothing moves: the rule looks at
/// the eight neighbours the same way in every direction, so it keeps the map symmetric.
/// </summary>
internal static class Relief
{
    /// <summary>The share of corners where a ramp may cut into a terrace (mountainous relief).</summary>
    private const int RampPercent = 30;

    public static void Build(GenMap map)
    {
        if (map.Settings.Relief == ReliefKind.Flat)
        {
            return;
        }

        for (var y = 0; y <= map.N; y++)
        {
            for (var x = 0; x <= map.N; x++)
            {
                var (cx, cy) = map.Symmetry.CanonicalCorner(x, y);
                var step = IntNoise.At(cx, cy, map.Seed, Channels.Height) * 5 / (IntNoise.Max + 1);
                map.SetCorner(x, y, map.Settings.Relief == ReliefKind.Hilly ? step : step * 2);
            }
        }
    }

    /// <summary>The corners where a ramp may cut a terrace: the top <see cref="RampPercent"/> % of their own noise.</summary>
    public static bool[] RampMask(GenMap map)
    {
        var mask = new bool[(map.N + 1) * (map.N + 1)];
        for (var y = 0; y <= map.N; y++)
        {
            for (var x = 0; x <= map.N; x++)
            {
                var (cx, cy) = map.Symmetry.CanonicalCorner(x, y);
                mask[y * (map.N + 1) + x] = IntNoise.At(cx, cy, map.Seed, Channels.Ramps) > IntNoise.Max * (100 - RampPercent) / 100;
            }
        }

        return mask;
    }

    /// <summary>Lowers every corner in <paramref name="mask"/> (null: all) to at most its lowest neighbour + 1, all at once, until stable.</summary>
    public static void Soften(GenMap map, bool[]? mask)
    {
        var size = map.N + 1;
        var next = new int[map.Levels.Length];
        bool moved;
        do
        {
            moved = false;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var index = y * size + x;
                    var level = map.Levels[index];
                    if (mask is null || mask[index])
                    {
                        var lowest = int.MaxValue;
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                var (nx, ny) = (x + dx, y + dy);
                                if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < size && ny < size)
                                {
                                    lowest = IntMath.Min(lowest, map.Levels[ny * size + nx]);
                                }
                            }
                        }

                        level = IntMath.Min(level, lowest + 1);
                    }

                    next[index] = level;
                    moved |= level != map.Levels[index];
                }
            }

            System.Array.Copy(next, map.Levels, next.Length);
        }
        while (moved);
    }

    /// <summary>Marks the corners within <paramref name="radius"/> cells of the centre of <paramref name="cell"/> (doubled coordinates keep it symmetric).</summary>
    public static void MarkDisc(GenMap map, bool[] mask, Rts.Simulation.Geometry.CellPos cell, int radius) =>
        MarkDisc2(map, mask, 2 * cell.X + 1, 2 * cell.Y + 1, radius);

    /// <summary>Marks the corners within <paramref name="radius"/> cells of the point (<paramref name="cx"/>/2, <paramref name="cy"/>/2).</summary>
    public static void MarkDisc2(GenMap map, bool[] mask, int cx, int cy, int radius)
    {
        for (var y = 0; y <= map.N; y++)
        {
            for (var x = 0; x <= map.N; x++)
            {
                var (dx, dy) = (2 * x - cx, 2 * y - cy);
                if (dx * dx + dy * dy <= 4 * radius * radius)
                {
                    mask[y * (map.N + 1) + x] = true;
                }
            }
        }
    }
}
