using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>A snowdrift (docs/map_elements/20_snowdrift): it slows foot, tracks and wheels by its percent, tracks crush it.</summary>
public sealed class SnowdriftTests
{
    /// <summary>A 16 x <paramref name="height"/> field of clear ground with a snowdrift on every cell of column 8.</summary>
    private static GameWorld Drifted(int height = 9)
    {
        var objects = Enumerable.Range(0, height).Select(y => MapObjectState.New(MapObjectKind.Snowdrift, new CellPos(8, y), 0)).ToList();
        return new GameWorld(seed: 1, MapGrid.FromLayers(16, height, new byte[16 * height].Select(_ => (byte)LandType.Clear).ToArray(), new byte[16 * height], default, objects), TestRules.Standard);
    }

    [Fact]
    public void A_snowdrift_slows_foot_tracks_and_wheels_by_its_percent_and_nothing_else()
    {
        var map = Drifted().Map;
        var drift = new CellPos(8, 4);
        var clear = new CellPos(2, 4);

        Assert.Equal(60, map.SpeedPercent(drift, Locomotor.Foot));
        Assert.Equal(80, map.SpeedPercent(drift, Locomotor.Track));
        Assert.Equal(50, map.SpeedPercent(drift, Locomotor.Wheel));
        Assert.Equal(map.SpeedPercent(clear, Locomotor.Amphibious), map.SpeedPercent(drift, Locomotor.Amphibious));
        Assert.Equal(map.SpeedPercent(clear, Locomotor.Hover), map.SpeedPercent(drift, Locomotor.Hover));
        Assert.True(map.CanEnter(drift, Locomotor.Foot));
    }

    [Fact]
    public void Tracks_crush_the_snowdrift_and_infantry_does_not()
    {
        var world = Drifted();
        var drift = new CellPos(8, 4);
        var snowdrift = world.Map.ObjectsAt(drift).Single();

        world.CrushMapObjects(drift, Locomotor.Foot, new List<SimEvent>());
        Assert.False(snowdrift.Destroyed);
        Assert.Equal(60, world.Map.SpeedPercent(drift, Locomotor.Foot));

        world.CrushMapObjects(drift, Locomotor.Track, new List<SimEvent>());
        Assert.True(snowdrift.Destroyed);
        Assert.Equal(100, world.Map.SpeedPercent(drift, Locomotor.Foot));
        Assert.Equal(100, world.Map.SpeedPercent(drift, Locomotor.Track));
        Assert.Equal(100, world.Map.SpeedPercent(drift, Locomotor.Wheel));
    }

    [Fact]
    public void A_route_across_a_snowdrift_costs_more_than_across_clear_ground()
    {
        var drifted = Drifted().Map;
        var clear = MapGrid.FromLayers(16, 9, new byte[16 * 9].Select(_ => (byte)LandType.Clear).ToArray(), new byte[16 * 9], default, new List<MapObjectState>());
        var from = new CellPos(2, 4);
        var to = new CellPos(14, 4);

        var through = AStar.FindPath(drifted, from, to, Locomotor.Wheel);
        var free = AStar.FindPath(clear, from, to, Locomotor.Wheel);

        Assert.True(free.Cost > 0);
        Assert.True(through.Cost > free.Cost, $"Through the drift {through.Cost}, on clear ground {free.Cost}.");
    }

    [Fact]
    public void The_passability_cache_follows_crushing_and_equals_a_fresh_build()
    {
        var world = Drifted();
        world.CrushMapObjects(new CellPos(8, 3), Locomotor.Track, new List<SimEvent>());
        world.CrushMapObjects(new CellPos(8, 5), Locomotor.Track, new List<SimEvent>());

        var fresh = world.Map.Clone();
        for (var y = 0; y < world.Map.Height; y++)
        {
            for (var x = 0; x < world.Map.Width; x++)
            {
                foreach (var locomotor in Enum.GetValues<Locomotor>())
                {
                    var cell = new CellPos(x, y);
                    Assert.Equal(fresh.SpeedPercent(cell, locomotor), world.Map.SpeedPercent(cell, locomotor));
                }
            }
        }
    }
}
