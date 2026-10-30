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
        var checkedPoints = 0;
        for (var i = 0; i < 1000; i++)
        {
            var x = random.NextInt(MapSize * 256) / 256f;
            var z = random.NextInt(MapSize * 256) / 256f;
            if (rules.ShapeAt(new CellPos((int)x, (int)z)) == CellShape.Cliff)
            {
                continue; // a wall, nobody stands there: see the cliff tests below
            }

            checkedPoints++;
            Assert.Equal(RulesHeight(rules, x, z), terrain.HeightAt(x, z), 4);
        }

        Assert.True(checkedPoints > 950);
    }

    /// <summary>16 x 8 cells: corners north of z = 4 at level 4, south of it level 0, so row 3 is a straight cliff.</summary>
    private static TerrainHeights StraightCliff()
    {
        var levels = new byte[17 * 9];
        for (var z = 0; z <= 8; z++)
        {
            for (var x = 0; x <= 16; x++)
            {
                levels[z * 17 + x] = (byte)(z <= 3 ? 4 : 0);
            }
        }

        return TerrainHeights.FromRules(CornerHeightGrid.FromLayer(16, 8, levels));
    }

    [Fact]
    public void A_cliff_is_a_steep_wall_with_the_plateau_above_it_and_the_ground_below()
    {
        var terrain = StraightCliff();
        var top = 4 * TerrainHeights.LevelHeight;

        Assert.True(terrain.IsCliff(5, 3));
        Assert.False(terrain.IsCliff(5, 2));
        Assert.False(terrain.IsCliff(5, 4));
        for (var x = 0.05f; x < 16f; x += 0.1f)
        {
            // The noise moves the wall a little, never into the outer fifth of the cell.
            Assert.Equal(top, terrain.HeightAt(x, 3.2f));
            Assert.Equal(0f, terrain.HeightAt(x, 3.8f));

            var steepest = 0f;
            for (var z = 3f; z < 4f; z += 0.01f)
            {
                var drop = terrain.HeightAt(x, z) - terrain.HeightAt(x, z + 0.01f);
                Assert.True(drop >= -0.0001f, $"the wall turns back up at ({x}, {z})");
                steepest = MathF.Max(steepest, drop / 0.01f);
            }

            Assert.True(steepest > MathF.Tan(75f * MathF.PI / 180f), $"the wall at x = {x} is only {MathF.Atan(steepest) * 180f / MathF.PI:0}°");
        }
    }

    [Fact]
    public void The_wall_wanders_along_the_cliff_instead_of_running_straight()
    {
        var terrain = StraightCliff();
        var middle = 2 * TerrainHeights.LevelHeight;

        // Where the wall crosses half its height, column by column.
        var crossings = new List<float>();
        for (var x = 0.05f; x < 16f; x += 0.25f)
        {
            var z = 3f;
            while (terrain.HeightAt(x, z) > middle)
            {
                z += 0.005f;
            }

            crossings.Add(z);
        }

        Assert.True(crossings.Max() - crossings.Min() > 0.1f, $"the wall runs between z = {crossings.Min()} and {crossings.Max()}");
    }

    [Fact]
    public void Cliff_cells_meet_their_neighbours_without_a_step()
    {
        // Random corners 0…6: walls of every kind next to ramps and flat cells.
        var random = new TestRandom(5);
        var levels = new byte[41 * 41];
        for (var i = 0; i < levels.Length; i++)
        {
            levels[i] = (byte)random.NextInt(7);
        }

        var terrain = TerrainHeights.FromRules(CornerHeightGrid.FromLayer(40, 40, levels));
        for (var i = 0; i < 4000; i++)
        {
            // A point on a cell edge, read from both cells beside it.
            var edge = 1 + random.NextInt(39);
            var along = random.NextInt(40 * 64) / 64f;
            Assert.InRange(terrain.HeightAt(edge - 0.00001f, along) - terrain.HeightAt(edge, along), -0.001f, 0.001f);
            Assert.InRange(terrain.HeightAt(along, edge - 0.00001f) - terrain.HeightAt(along, edge), -0.001f, 0.001f);
        }
    }

    [Fact]
    public void A_click_on_a_cliff_finds_the_wall()
    {
        var terrain = StraightCliff();
        var random = new TestRandom(9);

        // The camera looks north-west, so it sees the cliff's south face.
        for (var i = 0; i < 300; i++)
        {
            var x = 2f + random.NextInt(1200) / 100f;
            var z = 3f + random.NextInt(100) / 100f;
            var target = new Vector3(x, terrain.HeightAt(x, z), z);

            Assert.True(terrain.TryIntersectRay(target - CameraDirection * 50f, CameraDirection, out var hit));
            Assert.True(Vector3.Distance(target, hit) < 0.01f, $"hit {hit}, expected {target}");
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

    [Fact]
    public void A_ray_that_starts_under_the_ground_does_not_hit_its_origin()
    {
        var terrain = TerrainHeights.FromRules(Relief());

        // The ground here is at level 0, so this origin is one unit below it; the ray only goes further down.
        Assert.False(terrain.TryIntersectRay(new Vector3(20f, -1f, 10f), CameraDirection, out _));
    }
}
