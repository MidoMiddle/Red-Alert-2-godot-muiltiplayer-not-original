using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The Alliance, a faction one can play (docs/content/names.md): its start, its yard, its tech tree by the game data, and the late
/// buildings of both factions whose ability comes later — built, drawing power and opening the tree. Two people play here, no bot.
/// </summary>
public sealed class AllianceTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match() =>
        new(SimulationSettings.ForMatch(
            new MatchSetup(
                "clear",
                3,
                new MatchOptions(),
                [
                    new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(12, 12)),
                    new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(50, 50)),
                ],
                [new CellPos(12, 12), new CellPos(50, 50)]),
            new MapGrid(64, 64)),
            GameDataFiles.Rules);

    private static bool CanBuild(GameWorld world, PlayerId player, string id)
    {
        TechTree.Update(world);
        var rules = world.Rules.Database;
        var building = rules.IndexOfBuilding(id);
        return building >= 0 ? world.Player(player).CanBuildBuilding(building) : world.Player(player).CanBuildUnit(rules.IndexOfUnit(id));
    }

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    [Fact]
    public void The_alliance_starts_with_its_mcv_and_three_riflemen_and_the_union_with_conscripts()
    {
        var world = Match().World;

        var allied = world.Entities.OfType<Unit>().Where(unit => unit.Owner == Allied).Select(unit => unit.Type).Order().ToList();
        var union = world.Entities.OfType<Unit>().Where(unit => unit.Owner == Union).Select(unit => unit.Type).Order().ToList();

        Assert.Equal([UnitType.AllianceMcv, UnitType.AllianceRifleman, UnitType.AllianceRifleman, UnitType.AllianceRifleman], allied);
        Assert.Equal([UnitType.Conscript, UnitType.Conscript, UnitType.Conscript, UnitType.Mcv], union);
        Assert.Equal("alliance", world.Player(Allied).Faction.Id);
    }

    [Fact]
    public void The_alliance_mcv_deploys_into_its_command_yard_which_opens_its_power_station_only()
    {
        var simulation = Match();
        var mcv = simulation.World.Entities.OfType<Unit>().Single(unit => unit.Owner == Allied && unit.Type == UnitType.AllianceMcv);

        simulation.Enqueue(new DeployCommand(Allied, 0, 0, mcv.Id));
        for (var tick = 0; tick < 60; tick++)
        {
            simulation.Step();
        }

        var world = simulation.World;
        Assert.Contains(world.Entities.OfType<Building>(), building => building.Owner == Allied && building.Def!.Id == "alliance_construction_yard");
        Assert.True(CanBuild(world, Allied, "alliance_power_plant"));
        Assert.False(CanBuild(world, Allied, "reactor"));
        Assert.False(CanBuild(world, Allied, "alliance_barracks"));
    }

    [Fact]
    public void The_tree_of_the_alliance_opens_by_its_own_buildings_up_to_the_research_lab()
    {
        var world = Match().World;
        Build(world, Allied, "alliance_construction_yard", 10, 10);
        Build(world, Allied, "alliance_power_plant", 14, 10);
        Assert.True(CanBuild(world, Allied, "alliance_barracks"));
        Assert.False(CanBuild(world, Allied, "alliance_rifleman"), "the rifleman needs the training camp");

        Build(world, Allied, "alliance_barracks", 17, 10);
        Build(world, Allied, "alliance_ore_refinery", 10, 14);
        Build(world, Allied, "alliance_war_factory", 14, 14);
        Build(world, Allied, "alliance_air_force_command", 18, 14);
        Assert.True(CanBuild(world, Allied, "alliance_rifleman"));
        Assert.True(CanBuild(world, Allied, "alliance_light_tank"));
        Assert.True(CanBuild(world, Allied, "alliance_prism_tower"));
        Assert.True(CanBuild(world, Allied, "alliance_battle_lab"));
        Assert.False(CanBuild(world, Allied, "alliance_chronosphere"));

        Build(world, Allied, "alliance_battle_lab", 10, 19);
        Assert.True(CanBuild(world, Allied, "alliance_chronosphere"));
        Assert.True(CanBuild(world, Allied, "alliance_weather_control_device"));
        Assert.False(CanBuild(world, Allied, "heavy_tank"));
        Assert.False(CanBuild(world, Union, "alliance_power_plant"));
    }

    [Fact]
    public void A_building_whose_ability_comes_later_is_built_draws_power_and_opens_the_tree()
    {
        var world = Match().World;
        Build(world, Union, "construction_yard", 48, 48);
        Build(world, Union, "reactor", 52, 48);
        Build(world, Union, "refinery", 48, 52);
        Build(world, Union, "war_factory", 52, 52);
        Build(world, Union, "radar", 56, 48);
        Build(world, Union, "tech_center", 56, 52);
        Assert.True(CanBuild(world, Union, "iron_curtain"));
        Assert.True(CanBuild(world, Union, "nuclear_reactor"));

        PowerSystem.Update(world, null);
        var power = world.Player(Union).PowerDrain;
        var curtain = Build(world, Union, "iron_curtain", 44, 44);
        PowerSystem.Update(world, null);

        Assert.True(curtain.Def!.AbilityLater);
        Assert.Equal(power + 200, world.Player(Union).PowerDrain);
        Assert.False(world.Rules.Database.Buildings[world.Rules.Database.IndexOfBuilding("nuclear_reactor")].AbilityLater);
    }

    [Fact]
    public void The_late_units_open_by_their_buildings_and_ships_and_aircraft_never_yet()
    {
        // The first stage of filling the game: every unit is in; ships and aircraft wait for their mechanic (available_later).
        var world = Match().World;
        Build(world, Union, "construction_yard", 48, 48);
        Build(world, Union, "reactor", 52, 48);
        Build(world, Union, "barracks", 56, 48);
        Assert.True(CanBuild(world, Union, "attack_dog"));
        Assert.False(CanBuild(world, Union, "apocalypse_tank"));

        Build(world, Union, "refinery", 48, 52);
        Build(world, Union, "war_factory", 52, 52);
        Build(world, Union, "radar", 56, 52);
        Build(world, Union, "tech_center", 44, 52);
        Build(world, Union, "naval_shipyard", 40, 44);
        Assert.True(CanBuild(world, Union, "apocalypse_tank"));
        Assert.True(CanBuild(world, Union, "desolator"));
        Assert.False(CanBuild(world, Union, "kirov_airship"));
        Assert.False(CanBuild(world, Union, "dreadnought"));
        Assert.False(CanBuild(world, Union, "alliance_sniper"));

        Build(world, Allied, "alliance_construction_yard", 10, 10);
        Build(world, Allied, "alliance_power_plant", 14, 10);
        Build(world, Allied, "alliance_barracks", 17, 10);
        Build(world, Allied, "alliance_ore_refinery", 10, 14);
        Build(world, Allied, "alliance_air_force_command", 18, 14);
        Assert.True(CanBuild(world, Allied, "alliance_sniper"));
        Assert.False(CanBuild(world, Allied, "alliance_harrier"));

        var units = world.Rules.Database.Units;
        Assert.All(units.Where(unit => unit.AvailableLater), unit => Assert.True(unit.AbilityLater, unit.Id));
        Assert.Equal(14, units.Count(unit => unit.AvailableLater));
    }

    [Fact]
    public void In_the_short_game_the_alliance_mcv_keeps_its_player_in()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "clear",
                    3,
                    new MatchOptions(ShortGame: true),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(12, 12)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(50, 50)),
                    ],
                    [new CellPos(12, 12), new CellPos(50, 50)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        for (var tick = 0; tick < 30; tick++)
        {
            simulation.Step();
        }

        // No building yet: the MCV of either faction keeps its player in the short game.
        Assert.Null(simulation.World.Result);
        Assert.Null(simulation.World.Player(Allied).DefeatedTick);
        Assert.True(simulation.World.Rules.IsMcv(UnitType.AllianceMcv));
        Assert.False(simulation.World.Rules.IsMcv(UnitType.AllianceLightTank));
    }
}
