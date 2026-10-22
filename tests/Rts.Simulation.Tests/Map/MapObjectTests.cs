using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>Map objects (docs/implementation/M7_maps.md, 4.7, step 5): blocking, damage, stumps, crushing.</summary>
public sealed class MapObjectTests
{
    private static readonly PlayerId Blue = new(1);

    /// <summary>A world of 16 x 9 with a wall of <paramref name="kind"/> down column 8, no gap.</summary>
    private static GameWorld Walled(MapObjectKind kind, int height = 9)
    {
        var objects = Enumerable.Range(0, height).Select(y => MapObjectState.New(kind, new CellPos(8, y), 0)).ToList();
        return new GameWorld(seed: 1, MapGrid.FromLayers(16, height, new byte[16 * height].Select(_ => (byte)LandType.Clear).ToArray(), new byte[16 * height], default, objects));
    }

    private static void Order(GameWorld world, Unit unit, CellPos target) =>
        CommandProcessor.Apply(world, new MoveCommand(unit.Owner, world.CurrentTick, 0, [unit.Id], target.Center));

    private static (List<SimEvent> Events, HashSet<CellPos> Visited) Run(GameWorld world, Unit unit)
    {
        var events = new List<SimEvent>();
        var visited = new HashSet<CellPos>();
        for (var ticks = 0; unit.MoveTarget is not null || unit.PathPending || unit.HoldTicks > 0; ticks++)
        {
            Assert.True(ticks < 3000, "The unit never stopped.");
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.RemoveDead();
            world.CurrentTick++;
            ReservationTests.AssertOccupancy(world);
            visited.Add(unit.Position.ToCell());
        }

        return (events, visited);
    }

    [Fact]
    public void A_tree_blocks_until_it_is_felled_and_then_its_stump_lets_everybody_through()
    {
        var world = Walled(MapObjectKind.PineTree);
        var gap = new CellPos(8, 4);
        Assert.All(Enum.GetValues<Locomotor>(), locomotor => Assert.False(world.Map.CanEnter(gap, locomotor)));

        var events = new List<SimEvent>();
        Assert.True(CommandProcessor.Apply(world, new DamageMapObjectCommand(Blue, 0, 0, gap, 150), events));
        var tree = world.Map.ObjectsAt(gap).Single();
        Assert.Equal((250, false), (tree.Hp, tree.Destroyed));
        Assert.False(world.Map.CanEnter(gap, Locomotor.Foot));

        CommandProcessor.Apply(world, new DamageMapObjectCommand(Blue, 0, 1, gap, 300), events);

        Assert.True(tree.Destroyed);
        Assert.Contains(new MapObjectDestroyed(0, tree.Id, MapObjectKind.PineTree, gap), events);
        Assert.Contains(events, e => e is MapChanged);
        Assert.True(world.Map.CanEnter(gap, Locomotor.Foot));
        Assert.True(world.Map.CanEnter(gap, Locomotor.Wheel));
        Assert.Single(world.Map.ObjectsAt(gap));

        var conscript = world.SpawnUnit(Blue, UnitType.Conscript, new CellPos(2, 4).Center);
        Order(world, conscript, new CellPos(14, 4));
        var (_, visited) = Run(world, conscript);
        Assert.Contains(gap, visited);
        Assert.Equal(new CellPos(14, 4), conscript.Position.ToCell());
    }

    [Fact]
    public void Damage_below_the_hit_points_changes_the_hash_but_not_the_way()
    {
        var world = Walled(MapObjectKind.PineTree);
        var before = StateHasher.Compute(world);

        world.DamageAtCell(new CellPos(8, 2), 10, null);

        Assert.NotEqual(before, StateHasher.Compute(world));
        Assert.False(world.Map.CanEnter(new CellPos(8, 2), Locomotor.Track));
    }

    [Fact]
    public void A_boulder_cannot_be_destroyed()
    {
        var world = Walled(MapObjectKind.Boulder);
        var before = StateHasher.Compute(world);

        Assert.False(CommandProcessor.Apply(world, new DamageMapObjectCommand(Blue, 0, 0, new CellPos(8, 4), 10_000)));

        Assert.Equal(before, StateHasher.Compute(world));
        Assert.False(world.Map.CanEnter(new CellPos(8, 4), Locomotor.Track));
    }

    [Fact]
    public void A_stone_wall_stops_everybody_and_falls_to_enough_damage()
    {
        var world = Walled(MapObjectKind.WallStone);
        var cell = new CellPos(8, 4);
        Assert.False(world.Map.CanEnter(cell, Locomotor.Track));

        world.DamageAtCell(cell, 1499, null);
        Assert.False(world.Map.CanEnter(cell, Locomotor.Track));
        world.DamageAtCell(cell, 1, null);

        Assert.True(world.Map.CanEnter(cell, Locomotor.Track));
        Assert.DoesNotContain(world.Map.ObjectsAt(cell), o => o.IsAlive);
    }

