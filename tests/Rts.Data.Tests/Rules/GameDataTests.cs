using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Rules;

/// <summary>
/// The data files of the game, <c>game/data</c> (docs/implementation/M3_base_economy.md, 4.5, 6): they load, they give the simulation
/// the same rules as the tests of M2 play by, and what the engine needs from them is checked at the place where it is written.
/// </summary>
public sealed class GameDataTests
{
    private const string Terrain = "shared/terrain.yaml";

    /// <summary>The files the loader reads, the same list as <c>GameData.LoadDirectory</c>: without the sounds of <c>audio/</c>.</summary>
    private static List<(string FileName, string Text)> Files() =>
        RulesLoader.FilesBelow(GameDataFiles.Folder)
            .Select(file => (file.FileName, file.Text.Replace("\r\n", "\n")))
            .ToList();

    /// <summary>The errors of the game data with one text replaced in one file.</summary>
    private static IReadOnlyList<RulesError> ErrorsWith(string fileName, string text, string replacement)
    {
        var files = Files();
        var index = files.FindIndex(file => file.FileName == fileName);
        Assert.Equal(1, files[index].Text.Split(text).Length - 1);
        files[index] = (fileName, files[index].Text.Replace(text, replacement));
        return Assert.Throws<RulesException>(() => GameData.Load(files)).Errors;
    }

    [Fact]
    public void Every_file_of_the_game_data_loads()
    {
        var database = GameDataFiles.Current.Database;

        Assert.Equal(8, database.Units.Length);
        Assert.Equal(11, database.Buildings.Length);
        Assert.Equal(6, database.Locomotors.Length);
        Assert.Equal(8, database.Terrains.Length);
        Assert.Equal(new[] { "union" }, database.Factions.Select(faction => faction.Id));
    }

    [Fact]
    public void The_hash_of_the_game_data_is_pinned()
    {
        // Changes with every change of a number in game/data: update it then, together with the reason in the commit.
        // M5 step 5: the build order of the bots of the Union (game/data/ai/union.yaml); step 6: their army; step 14: «Medium» in the data
        // (game/data/ai/difficulty.yaml), the war factory before the second refinery and no tech centre in the build order. M8 step 1: the
        // four levels of the bots and their keys of M8, the four personalities (game/data/ai/personalities); step 2: the bonuses of the player in every level.
        Assert.Equal(0xb5233d371a029e59UL, GameDataFiles.Current.Hash);
    }

    [Fact]
    public void The_game_data_has_the_four_levels_and_the_four_personalities_of_the_bots()
    {
        // M8 step 1, 4.2 and 4.7: every level and every personality loads, every personality plays the Union.
        var database = GameDataFiles.Rules.Database;

        Assert.Equal(["ai_brutal", "ai_easy", "ai_hard", "ai_medium"], database.Difficulties.Select(level => level.Id));
        Assert.Equal(["aggressor", "balanced", "builder", "saboteur"], database.Personalities.Select(personality => personality.Id));
        // M8 step 2, 4.5: only «Brutal» has the bonuses of the player — ×1.25 income, ×1.2 build speed, the map open.
        Assert.Equal(
            [("ai_brutal", new PlayerModifiers(25, 20, true)), ("ai_easy", default), ("ai_hard", default), ("ai_medium", default)],
            database.Difficulties.Select(level => (level.Id, PlayerModifiers.Of(level))));
        Assert.All(database.Personalities, personality =>
        {
            Assert.Equal("union", Assert.Single(personality.Armies).Faction);
            Assert.Equal("union", Assert.Single(personality.BuildOrders).Faction);
        });
    }

    [Fact]
    public void The_game_data_gives_the_rules_of_the_simulation_tests()
    {
        var rules = GameDataFiles.Rules;

        foreach (var type in UnitTypes.All)
        {
            Assert.Equal(TestRules.Unit(type), rules.Unit(type));
        }

        Assert.True(rules.Land.ContentEquals(TestRules.Standard.Land));
        for (var land = LandType.Clear; land <= LandType.Ice; land++)
        {
            Assert.Equal(TestRules.Standard.IsBuildable(land), rules.IsBuildable(land));
        }
    }

