using System.Numerics;
using Rts.Client.Terrain;
using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Client;

/// <summary>The terrain mesh of M7 step 9: four triangles per cell, chunks without gaps, rebuilt where heights change.</summary>
public sealed class TerrainMeshTests
{
    [Fact]
    public void A_chunk_has_a_vertex_per_corner_and_cell_and_four_triangles_per_cell()
    {
        var mesh = TerrainMesh.BuildChunk(TerrainHeights.FromRules(TerrainHeightsTests.Relief()), 32, 0);

        Assert.Equal(33 * 33 + 32 * 32, mesh.Positions.Length);
        Assert.Equal(32 * 32 * 12, mesh.Indices.Length);
        Assert.Equal(new Vector2(32f, 0f), mesh.WorldUv[0]);
        Assert.Equal(new Vector2(0.5f, 0f), mesh.MapUv[0]);
    }

    [Fact]
    public void Corners_are_the_rules_and_centres_the_mean_of_their_corners()
    {
        var rules = TerrainHeightsTests.Relief();
        var terrain = TerrainHeights.FromRules(rules);
        var mesh = TerrainMesh.BuildChunk(terrain, 32, 0);

        foreach (var position in mesh.Positions)
        {
            Assert.Equal(TerrainHeightsTests.RulesHeight(rules, position.X, position.Z), position.Y, 4);
        }

        // On the stepped hill, the centre of a cell sits at the height the rules give the middle of the cell.
        var centre = mesh.Positions.Single(p => p.X == 39.5f && p.Z == 23.5f);
        Assert.Equal(terrain.HeightAt(39.5f, 23.5f), centre.Y, 5);
    }

    [Fact]
    public void Neighbouring_chunks_meet_without_gaps_even_at_the_ragged_edge()
    {
        // 70 x 40: the last chunks are 6 cells wide and 8 tall.
        var levels = new byte[71 * 41];
        for (var i = 0; i < levels.Length; i++)
        {
            levels[i] = (byte)(i * 7 % 5);
        }

        var terrain = TerrainHeights.FromRules(CornerHeightGrid.FromLayer(70, 40, levels));
        var vertices = new Dictionary<(float, float), float>();
        foreach (var (x, z) in TerrainMesh.Chunks(70, 40))
        {
            foreach (var position in TerrainMesh.BuildChunk(terrain, x, z).Positions)
            {
                if (vertices.TryGetValue((position.X, position.Z), out var other))
                {
                    Assert.Equal(other, position.Y);
                }

                vertices[(position.X, position.Z)] = position.Y;
            }
        }

        Assert.Equal(new[] { (0, 0), (32, 0), (64, 0), (0, 32), (32, 32), (64, 32) }, TerrainMesh.Chunks(70, 40));
        foreach (var ((x, z), y) in vertices)
        {
            // Every point is on the surface, so the grid of a cliff cell lies on the straight edge of a cell beside it.
            Assert.Equal(terrain.HeightAt(x, z), y, 4);
        }
    }

    [Fact]
    public void A_cliff_cell_is_a_grid_of_squares_and_the_other_cells_four_triangles()
    {
        // Relief's cliff runs down column 10: 32 cliff cells in the first chunk.
        var terrain = TerrainHeights.FromRules(TerrainHeightsTests.Relief());
        var mesh = TerrainMesh.BuildChunk(terrain, 0, 0);
        const int steps = TerrainMesh.CliffSteps;

        Assert.True(terrain.IsCliff(10, 5));
        Assert.Equal(33 * 33 + 32 * 32 + 32 * (steps + 1) * (steps + 1), mesh.Positions.Length);
        Assert.Equal((32 * 32 - 32) * 12 + 32 * steps * steps * 6, mesh.Indices.Length);
        foreach (var position in mesh.Positions)
        {
            Assert.Equal(terrain.HeightAt(position.X, position.Z), position.Y, 4);
        }
    }

    [Fact]
    public void Every_triangle_has_the_winding_of_godot_front_faces()
    {
        var mesh = TerrainMesh.BuildChunk(TerrainHeights.FromRules(TerrainHeightsTests.Relief()), 0, 0);

        for (var i = 0; i < mesh.Indices.Length; i += 3)
        {
            var (a, b, c) = (mesh.Positions[mesh.Indices[i]], mesh.Positions[mesh.Indices[i + 1]], mesh.Positions[mesh.Indices[i + 2]]);
            var normal = Vector3.Cross(b - a, c - a);
            Assert.True(normal.Y < 0f, $"triangle {i / 3} winds the other way");
        }
    }

    [Fact]
    public void Only_the_chunks_around_a_changed_corner_are_rebuilt()
    {
        var before = TerrainHeightsTests.Relief();
        CornerHeightGrid Changed(int x, int z)
        {
            var levels = before.LevelBytes();
            levels[z * 65 + x] = 9;
            return CornerHeightGrid.FromLayer(64, 64, levels);
        }

        var old = TerrainHeights.FromRules(before);

        Assert.Empty(TerrainMesh.ChangedChunks(old, TerrainHeights.FromRules(before)));
        Assert.Equal(new[] { (32, 32) }, TerrainMesh.ChangedChunks(old, TerrainHeights.FromRules(Changed(50, 50))));
        Assert.Equal(new[] { (0, 0), (32, 0), (0, 32), (32, 32) }, TerrainMesh.ChangedChunks(old, TerrainHeights.FromRules(Changed(32, 32))));
        Assert.Equal(new[] { (32, 0), (32, 32) }, TerrainMesh.ChangedChunks(old, TerrainHeights.FromRules(Changed(64, 32))));
    }
}