    [Fact]
    public void Infantry_and_wheels_go_round_a_fence_tanks_crush_it_and_then_everybody_passes()
    {
        // A fence wall with one way round at the bottom (row 8 is open).
        var world = Walled(MapObjectKind.FenceWood, height: 8);
        world = new GameWorld(seed: 1, AddOpenRow(world.Map));
        var jeep = world.SpawnUnit(Blue, UnitType.SupportVehicle, new CellPos(2, 2).Center);
        Order(world, jeep, new CellPos(14, 2));
        var (_, jeepCells) = Run(world, jeep);
        Assert.Equal(new CellPos(14, 2), jeep.Position.ToCell());
        Assert.DoesNotContain(jeepCells, cell => cell.X == 8 && cell.Y < 8);

        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, 4).Center);
        Order(world, tank, new CellPos(14, 4));
        var (events, tankCells) = Run(world, tank);
        Assert.Contains(new CellPos(8, 4), tankCells);
        var crushed = Assert.Single(events.OfType<MapObjectDestroyed>());
        Assert.Equal((MapObjectKind.FenceWood, new CellPos(8, 4)), (crushed.Kind, crushed.Cell));
        Assert.True(world.Map.CanEnter(new CellPos(8, 4), Locomotor.Foot));

        static MapGrid AddOpenRow(MapGrid walled)
        {
            var land = Enumerable.Repeat((byte)LandType.Clear, 16 * 9).ToArray();
            return MapGrid.FromLayers(16, 9, land, new byte[16 * 9], default, walled.ObjectStates());
        }
    }

    [Fact]
    public void A_lamp_post_stops_nobody_and_falls_under_anyone()
    {
        var world = new GameWorld(seed: 1, MapGrid.FromLayers(12, 5, Enumerable.Repeat((byte)LandType.Clear, 60).ToArray(), new byte[60], default,
            [MapObjectState.New(MapObjectKind.LampPost, new CellPos(6, 2), 0)]));
        Assert.All(Enum.GetValues<Locomotor>().Where(l => l != Locomotor.Naval), l => Assert.True(world.Map.CanEnter(new CellPos(6, 2), l)));
        var conscript = world.SpawnUnit(Blue, UnitType.Conscript, new CellPos(1, 2).Center);

        Order(world, conscript, new CellPos(10, 2));
        var (events, _) = Run(world, conscript);

        Assert.Equal(MapObjectKind.LampPost, Assert.Single(events.OfType<MapObjectDestroyed>()).Kind);
        Assert.True(world.Map.Objects.Single().Destroyed);
    }

    [Fact]
    public void Obstacle_masks_and_the_passability_cache_follow_damage_and_crushing()
    {
        var random = new TestRandom(5);
        var objects = new List<MapObjectState>();
        for (var i = 0; i < 80; i++)
        {
            objects.Add(MapObjectState.New((MapObjectKind)(1 + random.NextInt(5)), new CellPos(random.NextInt(20), random.NextInt(12)), random.NextInt(4)));
        }

        var world = new GameWorld(seed: 1, MapGrid.FromLayers(20, 12, Enumerable.Repeat((byte)LandType.Clear, 240).ToArray(), new byte[240], default, objects));
        for (var i = 0; i < 120; i++)
        {
            var cell = new CellPos(random.NextInt(20), random.NextInt(12));
            if (random.NextInt(2) == 0)
            {
                world.DamageAtCell(cell, 1 + random.NextInt(500), null);
            }
            else
            {
                world.CrushMapObjects(cell, (Locomotor)(1 + random.NextInt(3)), null);
            }
        }

        var fresh = world.Map.Clone();
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var cell = new CellPos(x, y);
                byte mask = 0;
                foreach (var o in world.Map.ObjectsAt(cell).Where(o => o.IsAlive))
                {
                    mask |= o.Type.BlockMask;
                }

                Assert.Equal(mask, world.Map.ObstacleMask(cell));
                foreach (var locomotor in Enum.GetValues<Locomotor>())
                {
                    Assert.Equal(fresh.SpeedPercent(cell, locomotor), world.Map.SpeedPercent(cell, locomotor));
                }
            }
        }

        Assert.True(fresh.ContentEquals(world.Map));
    }

    [Fact]
    public void Damage_outside_the_map_or_out_of_range_is_rejected()
    {
        var world = Walled(MapObjectKind.PineTree);

        Assert.False(CommandProcessor.Apply(world, new DamageMapObjectCommand(Blue, 0, 0, new CellPos(16, 0), 100)));
        Assert.False(CommandProcessor.Apply(world, new DamageMapObjectCommand(Blue, 0, 0, new CellPos(8, 0), 0)));
        Assert.False(CommandProcessor.Apply(world, new DamageMapObjectCommand(Blue, 0, 0, new CellPos(8, 0), DamageMapObjectCommand.MaxAmount + 1)));
        Assert.All(world.Map.Objects, o => Assert.Equal(400, o.Hp));
    }

    [Fact]
    public void The_sandbox_trees_are_pines_and_its_objects_are_there()
    {
        var map = TestMapParser.Parse(File.ReadAllText(Path.Combine(Rts.Simulation.Tests.Client.ClientFiles.RepoRoot, "game", "maps", "test", "sandbox.txt"))).Grid;

        Assert.Equal(88, map.Objects.Count(o => o.Kind == MapObjectKind.PineTree));
        Assert.Equal(8, map.Objects.Count(o => o.Kind == MapObjectKind.FenceWood));
        Assert.Equal(5, map.Objects.Count(o => o.Kind == MapObjectKind.WallStone));
        Assert.Equal(4, map.Objects.Count(o => o.Kind == MapObjectKind.Boulder));
        Assert.Equal(6, map.Objects.Count(o => o.Kind == MapObjectKind.LampPost));
        Assert.Equal(Enumerable.Range(1, map.Objects.Count), map.Objects.Select(o => o.Id));
    }
}
