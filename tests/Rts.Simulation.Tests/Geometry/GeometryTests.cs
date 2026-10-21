using Rts.Simulation.Geometry;

namespace Rts.Simulation.Tests.Geometry;

public sealed class GeometryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 0)]
    [InlineData(256, 1)]
    [InlineData(-1, -1)]
    [InlineData(-256, -1)]
    [InlineData(-257, -2)]
    public void ToCell_rounds_toward_negative_infinity(int leptons, int expectedCell)
    {
        var cell = new WorldPos(leptons, leptons).ToCell();

        Assert.Equal(new CellPos(expectedCell, expectedCell), cell);
    }

    [Fact]
    public void Cell_origin_and_center()
    {
        Assert.Equal(new WorldPos(128, 128), new CellPos(0, 0).Center);
        Assert.Equal(new WorldPos(512, 768), new CellPos(2, 3).Origin);
        Assert.Equal(new WorldPos(-128, -128), new CellPos(-1, -1).Center);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, -3)]
    [InlineData(-7, 12)]
    public void Every_position_inside_a_cell_maps_back_to_it(int cellX, int cellY)
    {
        var cell = new CellPos(cellX, cellY);

        foreach (var dx in new[] { 0, 1, 127, 128, 255 })
        {
            foreach (var dy in new[] { 0, 64, 255 })
            {
                Assert.Equal(cell, (cell.Origin + new WorldPos(dx, dy)).ToCell());
            }
        }
    }

    [Fact]
    public void Vector_operations()
    {
        var a = new WorldPos(300, -40);
        var b = new WorldPos(100, 60);

        Assert.Equal(new WorldPos(400, 20), a + b);
        Assert.Equal(new WorldPos(200, -100), a - b);
        Assert.Equal(50_000L, (a - b).LengthSquared);
        Assert.Equal(2L * int.MaxValue * int.MaxValue, new WorldPos(int.MaxValue, int.MaxValue).LengthSquared);
    }
}
