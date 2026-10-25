using System.Collections.Generic;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 3, water (docs/implementation/M7_maps.md, 4.14). Rivers: with a mirror, straight down the axis between the players,
/// two to four cells wide by the noise of the row; with turns, an arm that wanders along the diagonal from the north-west corner
/// of the grid to the centre — from the top of the screen down — about three cells wide, and its images: one river down the
/// middle of the screen for 180°, a cross for 90°. The ground under a river goes down to
/// level 0. Lakes: flat lowland at level 0 where their noise is high, not within <see cref="FieldClearance"/> cells of a contested
/// ore field (the bigger maps put one in a lake). The sea: along every side of the playable area and beyond it, about a tenth
/// of the grid wide with a coastline that wanders by its noise, so every base lies near the coast and a fleet can reach it; out in
/// it, between the players, an islet at sea level in a bay with oil derricks — one islet with a pair for a mirror, else one derrick
/// on each image of it (the owner, 2026-10-25). No water
/// within <see cref="StartClearance"/> cells of a start. Islands (<see cref="Islands"/>): a raised island for every player. Then the
/// depth (deep where all four neighbours are water) and a beach round the water.
/// </summary>
internal static class Water
{
    public const int StartClearance = Starts.FlatRadius + 2;

    /// <summary>The level of the top of an island: cliffs of four levels down to the sea.</summary>
    public const int IslandLevel = 4;

    // The sea between two islands before their coasts wander; how far a coast wanders in or out.
    private const int IslandGap = 16;
    private const int IslandWander = 3;

    // A ramp: the cells either side of its axis, and how far its foot runs out past the cliff at sea level, a beach.
    private const int RampHalfWidth = 3;
    private const int RampFoot = 4;

    // An islet out at sea with an oil derrick on it: its radius; and the radius of the bay round an islet of the sea.
    private const int IsletRadius = 3;
    private const int BayRadius = 9;
    private const int LakePercent = 22;

