using Rts.Client.Hud;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Matches;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The tooltip of the field: the name of the unit or building under the cursor after half a second, as the sidebar names it; nothing for
/// what the player does not see, a disguised spy named as the soldier it passes for, a holo tank standing as a tree not named at all.
/// Player 1 (the Union) looks; player 2 is the enemy.
/// </summary>
public sealed class HoverTooltipTests
{
    private const int Size = 64;
    private static readonly PlayerId Viewer = new(1);
    private static readonly PlayerId Enemy = new(2);
    private static readonly CellPos Middle = new(30, 30);
    private static readonly CellPos House = new(20, 40);

    private static string Translate(string key) => key switch
    {
        "RA2_NAME_CONSCRIPT" => "Conscript",
        "RA2_NAME_HEAVY_TANK" => "Rhino Tank",
        "RA2_NAME_BARRACKS" => "Soviet Barracks",
        "RA2_NAME_NEUTRAL_HOUSE_A" => "Civilian House",
        _ => key,
    };

    // The clear map of the match tests with a civilian house of the map at (20, 40) and an oil derrick at (40, 20).
    private static GameWorld World(GameRules? rules = null)
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null,
            [new BuildingState(BuildingKind.NeutralHouseA, House, PlayerId.Neutral, 0), new BuildingState(BuildingKind.TechOilDerrick, new CellPos(40, 20), PlayerId.Neutral, 0)]);
        return new GameSimulation(SimulationSettings.ForMatch(MatchSetupTests.OneOnOne(), map), rules ?? TestRules.Standard).World;
    }

    private static bool Sees(GameWorld world, Entity entity) => world.AreAllies(Viewer, entity.Owner) || Vision.Sees(world, Viewer, entity);

    private static string? Name(GameWorld world, Entity entity, bool ra2 = true) => HoverTooltip.NameOf(world, Viewer, entity, ra2, Translate);

    [Fact]
    public void The_tooltip_shows_after_half_a_second_on_the_same_thing()
    {
        var tooltip = new HoverTooltip();
        var tank = new EntityId(5);

        Assert.Null(tooltip.Update(tank, 0.1));
        for (var frame = 0; frame < 4; frame++)
        {
            Assert.Null(tooltip.Update(tank, 0.1));
        }

        Assert.Equal(tank, tooltip.Update(tank, 0.1));
        Assert.Equal(tank, tooltip.Update(tank, 0.1));
    }

    [Fact]
    public void Another_thing_or_nothing_under_the_cursor_starts_the_wait_again()
    {
        var tooltip = new HoverTooltip();
        var tank = new EntityId(5);
        var house = new EntityId(6);
        for (var frame = 0; frame < 10; frame++)
        {
            tooltip.Update(tank, 0.1);
        }

        Assert.Null(tooltip.Update(house, 0.1));
        Assert.Null(tooltip.Update(null, 0.1));
        Assert.Null(tooltip.Update(tank, 0.1));
        Assert.Null(tooltip.Update(tank, 0.3));
        Assert.Equal(tank, tooltip.Update(tank, 0.2));
    }

    [Fact]
    public void Under_the_cursor_is_the_unit_picked_else_the_building_on_the_cell_whoever_owns_it()
    {
        var world = World();
        var tank = world.SpawnUnit(Viewer, UnitType.HeavyTank, Middle.Center);
        var house = world.BuildingAt(House)!;

        Assert.Same(tank, HoverTooltip.Under(world, tank.Id, House, _ => true));
        Assert.Same(house, HoverTooltip.Under(world, null, House, _ => true));
        // An enemy building under the cursor leaves no unit picked.
        Assert.Same(house, HoverTooltip.Under(world, EntityId.None, House, _ => true));
        Assert.Null(HoverTooltip.Under(world, null, Middle, _ => true));
        Assert.Null(HoverTooltip.Under(world, null, null, _ => true));
    }

    [Fact]
    public void What_the_fog_hides_has_no_tooltip()
    {
        var world = World();
        var enemy = world.SpawnUnit(Enemy, UnitType.HeavyTank, Middle.Center);
        world.CreateBuilding(Enemy, TestRules.BuildingDef("barracks"), new CellPos(32, 30), true, null);
        VisibilitySystem.Update(world);

        Assert.Null(HoverTooltip.Under(world, enemy.Id, null, entity => Sees(world, entity)));
        Assert.Null(HoverTooltip.Under(world, null, new CellPos(32, 30), entity => Sees(world, entity)));

        world.SpawnUnit(Viewer, UnitType.HeavyTank, new CellPos(28, 30).Center);
        VisibilitySystem.Update(world);

        Assert.Same(enemy, HoverTooltip.Under(world, enemy.Id, null, entity => Sees(world, entity)));
        Assert.Equal("Soviet Barracks", Name(world, HoverTooltip.Under(world, null, new CellPos(32, 30), entity => Sees(world, entity))!));
    }

    [Fact]
    public void Units_and_buildings_are_named_as_on_the_sidebar()
    {
        var world = World();
        var own = world.SpawnUnit(Viewer, UnitType.HeavyTank, Middle.Center);
        var enemy = world.SpawnUnit(Enemy, UnitType.Conscript, new CellPos(31, 30).Center);
        var barracks = world.CreateBuilding(Enemy, TestRules.BuildingDef("barracks"), new CellPos(34, 30), true, null);

        Assert.Equal("Rhino Tank", Name(world, own));
        Assert.Equal("Conscript", Name(world, enemy));
        Assert.Equal("Soviet Barracks", Name(world, barracks));
        // Without the names of RA2, ours.
        Assert.Equal("heavy_tank", Name(world, own, ra2: false));
        Assert.Equal("barracks", Name(world, barracks, ra2: false));
    }

    [Fact]
    public void A_building_of_the_map_has_its_plain_name_or_one_made_of_its_id()
    {
        var world = World();

        Assert.Equal("Civilian House", Name(world, world.BuildingAt(House)!));
        Assert.Equal("Civilian House", Name(world, world.BuildingAt(House)!, ra2: false));
        Assert.Equal("Oil Derrick", Name(world, world.BuildingAt(new CellPos(40, 20))!));
        Assert.Equal("House B", HoverTooltip.PlainName("neutral_house_b"));
        Assert.Equal("Outpost", HoverTooltip.PlainName("tech_outpost"));
        Assert.Equal("Statue", HoverTooltip.PlainName("statue"));
    }

    [Fact]
    public void A_disguised_enemy_spy_is_named_as_the_soldier_it_passes_for()
    {
        var world = World(TestRules.WithUnit(UnitType.AllianceSpy, unit => unit with { Disguised = true }));
        var spy = world.SpawnUnit(Enemy, UnitType.AllianceSpy, Middle.Center);

        Assert.Equal("Conscript", Name(world, spy));
        // Its own side sees it as it is.
        Assert.Equal("alliance_spy", HoverTooltip.NameOf(world, Enemy, spy, ra2: true, Translate));
    }

    [Fact]
    public void An_enemy_holo_tank_standing_as_a_tree_has_no_name()
    {
        var world = World(TestRules.WithUnit(UnitType.AllianceMirageTank, unit => unit with { DisguiseWhenStill = true }));
        var holo = world.SpawnUnit(Enemy, UnitType.AllianceMirageTank, Middle.Center);

        Assert.True(TargetingSystem.IsTree(holo));
        Assert.Null(Name(world, holo));
        Assert.Equal("alliance_mirage_tank", HoverTooltip.NameOf(world, Enemy, holo, ra2: true, Translate));
    }
}
