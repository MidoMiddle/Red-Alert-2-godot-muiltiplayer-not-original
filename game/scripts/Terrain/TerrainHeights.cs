using System;
using System.Numerics;

namespace Rts.Client.Terrain;

/// <summary>
/// Terrain heights for the view (docs/implementation/M1_visual_spike.md, 4.4; M7_maps.md, 4.12): one height per cell corner,
/// (W+1)×(H+1) vertices, in Godot units, always those of the rules (<see cref="FromRules"/>): what is drawn is what the units
/// drive on. The visual hills of M1 are gone since M7 step 9. Beyond the map lies the skirt (<see cref="MapSkirt"/>) when it is
/// built with one: <see cref="Skirt"/> more corners on every side, in world coordinates, so the rays of the camera find what
/// is drawn there. Pure math without Godot, so it is covered by xUnit.
/// <para>
/// A cliff cell (corners two levels apart or more, closed to every ground unit) is drawn as a wall, not a ramp
/// (docs/map_elements/01_relief/README.md): its height keeps to the lower or the upper level and drops in a steep band
/// round the middle that noise bends (<see cref="CliffHeight"/>). Nobody stands there, so only there the drawn height leaves
/// the rules'. On every cell edge the height depends on the edge alone (<see cref="EdgeHeight"/>), so cells meet without gaps.
/// </para>
/// </summary>
public sealed class TerrainHeights
{
    /// <summary>Height of one level of the rules in Godot units: 104 leptons of 256 per cell (docs/implementation/M7_maps.md, 4.1).</summary>
    public const float LevelHeight = Rts.Simulation.Map.CornerHeightGrid.LevelHeight / (float)Rts.Simulation.Geometry.WorldPos.LeptonsPerCell;

    /// <summary>Half the width of a cliff's wall, as a share of the drop across the cell: 0.14 makes a wall of about 80°.</summary>
    public const float CliffWallHalfWidth = 0.14f;

    /// <summary>How far noise moves a cliff's wall along the drop, as a share of it: the wall wanders up to a quarter cell.</summary>
    public const float CliffWander = 0.25f;

    /// <summary>Waves of the noise that bends a cliff's wall per cell.</summary>
    private const float CliffNoiseScale = 1.3f;

    private const float RayStep = 0.05f;
    private const int BisectIterations = 24;

    private readonly float[] _vertices;

    private TerrainHeights(int widthCells, int heightCells, int skirt = 0)
    {
        WidthCells = widthCells;
        HeightCells = heightCells;
        Skirt = skirt;
        _vertices = new float[(widthCells + 2 * skirt + 1) * (heightCells + 2 * skirt + 1)];
    }

    /// <summary>Cells of the map; the skirt is not counted.</summary>
    public int WidthCells { get; }

    public int HeightCells { get; }

    /// <summary>Cells of the skirt beyond every edge of the map; 0 without one.</summary>
    public int Skirt { get; }

    public float MaxHeight { get; private set; }

    /// <summary>A flat map at height 0.</summary>
    public static TerrainHeights Flat(int widthCells, int heightCells) => new(widthCells, heightCells);

    /// <summary>
    /// The relief of the rules (docs/implementation/M7_maps.md, 4.1): every corner at its level × 104 / 256 units, so the mesh,
    /// the units and the clicks stand where the simulation's heights put them (both blend the corners bilinearly).
    /// </summary>
    public static TerrainHeights FromRules(Rts.Simulation.Map.CornerHeightGrid heights)
    {
        var terrain = new TerrainHeights(heights.Width, heights.Height);
        for (var z = 0; z <= heights.Height; z++)
        {
            for (var x = 0; x <= heights.Width; x++)
            {
                terrain.SetVertex(x, z, heights.CornerLevel(x, z) * LevelHeight);
            }
        }

        return terrain;
    }

    /// <summary>The relief of the rules with its skirt round it: on the map the levels of the rules, beyond it those of the skirt.</summary>
    public static TerrainHeights FromRules(Rts.Simulation.Map.CornerHeightGrid heights, MapSkirt skirt)
    {
        var terrain = new TerrainHeights(heights.Width, heights.Height, skirt.Skirt);
        for (var z = -skirt.Skirt; z <= heights.Height + skirt.Skirt; z++)
        {
            for (var x = -skirt.Skirt; x <= heights.Width + skirt.Skirt; x++)
            {
                var onMap = x >= 0 && z >= 0 && x <= heights.Width && z <= heights.Height;
                terrain.SetVertex(x, z, (onMap ? heights.CornerLevel(x, z) : skirt.LevelAt(x, z)) * LevelHeight);
            }
        }

        return terrain;
    }

