using Rts.Client.Hud;
using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The squares of the sidebar in the order of RA2 (the owner's list, 2026-10-24), by the game data: every tab of both factions, and no
/// item of the data left out of the order. Two people play, no bot.
/// </summary>
public sealed class SidebarOrderTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameWorld World()
    {
        const int size = 64;
        var land = new byte[size * size];
        System.Array.Fill(land, (byte)LandType.Clear);
        var map = MapGrid.FromLayers(size, size, land, new byte[land.Length], default, null, [], []);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "sidebar",
                    55,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        return simulation.World;
    }

    private static string[] Tab(GameWorld world, PlayerId player, BuildGroup group)
    {
        var rules = world.Rules.Database;
        return [.. SidebarModel.Cameos(world, player, group)
            .Select(cameo => cameo.Kind == ProductionKind.Building ? rules.Buildings[cameo.DefIndex].Id : rules.Units[cameo.DefIndex].Id)];
    }

    [Fact]
    public void The_allied_tabs_go_in_the_order_of_ra2()
    {
        var world = World();
        Assert.Equal(
            ["alliance_power_plant", "alliance_ore_refinery", "alliance_barracks", "alliance_war_factory", "alliance_air_force_command",
             "alliance_naval_shipyard", "alliance_service_depot", "alliance_battle_lab", "alliance_ore_purifier",
             "alliance_chronosphere", "alliance_weather_control_device"],
            Tab(world, Allied, BuildGroup.Buildings));
        Assert.Equal(
            ["alliance_wall", "alliance_pillbox", "alliance_patriot_missile_system", "alliance_prism_tower", "alliance_gap_generator",
             "alliance_spy_satellite_uplink", "alliance_grand_cannon"],
            Tab(world, Allied, BuildGroup.Defense));
        Assert.Equal(
            ["alliance_attack_dog", "alliance_rifleman", "alliance_engineer", "alliance_rocketeer", "alliance_spy", "alliance_sniper", "alliance_tanya",
             "alliance_chrono_legionnaire", "alliance_chrono_commando", "alliance_psi_commando"],
            Tab(world, Allied, BuildGroup.Infantry));
        Assert.Equal(
            ["alliance_harvester", "alliance_light_tank", "alliance_ifv", "alliance_tank_destroyer", "alliance_mirage_tank", "alliance_prism_tank",
             "alliance_mcv", "alliance_harrier", "alliance_black_eagle", "alliance_nighthawk", "alliance_amphibious_transport",
             "alliance_destroyer", "alliance_aegis_cruiser", "alliance_dolphin", "alliance_aircraft_carrier"],
            Tab(world, Allied, BuildGroup.Vehicles));
    }

    [Fact]
    public void The_soviet_tabs_go_in_the_order_of_ra2()
    {
        var world = World();
        Assert.Equal(
            ["reactor", "refinery", "barracks", "war_factory", "radar", "naval_shipyard", "service_depot", "tech_center", "nuclear_reactor",
             "cloning_vats", "iron_curtain", "nuclear_missile_silo"],
            Tab(world, Union, BuildGroup.Buildings));
        Assert.Equal(["wall", "sentry_bunker", "flak_cannon", "tesla_coil", "psychic_sensor"], Tab(world, Union, BuildGroup.Defense));
        Assert.Equal(
            ["attack_dog", "conscript", "engineer", "rocketeer", "tesla_trooper", "crazy_ivan", "terrorist", "desolator", "yuri", "chrono_ivan", "yuri_prime"],
            Tab(world, Union, BuildGroup.Infantry));
        Assert.Equal(
            ["harvester", "heavy_tank", "support_vehicle", "terror_drone", "demolition_truck", "rocket_artillery", "tesla_tank", "apocalypse_tank",
             "mcv", "kirov_airship", "hover_transport", "typhoon_sub", "sea_scorpion", "giant_squid", "dreadnought"],
            Tab(world, Union, BuildGroup.Vehicles));
    }

    [Fact]
    public void Every_item_of_the_sidebar_has_its_place_in_the_order()
    {
        var world = World();
        foreach (var player in new[] { Union, Allied })
        {
            foreach (var group in new[] { BuildGroup.Buildings, BuildGroup.Defense, BuildGroup.Infantry, BuildGroup.Vehicles })
            {
                foreach (var id in Tab(world, player, group))
                {
                    Assert.True(SidebarModel.Ra2Place(id) < SidebarModel.Ra2Place("not_an_item"), id);
                }
            }
        }
    }

    [Fact]
    public void Every_tab_fits_into_the_squares_of_the_sidebar()
    {
        // M6 step 10: the grid has SidebarLayout.Slots squares and does not scroll; an item past them would not be seen.
        var world = World();
        foreach (var player in new[] { Union, Allied })
        {
            foreach (var group in Enum.GetValues<BuildGroup>())
            {
                var count = SidebarModel.Cameos(world, player, group).Count;
                Assert.True(count <= SidebarLayout.Slots, $"{player} {group}: {count} items for {SidebarLayout.Slots} squares");
            }
        }
    }
}
