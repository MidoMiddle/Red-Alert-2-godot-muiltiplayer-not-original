using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Map;

public sealed class MapGridTests
{
    private static readonly Locomotor[] AllLocomotors = [Locomotor.Foot, Locomotor.Track, Locomotor.Wheel];

    [Fact]
    public void New_grid_is_clear_land_without_obstacles()
    {
        var grid = new MapGrid(5, 3);

        Assert.Equal((5, 3), (grid.Width, grid.Height));
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(LandType.Clear, grid.LandAt(new CellPos(x, y)));
                Assert.False(grid.IsBlocked(new CellPos(x, y)));
            }
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 5)]
    [InlineData(5, -2)]
    public void Invalid_size_is_rejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapGrid(width, height));
    }

    [Theory]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(5, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(4, 2, true)]
    [InlineData(0, 0, true)]
    public void Bounds_are_checked(int x, int y, bool inside)
    {
        var grid = new MapGrid(5, 3);
        var cell = new CellPos(x, y);

        Assert.Equal(inside, grid.Contains(cell));
        if (!inside)
        {
            Assert.All(AllLocomotors, locomotor => Assert.False(grid.CanEnter(cell, locomotor)));
            Assert.Equal(0, grid.SpeedPercent(cell, Locomotor.Foot));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.LandAt(cell));
        }
    }

    [Fact]
    public void Speed_follows_the_land_type_of_the_cell()
    {
        var grid = new MapGrid(9, 1);
        var lands = Enum.GetValues<LandType>();
        for (var x = 0; x < lands.Length; x++)
        {
            grid.SetLand(new CellPos(x, 0), lands[x]);
        }

        for (var x = 0; x < lands.Length; x++)
        {
            foreach (var locomotor in AllLocomotors)
            {
                var expected = LocomotorRules.SpeedPercent(locomotor, lands[x]);
                Assert.Equal(expected, grid.SpeedPercent(new CellPos(x, 0), locomotor));
                Assert.Equal(expected > 0, grid.CanEnter(new CellPos(x, 0), locomotor));
            }
        }
    }

    [Fact]
    public void A_tree_blocks_every_locomotor_until_it_is_gone()
    {
        var grid = new MapGrid(3, 3);
        var cell = new CellPos(1, 1);
        grid.SetLand(cell, LandType.Road);

        grid.SetBlocked(cell, true);
        Assert.All(AllLocomotors, locomotor => Assert.False(grid.CanEnter(cell, locomotor)));

        grid.SetBlocked(cell, false);
        Assert.All(AllLocomotors, locomotor => Assert.True(grid.CanEnter(cell, locomotor)));
        Assert.Equal(120, grid.SpeedPercent(cell, Locomotor.Wheel));
    }

    [Fact]
    public void Cells_are_indexed_by_x_then_y()
    {
        // A non-square map catches a swapped index.
        var grid = new MapGrid(4, 2);
        grid.SetLand(new CellPos(3, 0), LandType.Rock);
        grid.SetBlocked(new CellPos(0, 1), true);

        Assert.Equal(LandType.Rock, grid.LandAt(new CellPos(3, 0)));
        Assert.Equal(LandType.Clear, grid.LandAt(new CellPos(0, 1)));
        Assert.True(grid.IsBlocked(new CellPos(0, 1)));
        Assert.False(grid.IsBlocked(new CellPos(1, 0)));
        Assert.Equal(LandType.Rock, grid.LandCells[3]);
        Assert.True(grid.BlockedCells[4]);
    }

    [Fact]
    public void Unknown_land_type_cannot_be_set()
    {
        var grid = new MapGrid(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetLand(new CellPos(0, 0), (LandType)0));
    }
}
