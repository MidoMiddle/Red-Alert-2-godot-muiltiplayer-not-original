using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

public sealed class GameWorldTests
{
    private static readonly PlayerId Player1 = new(1);

    private static GameWorld NewWorld() => new(seed: 1, widthCells: 64, heightCells: 64);

    private static TestUnit Spawn(GameWorld world) => world.SpawnTestUnit(Player1, new WorldPos(128, 128), speed: 10);

    [Fact]
    public void Ids_start_at_one_and_increase()
    {
        var world = NewWorld();

        Assert.Equal(new EntityId(1), world.NextEntityId);
        Assert.Equal(new EntityId(1), Spawn(world).Id);
        Assert.Equal(new EntityId(2), Spawn(world).Id);
        Assert.Equal(new EntityId(3), world.NextEntityId);
    }

    [Fact]
    public void Ids_are_never_reused_after_removal()
    {
        var world = NewWorld();
        var first = Spawn(world);
        first.MarkDead();
        world.RemoveDead();

        Assert.Equal(new EntityId(2), Spawn(world).Id);
    }

    [Fact]
    public void Entities_are_iterated_in_id_order_after_removals()
    {
        var world = NewWorld();
        var units = Enumerable.Range(0, 10).Select(_ => Spawn(world)).ToList();
        units[2].MarkDead();
        units[7].MarkDead();
        world.RemoveDead();
        Spawn(world);

        var ids = world.Entities.Select(e => e.Id.Value).ToList();

        Assert.Equal([1, 2, 4, 5, 6, 7, 9, 10, 11], ids);
    }

    [Fact]
    public void Dead_entity_stays_until_end_of_tick()
    {
        var world = NewWorld();
        var unit = Spawn(world);

        unit.MarkDead();

        Assert.Single(world.Entities);
        Assert.True(world.TryGet(unit.Id, out var found));
        Assert.False(found.IsAlive);

        world.RemoveDead();

        Assert.Empty(world.Entities);
        Assert.False(world.TryGet(unit.Id, out _));
    }

    [Fact]
    public void TryGet_finds_living_entities_among_many()
    {
        var world = NewWorld();
        var units = Enumerable.Range(0, 1000).Select(_ => Spawn(world)).ToList();
        foreach (var unit in units.Where((_, index) => index % 2 == 1))
        {
            unit.MarkDead();
        }

        world.RemoveDead();

        for (var index = 0; index < units.Count; index++)
        {
            var exists = world.TryGet(units[index].Id, out var found);
            Assert.Equal(index % 2 == 0, exists);
            if (exists)
            {
                Assert.Same(units[index], found);
            }
        }

        Assert.False(world.TryGet(EntityId.None, out _));
        Assert.False(world.TryGet(new EntityId(5000), out _));
    }

    [Fact]
    public void Spawned_unit_keeps_its_parameters()
    {
        var world = NewWorld();

        var unit = world.SpawnTestUnit(Player1, new WorldPos(300, 400), speed: 25);

        Assert.Equal(Player1, unit.Owner);
        Assert.Equal(new WorldPos(300, 400), unit.Position);
        Assert.Equal(25, unit.Speed);
        Assert.Null(unit.MoveTarget);
        Assert.True(unit.IsAlive);
    }

    [Fact]
    public void Contains_checks_map_bounds()
    {
        var world = new GameWorld(seed: 1, widthCells: 4, heightCells: 2);

        Assert.True(world.Contains(new WorldPos(0, 0)));
        Assert.True(world.Contains(new WorldPos(4 * 256 - 1, 2 * 256 - 1)));
        Assert.False(world.Contains(new WorldPos(4 * 256, 0)));
        Assert.False(world.Contains(new WorldPos(0, 2 * 256)));
        Assert.False(world.Contains(new WorldPos(-1, 0)));
    }

    [Fact]
    public void Invalid_map_size_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameWorld(seed: 1, widthCells: 0, heightCells: 10));
    }

    [Fact]
    public void Players()
    {
        Assert.True(PlayerId.Neutral.IsNeutral);
        Assert.False(new PlayerId(1).IsNeutral);
        Assert.Equal(8, PlayerId.MaxPlayers);
        Assert.Equal("Neutral", PlayerId.Neutral.ToString());
        Assert.Equal("P3", new PlayerId(3).ToString());
    }
}
