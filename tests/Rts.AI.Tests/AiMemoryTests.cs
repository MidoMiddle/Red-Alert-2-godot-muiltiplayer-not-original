using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>What a bot remembers of what it saw (docs/implementation/M5_first_bot.md, 4.5).</summary>
public sealed class AiMemoryTests
{
    [Fact]
    public void A_building_seen_is_remembered_in_the_fog_and_brought_up_to_date_when_seen_again()
    {
        var scene = new Scene();
        var reactor = scene.Build(Scene.Enemy, "reactor", 40, 40);
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 38, 40);
        scene.Look(10);

        Scene.Move(scout, 8, 8);
        reactor.Hp -= 100;
        scene.Look(20);

        var remembered = Assert.Single(scene.Memory.Buildings);
        Assert.Equal((reactor.Id, Scene.Enemy, reactor.DefIndex, reactor.Cell, 10), (remembered.Id, remembered.Owner, remembered.DefIndex, remembered.Cell, remembered.SeenTick));
        Assert.Equal(reactor.MaxHp, remembered.Hp);

        Scene.Move(scout, 38, 40);
        scene.Look(30);
        Assert.Equal((reactor.MaxHp - 100, 30), (scene.Memory.Buildings[0].Hp, scene.Memory.Buildings[0].SeenTick));
    }

    [Fact]
    public void A_building_destroyed_in_the_fog_is_remembered_until_its_place_is_seen_empty()
    {
        var scene = new Scene();
        var reactor = scene.Build(Scene.Enemy, "reactor", 40, 40);
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 38, 40);
        scene.Look(10);
        Scene.Move(scout, 8, 8);
        scene.Look(20);

        scene.World.DestroyBuilding(reactor, null);
        DeathSystem.Update(scene.World, null);
        scene.World.RemoveDead();
        scene.Look(30);
        Assert.Single(scene.Memory.Buildings);

        Scene.Move(scout, 38, 40);
        scene.Look(40);
        Assert.Empty(scene.Memory.Buildings);
    }

    [Fact]
    public void A_building_sold_in_the_fog_is_forgotten_when_its_place_is_seen_empty()
    {
        var scene = new Scene();
        var reactor = scene.Build(Scene.Enemy, "reactor", 40, 40);
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 38, 40);
        scene.Look();
        Scene.Move(scout, 8, 8);
        scene.Simulation.Enqueue(new SellCommand(Scene.Enemy, 0, 0, reactor.Id));
        scene.Simulation.Step();
        scene.Look(1);
        Assert.Single(scene.Memory.Buildings);

        Scene.Move(scout, 38, 40);
        scene.Look(2);
        Assert.Empty(scene.Memory.Buildings);
    }

    [Fact]
    public void An_army_seen_is_remembered_with_its_centre_and_strength_for_60_seconds()
    {
        var scene = new Scene();
        // Two engineers in the block of cells 40–47 × 40–47; the rules of the tests give every unit a cost of 100.
        var first = scene.Spawn(Scene.Enemy, UnitType.Engineer, 42, 42);
        var second = scene.Spawn(Scene.Enemy, UnitType.Engineer, 44, 42);
        second.Hp = second.MaxHp / 2;
        var scout = scene.Spawn(Scene.Me, UnitType.HeavyTank, 40, 37);
        scene.Look(100);

        var army = Assert.Single(scene.Memory.Armies);
        Assert.Equal((5, 5, 2, 2, 100 + 100 * second.Hp / second.MaxHp, 100), (army.BlockX, army.BlockY, army.Units, army.Infantry, army.Strength, army.SeenTick));
        Assert.Equal(new WorldPos((first.Position.X + second.Position.X) / 2, first.Position.Y), army.Centre);

        // The scout leaves: the army stays remembered for 60 s, then is forgotten.
        Scene.Move(scout, 8, 8);
        scene.Look(100 + 899);
        Assert.Single(scene.Memory.Armies);
        scene.Look(100 + 900);
        Assert.Empty(scene.Memory.Armies);
    }

    [Fact]
    public void An_army_is_forgotten_at_once_when_the_centre_of_its_block_is_seen_without_it()
    {
        var scene = new Scene();
        var enemy = scene.Spawn(Scene.Enemy, UnitType.Engineer, 42, 42);
        var scout = scene.Spawn(Scene.Me, UnitType.HeavyTank, 40, 37);
        scene.Look(1);
        Scene.Move(scout, 8, 8);
        scene.Look(2);
        Assert.Single(scene.Memory.Armies);

        Scene.Move(enemy, 60, 20);
        Scene.Move(scout, 44, 40);
        scene.Look(3);
        Assert.Empty(scene.Memory.Armies);
    }

    [Fact]
    public void Where_an_enemy_was_last_seen_is_kept_after_it_is_out_of_sight()
    {
        var scene = new Scene();
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 38, 40);
        scene.Spawn(Scene.Enemy, UnitType.Engineer, 40, 40);
        scene.Look();
        Assert.False(scene.Memory.EnemySeen && scene.Memory.LastEnemyCell is null);

        Scene.Move(scout, 8, 8);
        scene.Look(5000);

        Assert.True(scene.Memory.EnemySeen);
        Assert.Equal(new CellPos(40, 40), scene.Memory.LastEnemyCell);
        Assert.Empty(scene.Memory.Armies);
    }

    [Fact]
    public void The_enemy_may_be_at_any_start_not_explored_yet()
    {
        var scene = new Scene();
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 8, 8);

        scene.Look();
        Assert.Equal([new CellPos(56, 56), new CellPos(56, 8), new CellPos(8, 56)], scene.Memory.PossibleEnemyStarts);

        Scene.Move(scout, 54, 8);
        scene.Look(1);
        Assert.Equal([new CellPos(56, 56), new CellPos(8, 56)], scene.Memory.PossibleEnemyStarts);

        // Explored stays explored: the scout going home brings no start back.
        Scene.Move(scout, 8, 8);
        scene.Look(2);
        Assert.Equal(2, scene.Memory.PossibleEnemyStarts.Count);
    }

    [Fact]
    public void On_a_map_for_two_the_one_other_start_is_the_enemy_as_in_ra2()
    {
        var scene = new Scene([new CellPos(8, 32), new CellPos(56, 32)]);
        scene.Spawn(Scene.Me, UnitType.Engineer, 8, 32);

        scene.Look();

        Assert.Equal([new CellPos(56, 32)], scene.Memory.PossibleEnemyStarts);
    }
}