    [Fact]
    public void The_game_data_has_the_buildings_and_prerequisites_of_the_simulation_tests()
    {
        // What power, technologies, placement, build-up and production read (M3 steps 5–8).
        var data = GameDataFiles.Current.Database;
        var test = TestRules.Standard.Database;

        Assert.Equal(test.Buildings.Select(Shape), data.Buildings.Select(Shape));
        Assert.Equal(test.Units.Select(unit => (unit.Id, unit.Faction, string.Join(",", unit.Prerequisites), unit.Armor, unit.Primary, unit.Secondary, unit.Threat, unit.Stance, unit.Crusher, unit.Crushable)),
            data.Units.Select(unit => (unit.Id, unit.Faction, string.Join(",", unit.Prerequisites), unit.Armor, unit.Primary, unit.Secondary, unit.Threat, unit.Stance, unit.Crusher, unit.Crushable)));
        Assert.Equal(test.Factions.Single(), data.Factions.Single() with { Name = test.Factions.Single().Name });

        Assert.Equal(test.Economies.Single(), data.Economies.Single());

        static (string, string, int, int, int, string, int, int, bool, bool, string, string, string, string, string, string, int, bool, bool, int) Shape(Rts.Simulation.Rules.BuildingDef building) =>
            (building.Id, building.Faction, building.Footprint.Width, building.Footprint.Height, building.Power,
             string.Join(",", building.Prerequisites), building.BuildUp, building.Adjacent, building.ExtendsBase, building.Radar,
             string.Join(",", building.Produces), building.ExitCell?.ToString() ?? "", building.DockCell?.ToString() ?? "",
             building.Category.ToString(), building.Armor, building.Primary, building.Threat, building.Wall, building.Powered, building.Sight);
    }

    [Fact]
    public void The_game_data_has_the_combat_of_the_simulation_tests()
    {
        // Armor, warheads, projectiles and weapons (M4 step 1): every number the simulation reads; names and sounds are left out.
        var data = GameDataFiles.Current.Database;
        var test = TestRules.Standard.Database;

        Assert.Equal(test.Armors.Select(armor => armor.Id), data.Armors.Select(armor => armor.Id));
        Assert.Equal(test.Warheads.Select(w => w with { Name = "" }), data.Warheads.Select(w => w with { Name = "" }));
        Assert.Equal(test.Projectiles.Select(p => p with { Name = "" }), data.Projectiles.Select(p => p with { Name = "" }));
        Assert.Equal(test.Weapons.Select(w => w with { Name = "", Report = "" }), data.Weapons.Select(w => w with { Name = "", Report = "" }));
    }

    [Fact]
    public void The_land_speeds_of_the_game_data_are_the_reference_table_of_maps()
    {
        // Maps outside a match (the editor, the checks, the generator) go by the reference table.
        Assert.True(GameDataFiles.Rules.Land.ContentEquals(LandSpeeds.Reference));
    }

    [Fact]
    public void A_unit_type_the_engine_needs_is_reported_without_a_place()
    {
        // The engineer: no build order or army of the bots names it, so the engine's need is the only error (M5 step 6).
        var error = Assert.Single(ErrorsWith("factions/union/units.yaml", "engineer:  # Engineer", "sapper:  # Engineer"));

        Assert.Equal(("", 0, 0), (error.FileName, error.Line, error.Column));
        Assert.Equal("the engine needs unit 'engineer', which is not defined", error.Reason);
        Assert.Equal(error.Reason, error.ToString());
    }

    [Fact]
    public void The_engine_needs_one_economy_record()
    {
        var files = Files().Where(file => file.FileName != "shared/economy.yaml").ToList();

        var error = Assert.Single(Assert.Throws<RulesException>(() => GameData.Load(files)).Errors);

        Assert.Equal("the engine needs exactly one economy record, and there are 0", error.Reason);
    }