    /// <summary>Height at a cell corner, in world coordinates; corners beyond the skirt take its nearest edge.</summary>
    public float VertexHeight(int x, int z)
    {
        x = Math.Clamp(x, -Skirt, WidthCells + Skirt);
        z = Math.Clamp(z, -Skirt, HeightCells + Skirt);
        return _vertices[(z + Skirt) * (WidthCells + 2 * Skirt + 1) + x + Skirt];
    }

    /// <summary>
    /// Height at any point: bilinear inside a cell, a wall inside a cliff cell (<see cref="CliffHeight"/>), edge height beyond
    /// the skirt (beyond the map without one).
    /// </summary>
    public float HeightAt(float x, float z)
    {
        x = Math.Clamp(x, -Skirt, WidthCells + Skirt);
        z = Math.Clamp(z, -Skirt, HeightCells + Skirt);
        var cellX = Math.Min((int)MathF.Floor(x), WidthCells + Skirt - 1);
        var cellZ = Math.Min((int)MathF.Floor(z), HeightCells + Skirt - 1);
        var fx = x - cellX;
        var fz = z - cellZ;
        if (IsCliff(cellX, cellZ))
        {
            return CliffHeight(cellX, cellZ, fx, fz);
        }

        var top = Lerp(VertexHeight(cellX, cellZ), VertexHeight(cellX + 1, cellZ), fx);
        var bottom = Lerp(VertexHeight(cellX, cellZ + 1), VertexHeight(cellX + 1, cellZ + 1), fx);
        return Lerp(top, bottom, fz);
    }

    /// <summary>True when the corners of the cell are two levels apart or more: a cliff of the rules, drawn as a wall.</summary>
    public bool IsCliff(int cellX, int cellZ)
    {
        var (a, b, c, d) = Corners(cellX, cellZ);
        return IsDrop(Math.Min(Math.Min(a, b), Math.Min(c, d)), Math.Max(Math.Max(a, b), Math.Max(c, d)));
    }

    /// <summary>
    /// Height of a point of a cliff cell, <paramref name="fx"/> and <paramref name="fz"/> 0…1 inside it. On its edges it is
    /// <see cref="EdgeHeight"/>. A cell of two levels only (a straight cliff, an outer or an inner corner) blends its corners and
    /// pushes the blend to the lower or the upper level by <see cref="Wall"/>, so the wall follows the line where the blend is
    /// half way. A cell of three levels or more keeps the blend but moves the point along every edge that drops the way that
    /// edge's wall does, so its edges stay those of its neighbours.
    /// </summary>
    public float CliffHeight(int cellX, int cellZ, float fx, float fz)
    {
        var (a, b, c, d) = Corners(cellX, cellZ);
        if (fz == 0f)
        {
            return EdgeHeight(a, b, fx, cellX + fx, cellZ);
        }

        if (fz == 1f)
        {
            return EdgeHeight(c, d, fx, cellX + fx, cellZ + 1);
        }

        if (fx == 0f)
        {
            return EdgeHeight(a, c, fz, cellX, cellZ + fz);
        }

        if (fx == 1f)
        {
            return EdgeHeight(b, d, fz, cellX + 1, cellZ + fz);
        }

        var low = Math.Min(Math.Min(a, b), Math.Min(c, d));
        var high = Math.Max(Math.Max(a, b), Math.Max(c, d));
        var (x, z) = (cellX + fx, cellZ + fz);
        if (IsLowOrHigh(a) && IsLowOrHigh(b) && IsLowOrHigh(c) && IsLowOrHigh(d))
        {
            var blend = Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fz);
            return low + (high - low) * Wall((blend - low) / (high - low), x, z);
        }

