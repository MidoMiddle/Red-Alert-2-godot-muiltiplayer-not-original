using System;
using System.Numerics;

namespace Rts.Client.Terrain;

/// <summary>
/// Terrain heights for the view (docs/implementation/M1_visual_spike.md, 4.4; M7_maps.md, 4.12): one height per cell corner,
/// (W+1)×(H+1) vertices, in Godot units, always those of the rules (<see cref="FromRules"/>): what is drawn is what the units
/// drive on. The visual hills of M1 are gone since M7 step 9. Pure math without Godot, so it is covered by xUnit.
/// </summary>
public sealed class TerrainHeights
{
    /// <summary>Height of one level of the rules in Godot units: 104 leptons of 256 per cell (docs/implementation/M7_maps.md, 4.1).</summary>
    public const float LevelHeight = Rts.Simulation.Map.CornerHeightGrid.LevelHeight / (float)Rts.Simulation.Geometry.WorldPos.LeptonsPerCell;

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

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
