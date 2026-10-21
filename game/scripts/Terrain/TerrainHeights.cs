using System;
using System.Numerics;

namespace Rts.Client.Terrain;

/// <summary>
/// Visual terrain heights (docs/implementation/M1_visual_spike.md, 4.4): one height per cell corner, (W+1)×(H+1) vertices,
/// in Godot units (one height level = 0.4). Pure math without Godot, so it is covered by xUnit.
/// The simulation stays flat in M1; heights become rules in M7.
/// </summary>
public sealed class TerrainHeights
{
    /// <summary>Visual height of one level (docs/implementation/M1_visual_spike.md, 4.2).</summary>
    public const float LevelHeight = 0.4f;

    private const float RayStep = 0.05f;
    private const int BisectIterations = 24;

    private readonly float[] _vertices;

    private TerrainHeights(int widthCells, int heightCells)
    {
        WidthCells = widthCells;
        HeightCells = heightCells;
        _vertices = new float[(widthCells + 1) * (heightCells + 1)];
    }

    public int WidthCells { get; }

    public int HeightCells { get; }

    public float MaxHeight { get; private set; }

    /// <summary>A flat map at height 0.</summary>
    public static TerrainHeights Flat(int widthCells, int heightCells) => new(widthCells, heightCells);

    /// <summary>
    /// The M1 showcase relief, set in code as the plan says (the map format comes in M7): five smooth hills of 2–5 levels,
    /// while the two base sites stay flat so the starting units never stand on a slope: the blue group occupies cells 20–24 × 20–22
    /// and the red group cells 44–48 × 44–46 (<c>SimulationHost</c>), so the flat circles are centred on those groups.
    /// </summary>
    public static TerrainHeights Showcase(int widthCells, int heightCells)
    {
        (float X, float Z, float Radius, float Levels)[] hills =
        [
            (12f, 42f, 9f, 4f),
            (40f, 12f, 8f, 3f),
            (54f, 32f, 10f, 5f),
            (30f, 54f, 7f, 2.5f),
            (6f, 8f, 6f, 2f),
        ];
        (float X, float Z)[] flatSites = [(22.5f, 22f), (46.5f, 46f)];
        const float FlatRadius = 6f;
        const float FlatBlend = 3f;

        var terrain = new TerrainHeights(widthCells, heightCells);
        for (var z = 0; z <= heightCells; z++)
        {
            for (var x = 0; x <= widthCells; x++)
            {
                var height = 0f;
                foreach (var hill in hills)
                {
                    var distance = MathF.Sqrt((x - hill.X) * (x - hill.X) + (z - hill.Z) * (z - hill.Z));
                    height += hill.Levels * LevelHeight * Bump(distance / hill.Radius);
                }

                foreach (var site in flatSites)
                {
                    var distance = MathF.Sqrt((x - site.X) * (x - site.X) + (z - site.Z) * (z - site.Z));
                    height *= SmoothStep(FlatRadius, FlatRadius + FlatBlend, distance);
                }

                terrain.SetVertex(x, z, height);
            }
        }

        return terrain;
    }

    /// <summary>Height at a cell corner; corners outside the map take the nearest edge.</summary>
    public float VertexHeight(int x, int z)
    {
        x = Math.Clamp(x, 0, WidthCells);
        z = Math.Clamp(z, 0, HeightCells);
        return _vertices[z * (WidthCells + 1) + x];
    }

    /// <summary>Height at any point: bilinear inside a cell, edge height outside the map.</summary>
    public float HeightAt(float x, float z)
    {
        x = Math.Clamp(x, 0f, WidthCells);
        z = Math.Clamp(z, 0f, HeightCells);
        var cellX = Math.Min((int)MathF.Floor(x), WidthCells - 1);
        var cellZ = Math.Min((int)MathF.Floor(z), HeightCells - 1);
        var fx = x - cellX;
        var fz = z - cellZ;

        var top = Lerp(VertexHeight(cellX, cellZ), VertexHeight(cellX + 1, cellZ), fx);
        var bottom = Lerp(VertexHeight(cellX, cellZ + 1), VertexHeight(cellX + 1, cellZ + 1), fx);
        return Lerp(top, bottom, fz);
    }

    /// <summary>Smooth vertex normal from central differences of the neighbouring heights.</summary>
    public Vector3 NormalAt(int x, int z)
    {
        var slopeX = (VertexHeight(x + 1, z) - VertexHeight(x - 1, z)) / (Math.Min(x + 1, WidthCells) - Math.Max(x - 1, 0));
        var slopeZ = (VertexHeight(x, z + 1) - VertexHeight(x, z - 1)) / (Math.Min(z + 1, HeightCells) - Math.Max(z - 1, 0));
        return Vector3.Normalize(new Vector3(-slopeX, 1f, -slopeZ));
    }

    /// <summary>
    /// Where a ray first meets the surface: steps down the ray from the highest point of the terrain, then refines the crossing
    /// by bisection. False only for rays that do not go down.
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

        var previous = start;
        for (var t = start + RayStep; t <= end; t += RayStep)
        {
            if (AboveSurface(origin, direction, t) <= 0f)
            {
                hit = origin + direction * Bisect(origin, direction, previous, t);
                return true;
            }

            previous = t;
        }

        hit = origin + direction * Bisect(origin, direction, previous, end);
        return true;
    }

    private void SetVertex(int x, int z, float height)
    {
        _vertices[z * (WidthCells + 1) + x] = height;
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

    // Smooth hill profile: 1 at the centre, 0 at the radius and beyond, zero slope at both ends.
    private static float Bump(float u) => u >= 1f ? 0f : (1f - u * u) * (1f - u * u);

    private static float SmoothStep(float from, float to, float value)
    {
        var t = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
