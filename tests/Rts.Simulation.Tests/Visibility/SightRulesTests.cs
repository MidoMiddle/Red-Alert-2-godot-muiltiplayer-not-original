using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Visibility;

/// <summary>Height and sight (docs/implementation/M7_maps.md, 4.2, step 8).</summary>
public sealed class SightRulesTests
{
    /// <summary>20 x 20 of flat ground at level 0 with a flat square of <paramref name="level"/> from corner (6, 6) to (14, 14).</summary>
    private static MapGrid Plateau(int level)
    {
        var heights = new byte[21 * 21];
        for (var y = 6; y <= 14; y++)
        {
            for (var x = 6; x <= 14; x++)
            {
                heights[y * 21 + x] = (byte)level;
            }
        }

        var land = Enumerable.Repeat((byte)LandType.Clear, 400).ToArray();
        return MapGrid.FromLayers(20, 20, land, new byte[400], heights);
    }

    [Fact]
    public void Every_two_levels_add_a_cell_of_sight_up_to_four()
    {
        int[] expected = [0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 4, 4, 4, 4, 4];

        Assert.Equal(expected, Enumerable.Range(0, CornerHeightGrid.MaxLevel + 1).Select(SightRules.HeightBonus));
    }

    [Fact]
    public void A_unit_on_level_four_sees_two_cells_farther()
    {
        var world = new GameWorld(seed: 1, Plateau(4), TestRules.Standard);
        var below = world.SpawnUnit(new PlayerId(1), UnitType.HeavyTank, new CellPos(2, 2).Center);
        var above = world.SpawnUnit(new PlayerId(1), UnitType.HeavyTank, new CellPos(10, 10).Center);

        Assert.Equal(4, world.Map.Heights.CellLevel(new CellPos(10, 10)));
        Assert.Equal(8, SightRules.SightOf(below, world.Map));
        Assert.Equal(10, SightRules.SightOf(above, world.Map));
    }

    [Fact]
    public void A_slope_counts_by_its_foot_and_any_point_of_a_cell_counts_the_same()
    {
        // A square of level 1: its rim is a slope from 0 to 1 and counts as level 0.
        var gentle = Plateau(1);
        Assert.Equal(CellShape.Slope, gentle.Heights.ShapeAt(new CellPos(5, 10)));
        Assert.Equal(8, SightRules.SightAt(gentle, new CellPos(5, 10), 8));

        // Inside a square of level 2, every point of a cell gives the same radius.
        var high = Plateau(2);
        WorldPos[] points = [new(10 * 256 + 1, 10 * 256 + 1), new CellPos(10, 10).Center, new(11 * 256 - 1, 11 * 256 - 1)];
        Assert.All(points, point => Assert.Equal(9, SightRules.SightAt(high, point.ToCell(), 8)));
    }

    [Fact]
    public void Sight_is_a_disc_of_whole_cells()
    {
        Assert.True(SightRules.InSight(3, 0, 3));
        Assert.True(SightRules.InSight(0, -3, 3));
        Assert.False(SightRules.InSight(3, 1, 3));
        Assert.True(SightRules.InSight(2, 2, 3));

        Assert.Equal(new[] { 5, 13, 29 }, new[] { 1, 2, 3 }.Select(r =>
            (from dy in Enumerable.Range(-r, 2 * r + 1) from dx in Enumerable.Range(-r, 2 * r + 1) where SightRules.InSight(dx, dy, r) select 1).Count()));
    }

    [Theory]
    [InlineData(UnitType.Conscript, 5)]
    [InlineData(UnitType.Rocketeer, 5)]
    [InlineData(UnitType.Engineer, 4)]
    [InlineData(UnitType.Mcv, 6)]
    [InlineData(UnitType.Harvester, 4)]
    [InlineData(UnitType.SupportVehicle, 8)]
    [InlineData(UnitType.HeavyTank, 8)]
    [InlineData(UnitType.TeslaTank, 8)]
    public void Each_type_sees_as_far_as_its_ra2_counterpart(UnitType type, int sight)
    {
        Assert.Equal(sight, TestRules.Unit(type).Sight);
    }
}
