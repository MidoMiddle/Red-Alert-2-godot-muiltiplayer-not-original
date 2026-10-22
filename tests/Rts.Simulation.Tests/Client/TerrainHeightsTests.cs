using System.Numerics;
using Rts.Client.Terrain;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Client;

public sealed class TerrainHeightsTests
{
    private const int MapSize = 64;

    // Direction of the RA2 camera's view: yaw 45°, pitch 30° down (docs/implementation/M1_visual_spike.md, 4.1).
    private static readonly Vector3 CameraDirection = Vector3.Normalize(new Vector3(
        -MathF.Sin(MathF.PI / 4f) * MathF.Cos(MathF.PI / 6f),
        -MathF.Sin(MathF.PI / 6f),
        -MathF.Cos(MathF.PI / 4f) * MathF.Cos(MathF.PI / 6f)));

    [Fact]
    public void Flat_terrain_is_at_zero_and_rays_hit_the_plane()
    {
        var terrain = TerrainHeights.Flat(MapSize, MapSize);
        var target = new Vector3(20.3f, 0f, 41.7f);

        Assert.Equal(0f, terrain.HeightAt(12.5f, 7.25f));
        Assert.True(terrain.TryIntersectRay(target - CameraDirection * 50f, CameraDirection, out var hit));
        Assert.True(Vector3.Distance(target, hit) < 0.01f, $"hit {hit}, expected {target}");
    }

    /// <summary>A 64 x 64 map of the rules with a stepped hill up to level 6 around corner (40, 24) and a cliff at x = 10.</summary>
    internal static CornerHeightGrid Relief()
    {
        var levels = new byte[(MapSize + 1) * (MapSize + 1)];
        for (var z = 0; z <= MapSize; z++)
        {
            for (var x = 0; x <= MapSize; x++)
            {
                var hill = 6 - System.Math.Max(System.Math.Abs(x - 40), System.Math.Abs(z - 24));
                levels[z * (MapSize + 1) + x] = (byte)System.Math.Max(System.Math.Max(hill, 0), x <= 10 ? 3 : 0);
            }
        }

        return CornerHeightGrid.FromLayer(MapSize, MapSize, levels);
    }

    /// <summary>The height of the rules at a point, in Godot units (cells).</summary>
    internal static float RulesHeight(CornerHeightGrid heights, float x, float z) =>
        heights.HeightAt(new WorldPos((int)(x * 256), (int)(z * 256))).Raw / (float)Fixed.OneRaw / WorldPos.LeptonsPerCell;

    [Fact]
    public void Heights_of_the_rules_are_drawn_where_the_simulation_puts_them()
    {
        var rules = Relief();
        var terrain = TerrainHeights.FromRules(rules);
        var random = new TestRandom(77);

        Assert.Equal(6 * TerrainHeights.LevelHeight, terrain.VertexHeight(40, 24));
        Assert.Equal(104f / 256f, TerrainHeights.LevelHeight);
        for (var i = 0; i < 1000; i++)
        {
            var x = random.NextInt(MapSize * 256) / 256f;
            var z = random.NextInt(MapSize * 256) / 256f;
            Assert.Equal(RulesHeight(rules, x, z), terrain.HeightAt(x, z), 4);
        }
    }

    [Fact]
    public void HeightAt_matches_vertices_and_is_bilinear_between_them()
    {
        var terrain = TerrainHeights.FromRules(Relief());

        Assert.Equal(terrain.VertexHeight(38, 24), terrain.HeightAt(38f, 24f), 5);
        var corners = (terrain.VertexHeight(37, 22) + terrain.VertexHeight(38, 22) + terrain.VertexHeight(37, 23) + terrain.VertexHeight(38, 23)) / 4f;
        Assert.Equal(corners, terrain.HeightAt(37.5f, 22.5f), 5);
        var edge = (terrain.VertexHeight(37, 22) + terrain.VertexHeight(38, 22)) / 2f;
        Assert.Equal(edge, terrain.HeightAt(37.5f, 22f), 5);
    }

    [Fact]
    public void Heights_outside_the_map_take_the_edge()
    {
        var terrain = TerrainHeights.FromRules(Relief());

        Assert.Equal(terrain.HeightAt(0f, 10f), terrain.HeightAt(-5f, 10f));
        Assert.Equal(terrain.VertexHeight(MapSize, MapSize), terrain.VertexHeight(MapSize + 3, MapSize + 3));
    }

    [Fact]
    public void Camera_rays_hit_the_surface_where_they_should()
    {
        var terrain = TerrainHeights.FromRules(Relief());
        var random = new TestRandom(1234);

        for (var i = 0; i < 1000; i++)
        {
            var x = random.NextInt(MapSize * 100) / 100f;
            var z = random.NextInt(MapSize * 100) / 100f;
            var target = new Vector3(x, terrain.HeightAt(x, z), z);

            Assert.True(terrain.TryIntersectRay(target - CameraDirection * 50f, CameraDirection, out var hit));
            Assert.True(Vector3.Distance(target, hit) < 0.01f, $"hit {hit}, expected {target}");
        }
    }

    [Fact]
    public void Rays_going_up_do_not_hit()
    {
        var terrain = TerrainHeights.Flat(MapSize, MapSize);

        Assert.False(terrain.TryIntersectRay(new Vector3(10f, 5f, 10f), new Vector3(0f, 1f, 0f), out _));
    }
}
