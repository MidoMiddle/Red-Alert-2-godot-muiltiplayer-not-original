using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Systems;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Spatial;

/// <summary>The spatial index of docs/implementation/M2_unit_control.md, 4.9, checked against a full scan.</summary>
public sealed class SpatialIndexTests
{
    private static readonly PlayerId Player1 = new(1);

    private static List<EntityId> ScanRadius(GameWorld world, WorldPos center, long radius) => world.Entities
        .OfType<Unit>()
        .Where(u => u.IsAlive && (u.Position - center).LengthSquared <= radius * radius)
        .Select(u => u.Id)
        .Order()
        .ToList();

    private static List<EntityId> ScanRect(GameWorld world, WorldPos a, WorldPos b) => world.Entities
        .OfType<Unit>()
        .Where(u => u.IsAlive
            && u.Position.X >= System.Math.Min(a.X, b.X) && u.Position.X <= System.Math.Max(a.X, b.X)
            && u.Position.Y >= System.Math.Min(a.Y, b.Y) && u.Position.Y <= System.Math.Max(a.Y, b.Y))
        .Select(u => u.Id)
        .Order()
        .ToList();

    private static List<EntityId> Radius(GameWorld world, WorldPos center, long radius)
    {
        var results = new List<EntityId>();
        world.Spatial.QueryRadius(world, center, radius, results);
        return results;
    }

    private static List<EntityId> Rect(GameWorld world, WorldPos a, WorldPos b)
    {
        var results = new List<EntityId>();
        world.Spatial.QueryRect(world, a, b, results);
        return results;
    }

    [Fact]
    public void Index_matches_a_full_scan_in_random_matches()
    {
        var random = new TestRandom(31);
        for (var scenario = 0; scenario < 50; scenario++)
        {
            // Sizes not divisible by 4 too: the last bucket row and column are partial.
            var width = 13 + random.NextInt(30);
            var height = 13 + random.NextInt(30);
            var world = new GameWorld(seed: 1, width, height);
            var events = new List<SimEvent>();
            uint sequence = 0;
            for (var tick = 0; tick < 60; tick++)
            {
                if (random.NextInt(2) == 0)
                {
                    var point = new WorldPos(random.NextInt(width * 256), random.NextInt(height * 256));
                    CommandProcessor.Apply(world, Spawn(1 + random.NextInt(2), tick, sequence++, point.X, point.Y, (UnitType)(1 + random.NextInt(8))));
                }

                var units = world.Entities.OfType<Unit>().ToList();
                if (units.Count > 0 && random.NextInt(3) == 0)
                {
                    var unit = units[random.NextInt(units.Count)];
                    var target = new WorldPos(random.NextInt(width * 256), random.NextInt(height * 256));
                    CommandProcessor.Apply(world, new MoveUnitsCommand(unit.Owner, tick, sequence++, [unit.Id], target));
                }

                if (units.Count > 3 && random.NextInt(10) == 0)
                {
                    units[random.NextInt(units.Count)].MarkDead();
                }

                world.PathRequests.Process(world, events);
                MovementSystem.Update(world, events);
                world.Spatial.Sync(world);
                world.RemoveDead();
                world.CurrentTick++;

                for (var q = 0; q < 20; q++)
                {
                    var center = new WorldPos(random.NextInt(width * 256 + 1000) - 500, random.NextInt(height * 256 + 1000) - 500);
                    long radius = random.NextInt(6 * 256);
                    Assert.Equal(ScanRadius(world, center, radius), Radius(world, center, radius));

                    var corner = new WorldPos(random.NextInt(width * 256 + 1000) - 500, random.NextInt(height * 256 + 1000) - 500);
                    Assert.Equal(ScanRect(world, center, corner), Rect(world, center, corner));
                }
            }

            Assert.Equal(world.Entities.Count, world.Spatial.Count);
        }
    }

    [Fact]
    public void Edge_cases_of_queries()
    {
        var world = new GameWorld(seed: 1, widthCells: 10, heightCells: 10);
        var a = world.SpawnUnit(Player1, UnitType.HeavyTank, new WorldPos(1024, 1024));
        var b = world.SpawnUnit(Player1, UnitType.HeavyTank, new WorldPos(1023, 1024));
        var c = world.SpawnUnit(Player1, UnitType.HeavyTank, new WorldPos(2400, 2400));

        Assert.Equal([a.Id], Radius(world, new WorldPos(1024, 1024), 0));
        Assert.Equal([a.Id, b.Id], Radius(world, new WorldPos(1024, 1024), 1));
        Assert.Empty(Radius(world, new WorldPos(-5000, -5000), 100));
        Assert.Equal([a.Id, b.Id, c.Id], Radius(world, new WorldPos(0, 0), 1_000_000));
        Assert.Equal(Rect(world, new WorldPos(0, 0), new WorldPos(1100, 1100)), Rect(world, new WorldPos(1100, 1100), new WorldPos(0, 0)));
        Assert.Equal([a.Id, b.Id], Rect(world, new WorldPos(1023, 1024), new WorldPos(1024, 1024)));
        Assert.Empty(Radius(world, new WorldPos(0, 0), -1));
    }

    [Fact]
    public void Unit_moves_between_buckets_only_when_it_crosses_a_border()
    {
        var world = new GameWorld(seed: 1, widthCells: 16, heightCells: 16);
        var unit = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(3, 1).Center);
        var westBucket = new WorldPos(0, 0);
        var westBucketEnd = new WorldPos(4 * 256 - 1, 4 * 256 - 1);
        var eastBucket = new WorldPos(4 * 256, 0);
        var eastBucketEnd = new WorldPos(8 * 256 - 1, 4 * 256 - 1);

        unit.Position = new WorldPos(4 * 256 - 1, unit.Position.Y);
        world.Spatial.Sync(world);
        Assert.Equal([unit.Id], Rect(world, westBucket, westBucketEnd));

        unit.Position = new WorldPos(4 * 256, unit.Position.Y);
        world.Spatial.Sync(world);
        Assert.Empty(Rect(world, westBucket, westBucketEnd));
        Assert.Equal([unit.Id], Rect(world, eastBucket, eastBucketEnd));
    }

    [Fact]
    public void Rebuild_gives_the_same_answers()
    {
        var world = new GameWorld(seed: 1, widthCells: 30, heightCells: 30);
        var random = new TestRandom(4);
        for (var i = 0; i < 60; i++)
        {
            world.SpawnUnit(Player1, UnitType.Conscript, new WorldPos(random.NextInt(30 * 256), random.NextInt(30 * 256)));
        }

        var before = Rect(world, new WorldPos(500, 500), new WorldPos(5000, 6000));
        world.Spatial.Rebuild(world);

        Assert.Equal(before, Rect(world, new WorldPos(500, 500), new WorldPos(5000, 6000)));
        Assert.Equal(60, world.Spatial.Count);
    }

    [Fact]
    public void Dead_units_leave_the_index()
    {
        var world = new GameWorld(seed: 1, widthCells: 10, heightCells: 10);
        var unit = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(2, 2).Center);

        unit.MarkDead();
        world.RemoveDead();

        Assert.Equal(0, world.Spatial.Count);
        Assert.Empty(Radius(world, new CellPos(2, 2).Center, 500));
    }
}
