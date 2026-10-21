using System.Numerics;
using Rts.Client.Terrain;

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

    [Fact]
    public void Showcase_has_hills_and_flat_base_sites()
    {
        var terrain = TerrainHeights.Showcase(MapSize, MapSize);

        Assert.True(terrain.MaxHeight > 0.8f, $"max height {terrain.MaxHeight}");
        // Every corner of every cell the starting units stand on (SimulationHost: cells 20–24 × 20–22 and 44–48 × 44–46), with a margin.
        for (var z = 19; z <= 24; z++)
        {
            for (var x = 19; x <= 26; x++)
            {
                Assert.Equal(0f, terrain.VertexHeight(x, z), 4);
                Assert.Equal(0f, terrain.VertexHeight(x + 24, z + 24), 4);
            }
        }
    }

    [Fact]
    public void Showcase_slopes_are_gentle_and_heights_not_negative()
    {
        var terrain = TerrainHeights.Showcase(MapSize, MapSize);

        for (var z = 0; z <= MapSize; z++)
        {
            for (var x = 0; x <= MapSize; x++)
            {
                var height = terrain.VertexHeight(x, z);
                Assert.True(height >= 0f);
                Assert.True(MathF.Abs(height - terrain.VertexHeight(x + 1, z)) <= TerrainHeights.LevelHeight, $"cliff at ({x}, {z})");
                Assert.True(MathF.Abs(height - terrain.VertexHeight(x, z + 1)) <= TerrainHeights.LevelHeight, $"cliff at ({x}, {z})");
            }
        }
    }

    [Fact]
    public void HeightAt_matches_vertices_and_is_bilinear_between_them()
    {
        var terrain = TerrainHeights.Showcase(MapSize, MapSize);

        Assert.Equal(terrain.VertexHeight(54, 32), terrain.HeightAt(54f, 32f), 5);
        var corners = (terrain.VertexHeight(50, 30) + terrain.VertexHeight(51, 30) + terrain.VertexHeight(50, 31) + terrain.VertexHeight(51, 31)) / 4f;
        Assert.Equal(corners, terrain.HeightAt(50.5f, 30.5f), 5);
        var edge = (terrain.VertexHeight(50, 30) + terrain.VertexHeight(51, 30)) / 2f;
        Assert.Equal(edge, terrain.HeightAt(50.5f, 30f), 5);
    }

    [Fact]
    public void Heights_outside_the_map_take_the_edge()
    {
        var terrain = TerrainHeights.Showcase(MapSize, MapSize);

        Assert.Equal(terrain.HeightAt(0f, 10f), terrain.HeightAt(-5f, 10f));
        Assert.Equal(terrain.VertexHeight(MapSize, MapSize), terrain.VertexHeight(MapSize + 3, MapSize + 3));
    }

    [Fact]
    public void Normals_point_up_on_flat_ground_and_away_from_a_hilltop_on_slopes()
    {
        var terrain = TerrainHeights.Showcase(MapSize, MapSize);

        Assert.Equal(Vector3.UnitY, terrain.NormalAt(22, 22));

        // East slope of the hill at (54, 32): the normal leans east, away from the top.
        var slope = terrain.NormalAt(59, 32);
        Assert.True(slope.X > 0.05f && slope.Y > 0.8f, $"normal {slope}");
        Assert.Equal(1f, slope.Length(), 4);
    }

    [Fact]
    public void Camera_rays_hit_the_surface_where_they_should()
    {
        var terrain = TerrainHeights.Showcase(MapSize, MapSize);
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