    [Theory]
    [InlineData("low_power_min_rate: 25%", "low_power_min_rate: 120%", "'low_power_min_rate' of 'economy' must be 0% to 100%")]
    [InlineData("multiple_factory_bonus: 50%", "multiple_factory_bonus: -10%", "'-10%' must be a percentage such as 125%")]
    [InlineData("max_factory_rate: 300%", "max_factory_rate: 50%", "'max_factory_rate' of 'economy' must be 100% or more")]
    [InlineData("ore_growth_chance: 20%", "ore_growth_chance: 120%", "'ore_growth_chance' of 'economy' must be 0% to 100%")]
    [InlineData("ore_spread_stage: 6", "ore_spread_stage: 12", "'ore_spread_stage' of 'economy' must be 0 to 11")]
    [InlineData("ore_value: 25", "ore_value: 0", "'ore_value' of 'economy' must be above zero")]
    public void An_economy_rate_out_of_its_range_is_reported(string text, string replacement, string reason)
    {
        var error = Assert.Single(ErrorsWith("shared/economy.yaml", text, replacement));

        Assert.Equal(reason, error.Reason);
    }

    [Theory]
    [InlineData("produces: [infantry]", "produces: [aircraft]", "'aircraft' must be one of building, defense, infantry, vehicle")]
    [InlineData("produces: [infantry]", "produces: [infantry, infantry]", "category 'infantry' repeats")]
    public void A_wrong_category_of_a_factory_is_reported(string text, string replacement, string reason)
    {
        var error = Assert.Single(ErrorsWith("factions/union/buildings.yaml", text, replacement));

        Assert.Contains(reason, error.Reason);
    }

    [Theory]
    [InlineData("factions/union/units.yaml", "  harvest_capacity: 1000\n", "", "'harvester' is the harvester of 'union', so it needs a harvest_capacity")]
    [InlineData("factions/union/buildings.yaml", "  dock_cell: [1, 3]\n", "", "'refinery' is the refinery of 'union', so it needs a dock_cell")]
    [InlineData("factions/union/buildings.yaml", "dock_cell: [1, 3]", "dock_cell: [1, 9]", "the dock cell [1, 9] of 'refinery' must be in its footprint or next to it")]
    [InlineData("factions/union/units.yaml", "unload_rate: 50", "unload_rate: 0", "'unload_rate' of 'harvester' must be above zero")]
    public void The_harvester_and_the_refinery_need_their_keys(string fileName, string text, string replacement, string reason)
    {
        var error = Assert.Single(ErrorsWith(fileName, text, replacement));

        Assert.Equal(reason, error.Reason);
    }

    [Fact]
    public void A_land_code_other_than_the_map_format_is_reported_at_the_code()
    {
        var error = Assert.Single(ErrorsWith(Terrain, "code: \"=\"", "code: \"+\""));

        Assert.Equal((Terrain, 21, 9), (error.FileName, error.Line, error.Column));
        Assert.Equal("'road' is written '=' in map files, so its code must be \"=\"", error.Reason);
    }

    [Fact]
    public void A_speed_over_255_percent_is_reported_at_the_speeds()
    {
        var error = Assert.Single(ErrorsWith(Terrain, "wheel: 120%", "wheel: 300%"));

        Assert.Equal((Terrain, 24, 10), (error.FileName, error.Line, error.Column));
        Assert.Equal("the speed of 'wheel' on 'road' must be 255% at most", error.Reason);
    }

    [Fact]
    public void A_mistake_of_the_data_itself_comes_before_the_engine_checks()
    {
        // The validator finds the repeated code; the engine checks, which would also want '=' here, run only on data without mistakes.
        var error = Assert.Single(ErrorsWith(Terrain, "code: \"=\"", "code: \".\""));

        Assert.Equal("code '.' of 'road' is already used by 'clear'", error.Reason);
    }
}
