using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Map;

/// <summary>Corner heights, cell shapes and point heights (docs/implementation/M7_maps.md, 4.1).</summary>
public sealed class CornerHeightGridTests
{
    /// <summary>A 1 x 1 map whose cell has the corners north-west, north-east, south-west, south-east.</summary>
    private static CornerHeightGrid Cell(int nw, int ne, int sw, int se) =>
        CornerHeightGrid.FromLayer(1, 1, [(byte)nw, (byte)ne, (byte)sw, (byte)se]);

    private static readonly CellPos Only = new(0, 0);

    [Fact]
    public void Shape_follows_the_rise_of_the_corners_for_every_combination()
    {
        int[] levels = [0, 1, 2, 3, 14];
        foreach (var nw in levels)
        foreach (var ne in levels)
        foreach (var sw in levels)
        foreach (var se in levels)
        {
            var rise = new[] { nw, ne, sw, se }.Max() - new[] { nw, ne, sw, se }.Min();
            var expected = rise == 0 ? CellShape.Flat : rise == 1 ? CellShape.Slope : CellShape.Cliff;
            Assert.Equal(expected, Cell(nw, ne, sw, se).ShapeAt(Only));
        }
    }

    [Theory]
    [InlineData(14, 14, 14, 14, CellShape.Flat)]
    [InlineData(3, 3, 3, 4, CellShape.Slope)]
    [InlineData(3, 4, 3, 4, CellShape.Slope)]
    [InlineData(3, 3, 3, 5, CellShape.Cliff)]
    [InlineData(0, 14, 0, 0, CellShape.Cliff)]
    public void Border_cases(int nw, int ne, int sw, int se, CellShape shape)
    {
        Assert.Equal(shape, Cell(nw, ne, sw, se).ShapeAt(Only));
    }

    [Theory]
    [InlineData(2, 3, 4, 5, 2)]
    [InlineData(6, 6, 5, 6, 5)]
    [InlineData(0, 0, 0, 0, 0)]
    public void The_cell_level_is_its_lowest_corner(int nw, int ne, int sw, int se, int level)
    {
        Assert.Equal(level, Cell(nw, ne, sw, se).CellLevel(Only));
    }

    [Fact]
    public void Corners_have_their_own_height()
    {
        var grid = Cell(1, 2, 3, 14);

        Assert.Equal(Fixed.FromInt(104), grid.HeightAt(new WorldPos(0, 0)));
        Assert.Equal(Fixed.FromInt(208), grid.HeightAt(new WorldPos(256, 0)));
        Assert.Equal(Fixed.FromInt(312), grid.HeightAt(new WorldPos(0, 256)));
        Assert.Equal(Fixed.FromInt(14 * 104), grid.HeightAt(new WorldPos(256, 256)));
    }

    [Fact]
    public void The_middle_of_a_ramp_is_half_way_up()
    {
        // North edge on level 0, south edge on level 1.
        var ramp = Cell(0, 0, 1, 1);

        Assert.Equal(Fixed.FromInt(52), ramp.HeightAt(new WorldPos(128, 128)));
        Assert.Equal(Fixed.FromInt(26), ramp.HeightAt(new WorldPos(40, 64)));
        Assert.Equal(Fixed.FromInt(104), Cell(0, 1, 1, 2).HeightAt(new WorldPos(128, 128)));
    }

    [Fact]
    public void Along_an_edge_the_height_is_linear_and_exact()
    {
        var grid = Cell(0, 4, 0, 4);

        for (var x = 0; x <= 256; x++)
        {
            // 4 levels over 256 leptons: x × 4 × 104 / 256 leptons, kept exactly in Q16.
            Assert.Equal(Fixed.FromRaw(x * 4L * 104 * 256), grid.HeightAt(new WorldPos(x, 0)));
        }
    }

    [Fact]
    public void Height_never_drops_going_up_a_ramp()
    {
        var grid = Cell(0, 1, 0, 1);
        var previous = grid.HeightAt(new WorldPos(0, 100));
        for (var x = 1; x <= 256; x++)
        {
            var height = grid.HeightAt(new WorldPos(x, 100));
            Assert.True(height >= previous);
            previous = height;
        }
    }

    [Fact]
    public void Points_beyond_the_map_take_the_edge_height()
    {
        var grid = CornerHeightGrid.FromLayer(2, 1, [1, 2, 3, 4, 5, 6]);

        Assert.Equal(grid.HeightAt(new WorldPos(0, 0)), grid.HeightAt(new WorldPos(-300, -10)));
        Assert.Equal(grid.HeightAt(new WorldPos(512, 256)), grid.HeightAt(new WorldPos(900, 700)));
        Assert.Equal(Fixed.FromInt(6 * 104), grid.HeightAt(new WorldPos(512, 256)));
    }

    [Fact]
    public void Levels_above_fourteen_and_wrong_sizes_are_refused()
    {
        Assert.Throws<ArgumentException>(() => CornerHeightGrid.FromLayer(1, 1, [0, 0, 0, 15]));
        Assert.Throws<ArgumentException>(() => CornerHeightGrid.FromLayer(2, 2, [0, 0, 0, 0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CornerHeightGrid(1, 1).SetCornerLevel(0, 0, 15));
    }

    [Fact]
    public void The_map_carries_its_heights_through_clone_compare_and_digest()
    {
        var map = new MapGrid(4, 4);
        var flat = map.Digest;
        map.SetCornerLevel(2, 2, 3);

        var copy = map.Clone();

        Assert.True(copy.ContentEquals(map));
        Assert.Equal(3, copy.Heights.CornerLevel(2, 2));
        Assert.NotEqual(flat, map.Digest);
        Assert.False(copy.ContentEquals(new MapGrid(4, 4)));
        map.SetCornerLevel(2, 2, 0);
        Assert.Equal(flat, map.Digest);
    }
}
