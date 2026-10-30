using Rts.Client.Terrain;
using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Client;

/// <summary>The stones of the cliffs: on the rim and at the foot of the wall, never on it, more at the foot, the same every time.</summary>
public sealed class CliffRocksTests
{
    // 16 x 8 cells: north of corner row 4 at level 4, south of it 0, so row 3 is a straight cliff; a gentle ramp in row 6.
    private static TerrainHeights Cliff()
    {
        var levels = new byte[17 * 9];
        for (var z = 0; z <= 8; z++)
        {
            for (var x = 0; x <= 16; x++)
            {
                levels[z * 17 + x] = (byte)(z <= 3 ? 4 : z == 8 ? 1 : 0);
            }
        }

        return TerrainHeights.FromRules(CornerHeightGrid.FromLayer(16, 8, levels));
    }

    [Fact]
    public void Stones_lie_on_the_rim_and_at_the_foot_of_the_wall_never_on_it()
    {
        var terrain = Cliff();
        var top = 4 * TerrainHeights.LevelHeight;

        var stones = CliffRocks.Place(terrain, (_, _) => false);

        Assert.NotEmpty(stones);
        foreach (var stone in stones)
        {
            Assert.Equal(3, (int)stone.At.Z);
            Assert.True(stone.At.Y <= 0.005f || MathF.Abs(stone.At.Y - top) <= 0.005f, $"a stone on the wall at {stone.At}");
            Assert.Equal(terrain.HeightAt(stone.At.X, stone.At.Z), stone.At.Y);
            Assert.InRange(stone.Size, 0.3f, 1.2f);
            Assert.InRange(stone.Variant, 0, 2);
        }

        // Fewer stones on the rim than in the scree at the foot, and smaller ones.
        var rim = stones.Where(stone => stone.At.Y > 1f).ToList();
        var foot = stones.Where(stone => stone.At.Y < 1f).ToList();
        Assert.InRange(rim.Count, 1, foot.Count - 1);
        Assert.True(rim.Max(stone => stone.Size) < foot.Max(stone => stone.Size));
        Assert.Equal(stones, CliffRocks.Place(terrain, (_, _) => false));
    }

    [Fact]
    public void Ramps_and_skipped_cells_get_no_stones()
    {
        var terrain = Cliff();

        Assert.DoesNotContain(CliffRocks.Place(terrain, (_, _) => false), stone => (int)stone.At.Z != 3);
        Assert.DoesNotContain(CliffRocks.Place(terrain, (x, _) => x < 8), stone => stone.At.X < 8f);
        Assert.Empty(CliffRocks.Place(terrain, (_, _) => true));
    }
}