    /// <summary>How far a lake keeps from the centre of a contested ore field.</summary>
    public const int FieldClearance = Ore.ContestedRadius + 4;

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
                    MakeWater(map, new CellPos(x, y), riverMask);
                }
            }

            return;
        }

        // Along the diagonal s = x + z, across it d = x − z: the arm is the band |d − wander(s)| ≤ 2 for s up to the centre.
        var amplitude = n / 10;
        for (var y = 0; y <= n / 2; y++)
        {
            for (var x = 0; x <= n / 2; x++)
            {
                // Up to the centre and one row past it, so the arm and its images meet without a dry cell between them.
                var s = x + y;
                if (s > n - 1)
                {
                    continue;
                }

                // The arm wanders less and less towards the centre, where it meets its images.
                var wander = (IntNoise.At(0, s, map.Seed, Channels.River) - IntNoise.Max / 2) * amplitude / (IntNoise.Max / 2);
                var across = wander * (n - 1 - s) / (n - 1);
                if (IntMath.Abs(x - y - across) <= 2)
                {
                    for (var k = 0; k < map.Symmetry.Order; k++)
                    {
                        MakeWater(map, map.Symmetry.Cell(k, new CellPos(x, y)), riverMask);
                    }
                }
            }
        }
    }

    public static void Sea(GenMap map, bool[] waterMask)
    {
        var n = map.N;
        // In steps of the area's axes (MapArea.Margin): a cell is about 1.4 of them. A band of width w round a square of side n takes
        // 1 − (1 − 2w/n)² of it: for a share p, w = n (1 − √(1 − p)) / 2.
        var percent = map.Settings.SeaPercent;
        var width = percent == 0 ? n / 10 : (int)((n * 1000L - n * IntMath.Sqrt(1_000_000L - percent * 10_000L)) / 2000);
        var wander = IntMath.Max(1, width / 4);
        var (islets, canonicalIslets) = SeaIslets(map, width * 2 / 3);
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var cell = new CellPos(x, y);
                var canonical = map.Symmetry.CanonicalCell(cell);
                var coast = width + (IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Sea) - IntNoise.Max / 2) * wander / (IntNoise.Max / 2);
                if (!FarFrom(islets, cell, IsletRadius + 1))
                {
                    // The islet: flat land at sea level, its banks marked for the slopes.
                    MakeWater(map, cell, waterMask);
                    map.SetLand(cell, LandType.Clear);
                }
                else if (map.Area.Margin(cell) < coast || !FarFrom(islets, cell, BayRadius))
                {
                    MakeWater(map, cell, waterMask);
                }
            }
        }

        Derricks(map, canonicalIslets);
    }

    // The islets of the sea: from the centre towards the middle of the way between the first start and its neighbour — when that is
    // the centre itself, square to the way between the two (one islet the mirror keeps, two the half turn) — out to where the area's
    // margin is <paramref name="margin"/>.
    private static (CellPos[] All, CellPos[] Canonical) SeaIslets(GenMap map, int margin)
    {
        var n = map.N;
        var start = map.Starts[0].Cell;
        var neighbour = Neighbour(map, start);
        var (mx, my) = (start.X + neighbour.X - n, start.Y + neighbour.Y - n);
        (int X, int Y)[] directions = IntMath.Abs(mx) + IntMath.Abs(my) < n / 8
            ? [(start.Y - neighbour.Y, neighbour.X - start.X)]
            : [(mx, my)];
        var canonical = new List<CellPos>();
        foreach (var (dx, dy) in directions)
        {
            var distance = 0;
            while (distance < n && map.Area.Margin(Off(n, dx, dy, distance + 1)) >= margin)
            {
                distance++;
            }

            var islet = Off(n, dx, dy, distance);
            if (!canonical.Exists(other => Images(map, other).Contains(islet)))
            {
                canonical.Add(islet);
            }
        }

        var all = new List<CellPos>();
        foreach (var islet in canonical)
        {
            foreach (var image in Images(map, islet))
            {
                if (!all.Contains(image))
                {
                    all.Add(image);
                }
            }
        }

        return ([.. all], [.. canonical]);
    }

    private static List<CellPos> Images(GenMap map, CellPos cell)
    {
        var images = new List<CellPos>();
        for (var k = 0; k < map.Symmetry.Order; k++)
        {
            images.Add(map.Symmetry.Cell(k, cell));
        }

        return images;
    }

    // The start nearest to the first one.
    private static CellPos Neighbour(GenMap map, CellPos start)
    {
        var neighbour = start;
        var nearest = long.MaxValue;
        foreach (var other in map.Starts)
        {
            var (dx, dy) = ((long)other.Cell.X - start.X, (long)other.Cell.Y - start.Y);
            if (other.Cell != start && dx * dx + dy * dy < nearest)
            {
                (nearest, neighbour) = (dx * dx + dy * dy, other.Cell);
            }
        }

        return neighbour;
    }

    // An oil derrick on every islet and its images; an islet the symmetry maps onto itself gets a pair, side by side.
    private static void Derricks(GenMap map, CellPos[] canonicalIslets)
    {
        foreach (var islet in canonicalIslets)
        {
            var own = Images(map, islet).TrueForAll(image => IntMath.Abs(image.X - islet.X) + IntMath.Abs(image.Y - islet.Y) <= 1);
            var derrick = new StructurePlacement("tech_oil_derrick", own ? new CellPos(islet.X - 2, islet.Y - 1) : new CellPos(islet.X - 1, islet.Y - 1), 0, 0);
            for (var k = 0; k < map.Symmetry.Order; k++)
            {
                // The north-west cell of the turned footprint of every image is its smallest corner after the turn.
                var (minX, minY) = (int.MaxValue, int.MaxValue);
                foreach (var cell in MapChecks.Footprint(derrick))
                {
                    var image = map.Symmetry.Cell(k, cell);
                    (minX, minY) = (IntMath.Min(minX, image.X), IntMath.Min(minY, image.Y));
                }

                var placement = derrick with { Cell = new CellPos(minX, minY) };
                if (!map.Structures.Contains(placement))
                {
                    map.Structures.Add(placement);
                }
            }
        }
    }

    /// <summary>
    /// The owner's wish (2026-10-25): an island for every player — the land within half the way to the nearest other start, less
    /// <see cref="IslandGap"/>, its coast wandering by the noise, with the contested ore field of the start on it too. Its top is
    /// flat at <see cref="IslandLevel"/>, cliffs round it, and two ramps a fleet can land at: 60° either side of the way to the centre
    /// the cliff opens into a slope <see cref="RampHalfWidth"/> × 2 + 1 cells wide, down to a strip of sea-level land where the
    /// beach is. Everything else is sea but a few islets at sea level with an oil derrick each (the owner, the same day): two either
    /// side of the centre between two players, four in the lagoon amid four — an engineer comes by transport. The first island's
    /// shape, and its images for the others.
    /// </summary>
    public static void Islands(GenMap map)
    {
        var n = map.N;
        var start = map.Starts[0].Cell;
        var nearest = long.MaxValue;
        foreach (var other in map.Starts)
        {
            var (dx, dy) = ((long)other.Cell.X - start.X, (long)other.Cell.Y - start.Y);
            nearest = other.Cell == start ? nearest : IntMath.Min(nearest, dx * dx + dy * dy);
        }

        var radius = (int)IntMath.Sqrt(nearest) / 2 - IslandGap / 2;
        var (_, contested) = Ore.Centres(start, n);
        var fields = new CellPos[map.Symmetry.Order];
        for (var k = 0; k < fields.Length; k++)
        {
            fields[k] = map.Symmetry.Cell(k, contested);
        }

        var land = new bool[n * n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var cell = new CellPos(x, y);
                var canonical = map.Symmetry.CanonicalCell(cell);
                var r = radius + (IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Sea) - IntNoise.Max / 2) * IslandWander / (IntNoise.Max / 2);
                land[y * n + x] = map.StartDistanceSquared(cell) < r * r || !FarFrom(fields, cell, Ore.ContestedRadius + 3);
            }
        }

        // The ramps of the first island, in doubled coordinates towards the centre as the ore does, turned by ±60° (cos ½, sin √3/2 ≈ 222/256).
        var foot = new bool[n * n];
        var slope = new bool[n * n];
        var (cx, cy) = ((long)n - (2 * start.X + 1), (long)n - (2 * start.Y + 1));
        foreach (var side in new[] { -1, 1 })
        {
            var (dx, dy) = ((cx * 128) - (side * cy * 222), (side * cx * 222) + (cy * 128));
            var length = IntMath.Sqrt((dx * dx) + (dy * dy));
            var edge = 0;
            while (Inside(map, land, Along(start, dx, dy, length, edge)))
            {
                edge++;
            }

            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var (ox, oy) = ((long)x - start.X, (long)y - start.Y);
                    var along = ((ox * dx) + (oy * dy)) / length;
                    var across = IntMath.Abs(((ox * dy) - (oy * dx)) / length);
                    if (across <= RampHalfWidth && along >= edge - 1 && along <= edge + RampFoot)
                    {
                        foot[y * n + x] = true;
                    }
                    else if (across <= RampHalfWidth && along >= edge - 2 - IslandLevel && along < edge - 1)
                    {
                        slope[y * n + x] = true;
                    }
                }
            }

            map.Landing ??= Along(start, dx, dy, length, edge + RampFoot - 1);
        }

        // The islets, at sea level as the feet of the ramps.
        var (islets, canonicalIslets) = Islets(map, start);
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                if (!FarFrom(islets, new CellPos(x, y), IsletRadius + 1))
                {
                    foot[y * n + x] = true;
                }
            }
        }

        // The images of the ramps: the foot is land at sea level, the slope's corners go down to it.
        var mask = new bool[map.Levels.Length];
        var isFoot = new bool[n * n];
        for (var i = 0; i < n * n; i++)
        {
            if (!foot[i] && !slope[i])
            {
                continue;
            }

            for (var k = 0; k < map.Symmetry.Order; k++)
            {
                var image = map.Symmetry.Cell(k, new CellPos(i % n, i / n));
                land[image.Y * n + image.X] = true;
                isFoot[image.Y * n + image.X] |= foot[i];
                for (var c = 0; c < 4; c++)
                {
                    mask[(image.Y + c / 2) * (n + 1) + image.X + c % 2] = true;
                }
            }
        }

        // A corner is on the top when every cell round it is land and none the foot of a ramp; else at sea level.
        for (var y = 0; y <= n; y++)
        {
            for (var x = 0; x <= n; x++)
            {
                var top = true;
                for (var c = 0; c < 4 && top; c++)
                {
                    var (ux, uy) = (x - 1 + c % 2, y - 1 + c / 2);
                    top = ux >= 0 && uy >= 0 && ux < n && uy < n && land[uy * n + ux] && !isFoot[uy * n + ux];
                }

                map.SetCorner(x, y, top ? IslandLevel : 0);
            }
        }

        // The sea; and a cell of the coast left at sea level under the cliff with no way up — a landing that goes nowhere — goes under too.
        for (var i = 0; i < n * n; i++)
        {
            var (x, y) = (i % n, i / n);
            if (!land[i] || (!isFoot[i] && map.Corner(x, y) == 0 && map.Corner(x + 1, y) == 0 && map.Corner(x, y + 1) == 0 && map.Corner(x + 1, y + 1) == 0))
            {
                map.SetLand(new CellPos(x, y), LandType.Shallow);
            }
        }

        Relief.Soften(map, mask);
        Derricks(map, canonicalIslets);
    }

    // The centres of the islets: every image of the canonical ones, each once; and the canonical ones.
    private static (CellPos[] All, CellPos[] Canonical) Islets(GenMap map, CellPos start)
    {
        var n = map.N;
        var neighbour = Neighbour(map, start);

        // Between two starts across the centre: a sixth of the grid off it, either side. Between four: in the lagoon amid the islands,
        // a twelfth of the grid from the centre towards each strait — the straits themselves are where the ramps come down.
        var (mx, my) = (start.X + neighbour.X - n, start.Y + neighbour.Y - n);
        CellPos[] canonical = IntMath.Abs(mx) + IntMath.Abs(my) < n / 8
            ? [Off(n, start.Y - n / 2, n / 2 - start.X, n / 6), Off(n, n / 2 - start.Y, start.X - n / 2, n / 6)]
            : [Off(n, mx, my, n / 12)];
        var all = new List<CellPos>();
        foreach (var cell in canonical)
        {
            for (var k = 0; k < map.Symmetry.Order; k++)
            {
                var image = map.Symmetry.Cell(k, cell);
                if (!all.Contains(image))
                {
                    all.Add(image);
                }
            }
        }

        return ([.. all], canonical);
    }

    // The cell <paramref name="distance"/> cells from the centre of the grid in the direction (dx, dy).
    private static CellPos Off(int n, int dx, int dy, int distance)
    {
        var length = IntMath.Max(1, (int)IntMath.Sqrt(((long)dx * dx) + ((long)dy * dy)));
        return new CellPos(n / 2 + dx * distance / length, n / 2 + dy * distance / length);
    }

    // The cell <paramref name="step"/> cells from the start along the direction.
    private static CellPos Along(CellPos start, long dx, long dy, long length, int step) =>
        new((int)(start.X + IntMath.DivRound(dx * step, length)), (int)(start.Y + IntMath.DivRound(dy * step, length)));

    private static bool Inside(GenMap map, bool[] land, CellPos cell) => map.Contains(cell) && land[cell.Y * map.N + cell.X];

    public static void Lakes(GenMap map)
    {
        var (_, contested) = Ore.Centres(map.Starts[0].Cell, map.N);
        var fields = new CellPos[map.Symmetry.Order];
        for (var k = 0; k < fields.Length; k++)
        {
            fields[k] = map.Symmetry.Cell(k, contested);
        }

        for (var y = 0; y < map.N; y++)
        {
            for (var x = 0; x < map.N; x++)
            {
                var cell = new CellPos(x, y);
                var canonical = map.Symmetry.CanonicalCell(cell);
                if (IntNoise.At(canonical.X, canonical.Y, map.Seed, Channels.Lakes) > IntNoise.Max * (100 - LakePercent) / 100
                    && map.IsFlat(cell) && map.Corner(x, y) == 0 && FarFromStarts(map, cell) && FarFrom(fields, cell, FieldClearance))
                {
                    map.SetLand(cell, LandType.Shallow);
                }
            }
        }
    }

    /// <summary>
    /// Deep water where all four neighbours are water too; a beach on the clear land next to water — not on a cliff over it when
    /// <paramref name="beachOnCliffs"/> is false (islands: their beaches are at the feet of the ramps).
    /// </summary>
    public static void Shores(GenMap map, bool beachOnCliffs = true)
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

                if (nearWater && land[y * map.N + x] == LandType.Clear && (beachOnCliffs || !map.IsCliff(cell)))
                {
                    map.SetLand(cell, LandType.Beach);
                }
            }
        }
    }

    private static bool FarFrom(CellPos[] centres, CellPos cell, int clearance)
    {
        foreach (var centre in centres)
        {
            var (dx, dy) = (cell.X - centre.X, cell.Y - centre.Y);
            if (dx * dx + dy * dy < clearance * clearance)
            {
                return false;
            }
        }

        return true;
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

    // A cell of a river or the sea: shallow water at level 0, its banks marked for the slopes down to it.
    private static void MakeWater(GenMap map, CellPos cell, bool[] riverMask)
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
