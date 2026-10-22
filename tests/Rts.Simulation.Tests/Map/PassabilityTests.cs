using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Map;

/// <summary>The passability cache of the map (docs/implementation/M7_maps.md, 4.4): slopes, cliffs, obstacles, local updates.</summary>
public sealed class PassabilityTests
{
    /// <summary>What the cache must hold for a cell, worked out from scratch.</summary>
    private static int Expected(MapGrid map, CellPos cell, Locomotor locomotor) =>
        map.IsBlocked(cell) ? 0 : LandSpeeds.Reference.CellPercent(locomotor, map.LandAt(cell), map.Heights.ShapeAt(cell));

    private static void AssertMatches(MapGrid map)
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                foreach (var locomotor in Enum.GetValues<Locomotor>())
                {
                    var cell = new CellPos(x, y);
                    Assert.Equal(Expected(map, cell, locomotor), map.SpeedPercent(cell, locomotor));
                }
            }
        }
    }

    [Fact]
    public void A_ramp_slows_a_cliff_closes_and_a_tree_closes_even_a_ramp()
    {
        var map = new MapGrid(4, 4);
        map.SetCornerLevel(2, 0, 1);
        map.SetCornerLevel(2, 1, 1);
        var ramp = new CellPos(1, 0);
        map.SetCornerLevel(4, 4, 2);
        var cliff = new CellPos(3, 3);

        Assert.Equal(CellShape.Slope, map.Heights.ShapeAt(ramp));
        Assert.Equal(75, map.SpeedPercent(ramp, Locomotor.Track));
        Assert.Equal(80, map.SpeedPercent(ramp, Locomotor.Foot));
        Assert.All(Enum.GetValues<Locomotor>(), locomotor => Assert.False(map.CanEnter(cliff, locomotor)));

        map.SetBlocked(ramp, true);
        Assert.Equal(0, map.SpeedPercent(ramp, Locomotor.Foot));
    }

    [Fact]
    public void The_cache_follows_any_series_of_changes()
    {
        var random = new TestRandom(73);
        for (var round = 0; round < 4; round++)
        {
            var width = 6 + random.NextInt(10);
            var height = 6 + random.NextInt(10);
            var map = new MapGrid(width, height);
            for (var change = 0; change < 200; change++)
            {
                switch (random.NextInt(3))
                {
                    case 0:
                        map.SetLand(new CellPos(random.NextInt(width), random.NextInt(height)), (LandType)(1 + random.NextInt(8)));
                        break;
                    case 1:
                        map.SetBlocked(new CellPos(random.NextInt(width), random.NextInt(height)), random.NextInt(4) == 0);
                        break;
                    default:
                        map.SetCornerLevel(random.NextInt(width + 1), random.NextInt(height + 1), random.NextInt(4));
                        break;
                }
            }

            AssertMatches(map);
            AssertMatches(map.Clone());
        }
    }

    [Fact]
    public void Outside_the_map_nobody_goes()
    {
        var map = new MapGrid(4, 4);

        Assert.Equal(0, map.SpeedPercent(new CellPos(-1, 0), Locomotor.Foot));
        Assert.Equal(0, map.SpeedPercent(new CellPos(0, 4), Locomotor.Hover));
    }
}