        var u = Lerp(EdgeShift(a, b, fx, x, cellZ), EdgeShift(c, d, fx, x, cellZ + 1), fz);
        var v = Lerp(EdgeShift(a, c, fz, cellX, z), EdgeShift(b, d, fz, cellX + 1, z), fx);
        return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);

        bool IsLowOrHigh(float corner) => corner == low || corner == high;
    }

    /// <summary>
    /// Height along a cell edge from corner height <paramref name="from"/> to <paramref name="to"/>, <paramref name="s"/> 0…1 from
    /// the first, at world point (<paramref name="x"/>, <paramref name="z"/>): a straight line, or a wall where the edge drops two
    /// levels or more. Both cells of the edge get the same numbers, so the same height.
    /// </summary>
    public static float EdgeHeight(float from, float to, float s, float x, float z) => Lerp(from, to, EdgeShift(from, to, s, x, z));

    /// <summary>
    /// The wall's push, 0…1, at <paramref name="t"/> 0…1 up the drop (0 at the lower level) at world point
    /// (<paramref name="x"/>, <paramref name="z"/>): a smoothstep <see cref="CliffWallHalfWidth"/> wide round the middle, which
    /// noise moves up to <see cref="CliffWander"/>. It keeps 0 and 1 where they are and never turns back.
    /// </summary>
    public static float Wall(float t, float x, float z)
    {
        var bent = t + CliffWander * 4f * t * (1f - t) * Noise(x * CliffNoiseScale, z * CliffNoiseScale);
        var k = Math.Clamp((bent - 0.5f + CliffWallHalfWidth) / (2f * CliffWallHalfWidth), 0f, 1f);
        return k * k * (3f - 2f * k);
    }

    // Where a point s of the way along an edge takes its height: the same s on a gentle edge, the wall's push on a drop.
    private static float EdgeShift(float from, float to, float s, float x, float z)
    {
        if (!IsDrop(Math.Min(from, to), Math.Max(from, to)))
        {
            return s;
        }

        return from < to ? Wall(s, x, z) : 1f - Wall(1f - s, x, z);
    }

    // Two levels or more between two heights: half a level of slack against rounding.
    private static bool IsDrop(float low, float high) => high - low > 1.5f * LevelHeight;

    private (float A, float B, float C, float D) Corners(int cellX, int cellZ) =>
        (VertexHeight(cellX, cellZ), VertexHeight(cellX + 1, cellZ), VertexHeight(cellX, cellZ + 1), VertexHeight(cellX + 1, cellZ + 1));

    // Smooth value noise, -1…1, on a lattice of whole numbers: the same numbers give the same noise.
    private static float Noise(float x, float z)
    {
        var ix = (int)MathF.Floor(x);
        var iz = (int)MathF.Floor(z);
        var fx = x - ix;
        var fz = z - iz;
        fx = fx * fx * (3f - 2f * fx);
        fz = fz * fz * (3f - 2f * fz);
        var top = Lerp(Hash(ix, iz), Hash(ix + 1, iz), fx);
        var bottom = Lerp(Hash(ix, iz + 1), Hash(ix + 1, iz + 1), fx);
        return Lerp(top, bottom, fz) * 2f - 1f;
    }

    // 0…1 from a lattice point.
    private static float Hash(int x, int z)
    {
        unchecked
        {
            var h = (uint)x * 374761393u + (uint)z * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    /// <summary>
    /// Where a ray first comes down onto the surface: steps down the ray, then refines the crossing by bisection. False for rays
    /// that do not go down, and for rays that start under the ground and never come down onto it: such a ray has no visible
    /// crossing, and its origin is not a point of the ground.
    /// </summary>
    public bool TryIntersectRay(Vector3 origin, Vector3 direction, out Vector3 hit)
    {
        hit = default;
        if (direction.Y >= 0f)
        {
            return false;
        }

        direction = Vector3.Normalize(direction);
        var top = MaxHeight + 0.01f;
        var start = origin.Y > top ? (origin.Y - top) / -direction.Y : 0f;
        var end = origin.Y / -direction.Y + RayStep; // past the lowest possible surface, height 0

        // The first step from above the surface to on or below it. Starting under the surface, the ray has no such step
        // until it climbs out, so its origin is never reported as the hit.
        var previous = start;
        var above = AboveSurface(origin, direction, start) > 0f;
        for (var t = start + RayStep; t <= end; t += RayStep)
        {
            var aboveNow = AboveSurface(origin, direction, t) > 0f;
            if (above && !aboveNow)
            {
                hit = origin + direction * Bisect(origin, direction, previous, t);
                return true;
            }

            above = aboveNow;
            previous = t;
        }

        return false;
    }

    private void SetVertex(int x, int z, float height)
    {
        _vertices[(z + Skirt) * (WidthCells + 2 * Skirt + 1) + x + Skirt] = height;
        MaxHeight = Math.Max(MaxHeight, height);
    }

    // Positive while the ray point is above the surface.
    private float AboveSurface(Vector3 origin, Vector3 direction, float t)
    {
        var point = origin + direction * t;
        return point.Y - HeightAt(point.X, point.Z);
    }

    private float Bisect(Vector3 origin, Vector3 direction, float above, float below)
    {
        for (var i = 0; i < BisectIterations; i++)
        {
            var middle = (above + below) / 2f;
            if (AboveSurface(origin, direction, middle) > 0f)
            {
                above = middle;
            }
            else
            {
                below = middle;
            }
        }

        return (above + below) / 2f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
