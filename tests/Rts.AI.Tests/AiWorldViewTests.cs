using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>The world through the eyes of a bot (docs/implementation/M5_first_bot.md, 4.5).</summary>
public sealed class AiWorldViewTests
{
    [Fact]
    public void An_enemy_is_seen_only_while_a_unit_of_the_bot_sees_it()
    {
        var scene = new Scene();
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 8, 8);
        var enemy = scene.Spawn(Scene.Enemy, UnitType.Engineer, 40, 40);

        scene.Look();
        Assert.Empty(scene.View.VisibleEnemies);
        Assert.Equal(CellVisibility.Shroud, scene.View.StateOf(new CellPos(40, 40)));

        Scene.Move(scout, 38, 40);
        scene.Look(1);
        Assert.Equal([enemy], scene.View.VisibleEnemies);
        Assert.True(scene.View.Sees(enemy));

        Scene.Move(scout, 8, 8);
        scene.Look(2);
        Assert.Empty(scene.View.VisibleEnemies);
        Assert.Equal(CellVisibility.Fog, scene.View.StateOf(new CellPos(40, 40)));
    }

    [Fact]
    public void The_own_units_and_buildings_are_known_wherever_they_are()
    {
        var scene = new Scene();
        var far = scene.Spawn(Scene.Me, UnitType.HeavyTank, 60, 60);
        var near = scene.Spawn(Scene.Me, UnitType.Conscript, 5, 5);
        var reactor = scene.Build(Scene.Me, "reactor", 30, 2);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 61, 61);

        scene.Look();

        Assert.Equal([far, near], scene.View.OwnUnits);
        Assert.Equal([reactor], scene.View.OwnBuildings);
        Assert.True(scene.View.IsAlly(Scene.Me));
        Assert.True(scene.View.IsEnemy(Scene.Enemy));
        Assert.False(scene.View.IsEnemy(PlayerId.Neutral));
    }

    [Fact]
    public void An_enemy_building_is_seen_when_one_cell_of_it_is()
    {
        var scene = new Scene();
        var yard = scene.Build(Scene.Enemy, "construction_yard", 40, 40);
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 8, 8);

        scene.Look();
        Assert.Empty(scene.View.VisibleEnemies);

        // The engineer sees 4 cells: from 6 cells left of the yard it sees only its left column.
        Scene.Move(scout, 36, 41);
        scene.Look(1);
        Assert.Equal([yard], scene.View.VisibleEnemies);
    }

    [Fact]
    public void Land_and_ore_are_known_in_explored_cells_and_the_shroud_counts_as_enterable()
    {
        var scene = new Scene();
        var rock = new CellPos(40, 40);
        scene.World.Map.SetLand(rock, LandType.Rock);
        scene.World.Map.SetOre(new CellPos(41, 40), new OreCell(OreKind.Ore, 5));
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 8, 8);

        scene.Look();
        Assert.Null(scene.View.LandAt(rock));
        Assert.True(scene.View.CanEnter(rock, Locomotor.Track));
        Assert.Equal(OreCell.Empty, scene.View.OreAt(new CellPos(41, 40)));
        Assert.False(scene.View.CanEnter(new CellPos(-1, 0), Locomotor.Foot));

        Scene.Move(scout, 38, 40);
        scene.Look(1);
        Assert.Equal(LandType.Rock, scene.View.LandAt(rock));
        Assert.False(scene.View.CanEnter(rock, Locomotor.Track));
        Assert.Equal(new OreCell(OreKind.Ore, 5), scene.View.OreAt(new CellPos(41, 40)));
    }

    [Fact]
    public void In_the_fog_a_building_put_up_since_does_not_block_a_cell()
    {
        var scene = new Scene();
        var scout = scene.Spawn(Scene.Me, UnitType.Engineer, 38, 40);
        scene.Look();
        Scene.Move(scout, 8, 8);
        scene.Look(1);

        // Built in the fog: the bot did not see it, so the cell is as it was when seen.
        scene.Build(Scene.Enemy, "reactor", 40, 40);
        scene.Look(2);

        Assert.Equal(CellVisibility.Fog, scene.View.StateOf(new CellPos(40, 40)));
        Assert.True(scene.View.CanEnter(new CellPos(40, 40), Locomotor.Track));
        Assert.Empty(scene.Memory.Buildings);
    }
}
