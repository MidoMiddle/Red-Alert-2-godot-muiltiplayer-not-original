using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 3, water (docs/implementation/M7_maps.md, 4.14). Rivers: with a mirror, straight down the axis between the players,
/// two to four cells wide by the noise of the row; with turns, an arm that wanders from the north edge to the centre, three
/// cells wide, and its images — one river through the centre for 180°, a cross for 90°. The ground under a river goes down to
/// level 0. Lakes: flat lowland at level 0 where their noise is high. No water within <see cref="StartClearance"/> cells of a
/// start. Then the depth (deep where all four neighbours are water) and a beach round the water.
/// </summary>
internal static class Water
{
    public const int StartClearance = 9;
    private const int LakePercent = 22;

    public static void Rivers(GenMap map, bool[] riverMask)
    {
        var n = map.N;
        if (map.Settings.Symmetry == MapSymmetry.Mirror)
        {
            for (var y = 0; y < n; y++)
            {
                var half = IntNoise.At(0, y, map.Seed, Channels.River) > IntNoise.Max / 2 ? 2 : 1;
                for (var x = n / 2 - half; x < n / 2 + half; x++)
                {
                    MakeRiver(map, new CellPos(x, y), riverMask);
                }
            }

            return;
        }

        var amplitude = n / 10;
        for (var y = 0; y < n / 2; y++)
        {
            // The arm wanders less and less towards the centre, where it meets its images.
            var wander = (IntNoise.At(0, y, map.Seed, Channels.River) - IntNoise.Max / 2) * amplitude / (IntNoise.Max / 2);
            var centre = n / 2 + wander * (n / 2 - 1 - y) / (n / 2);
            for (var x = centre - 1; x <= centre + 1; x++)
            {
                for (var k = 0; k < map.Symmetry.Order; k++)
                {
                    MakeRiver(map, map.Symmetry.Cell(k, new CellPos(x, y)), riverMask);
                }
            }
        }
    }

    public static void Lakes(GenMap map)
    {
        for (var y = 0; y < map.N; y++)
        {
            for (var x = 0; x < map.N; x++)
            {
                var cell = new CellPos(x, y);
                var canonical = map.Symmetry.CanonicalCell(cell);
                if (IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Lakes) > IntNoise.Max * (100 - LakePercent) / 100
                    && map.IsFlat(cell) && map.Corner(x, y) == 0 && FarFromStarts(map, cell))
                {
                    map.SetLand(cell, LandType.Shallow);
                }
            }
        }
    }

    /// <summary>Deep water where all four neighbours are water too; a beach on the clear land next to water.</summary>
    public static void Shores(GenMap map)
    {
        var land = (LandType[])map.Land.Clone();
        for (var y = 0; y < map.N; y++)
        {
            for (var x = 0; x < map.N; x++)
            {
                var cell = new CellPos(x, y);
                if (IsWater(land, map.N, x, y))
                {
                    var surrounded = IsWater(land, map.N, x - 1, y) && IsWater(land, map.N, x + 1, y) && IsWater(land, map.N, x, y - 1) && IsWater(land, map.N, x, y + 1);
                    map.SetLand(cell, surrounded ? LandType.Deep : LandType.Shallow);
                    continue;
                }

                var nearWater = false;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        nearWater |= IsWater(land, map.N, x + dx, y + dy);
                    }
                }

                if (nearWater && land[y * map.N + x] == LandType.Clear)
                {
                    map.SetLand(cell, LandType.Beach);
                }
            }
        }
    }

    public static bool FarFromStarts(GenMap map, CellPos cell)
    {
        foreach (var start in map.Starts)
        {
            var (dx, dy) = (cell.X - start.Cell.X, cell.Y - start.Cell.Y);
            if (dx * dx + dy * dy < StartClearance * StartClearance)
            {
                return false;
            }
        }

        return true;
    }

    private static void MakeRiver(GenMap map, CellPos cell, bool[] riverMask)
    {
        if (!map.Contains(cell) || !FarFromStarts(map, cell))
        {
            return;
        }

        map.SetLand(cell, LandType.Shallow);
        for (var dy = 0; dy <= 1; dy++)
        {
            for (var dx = 0; dx <= 1; dx++)
            {
                map.SetCorner(cell.X + dx, cell.Y + dy, 0);
            }
        }

        // The banks around the river are softened into slopes down to the water.
        for (var dy = -3; dy <= 4; dy++)
        {
            for (var dx = -3; dx <= 4; dx++)
            {
                var (x, y) = (cell.X + dx, cell.Y + dy);
                if (x >= 0 && y >= 0 && x <= map.N && y <= map.N)
                {
                    riverMask[y * (map.N + 1) + x] = true;
                }
            }
        }
    }

    private static bool IsWater(LandType[] land, int n, int x, int y) =>
        x >= 0 && y >= 0 && x < n && y < n && land[y * n + x] is LandType.Shallow or LandType.Deep;
}
