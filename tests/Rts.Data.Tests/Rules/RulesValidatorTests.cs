namespace Rts.Data.Tests.Rules;

/// <summary>
/// The checks of the meaning of the data and the collection of every error (docs/implementation/M3_base_economy.md, 4.3, 6): each mistake
/// is caught at the file and line where it is written, and all of them come together.
/// </summary>
public sealed class RulesValidatorTests
{
    private const string Buildings = "factions/union/buildings.yaml";
    private const string Units = "factions/union/units.yaml";
    private const string Factions = "factions/factions.yaml";
    private const string Terrain = "shared/terrain.yaml";
    private const string Templates = "shared/templates.yaml";
    private const string Locomotors = "shared/locomotors.yaml";
    private const string Weapons = "factions/union/weapons.yaml";
    private const string Warheads = "combat/warheads.yaml";
    private const string Projectiles = "combat/projectiles.yaml";
    private const string BuildOrders = "ai/union.yaml";
    private const string Levels = "ai/difficulty.yaml";
    private const string Personalities = "ai/personalities.yaml";

    /// <summary>The files of the test dataset as (name, text), in the order <see cref="RulesLoader.LoadDirectory"/> reads them.</summary>
    private static List<(string FileName, string Text)> Dataset() =>
        Directory.GetFiles(RulesDataset.Folder, "*.yaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RulesDataset.Folder, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => (name, File.ReadAllText(Path.Combine(RulesDataset.Folder, name)).Replace("\r\n", "\n")))
            .ToList();

    /// <summary>The dataset with one text replaced in one file; the text must be there exactly once.</summary>
    private static List<(string FileName, string Text)> Edited(
        List<(string FileName, string Text)> files, string fileName, string text, string replacement)
    {
        var index = files.FindIndex(file => file.FileName == fileName);
        var original = files[index].Text;
        Assert.Equal(1, original.Split(text).Length - 1);
        var edited = new List<(string FileName, string Text)>(files);
        edited[index] = (fileName, original.Replace(text, replacement));
        return edited;
    }

    private static IReadOnlyList<RulesError> Errors(List<(string FileName, string Text)> files) =>
        Assert.Throws<RulesException>(() => RulesLoader.Load(files)).Errors;

    /// <summary>The one error that a single edit of the dataset gives.</summary>
    private static RulesError Fails(string fileName, string text, string replacement) =>
        Assert.Single(Errors(Edited(Dataset(), fileName, text, replacement)));

    [Fact]
    public void The_dataset_passes_every_check()
    {
        var rules = RulesLoader.Load(Dataset());

        Assert.Equal(4, rules.Buildings.Length);
    }

    [Fact]
    public void An_unknown_key_is_reported_at_the_key()
    {
        var error = Fails(Units, "  sight: 8\n", "  sigth: 8\n");

        Assert.Equal((Units, 38, 3), (error.FileName, error.Line, error.Column));
        Assert.Contains("unknown key 'sigth' in 'heavy_tank'", error.Reason);
    }

    [Fact]
    public void A_prerequisite_that_is_not_defined_is_reported_at_its_id_in_the_list()
    {
        var error = Fails(Buildings, "prerequisites: [reactor]", "prerequisites: [reactor, reacter]");

        Assert.Equal((Buildings, 35, 28), (error.FileName, error.Line, error.Column));
        Assert.Equal("'refinery' refers to building 'reacter', which is not defined", error.Reason);
    }

    [Fact]
    public void A_prerequisite_of_another_kind_is_named_with_its_kind()
    {
        var error = Fails(Units, "prerequisites: [war_factory]", "prerequisites: [heavy_tank_cannon]");

        Assert.Equal((Units, 39, 19), (error.FileName, error.Line, error.Column));
        Assert.Equal("'heavy_tank' refers to building 'heavy_tank_cannon', but 'heavy_tank_cannon' is weapon", error.Reason);
    }

    [Fact]
    public void A_weapon_that_is_not_defined_is_reported()
    {
        var error = Fails(Units, "primary: heavy_tank_cannon", "primary: heavy_tank_gun");

        Assert.Equal((Units, 40, 12), (error.FileName, error.Line, error.Column));
        Assert.Contains("weapon 'heavy_tank_gun', which is not defined", error.Reason);
    }

    [Theory]
    [InlineData(Weapons, "warhead: ap_shell", "warhead: ap_shel", 8, 12, "'heavy_tank_cannon' refers to warhead 'ap_shel', which is not defined")]
    [InlineData(Weapons, "projectile: shell", "projectile: shel", 7, 15, "'heavy_tank_cannon' refers to projectile 'shel', which is not defined")]
    [InlineData(Weapons, "warhead: ap_shell", "warhead: shell", 8, 12, "'heavy_tank_cannon' refers to warhead 'shell', but 'shell' is projectile")]
    [InlineData(Warheads, "steel: 50%", "stone: 50%", 4, 37, "'ap_shell' refers to armor 'stone', which is not defined")]
    public void A_combat_reference_that_does_not_lead_to_its_kind_is_reported_where_it_is_written(
        string fileName, string text, string replacement, int line, int column, string reason)
    {
        var errors = Errors(Edited(Dataset(), fileName, text, replacement));

        Assert.Contains(errors, error => (error.FileName, error.Line, error.Column, error.Reason) == (fileName, line, column, reason));
    }

    [Fact]
    public void A_warhead_must_give_a_share_against_every_armor_type()
    {
        var error = Fails(Warheads, ", steel: 50% }", " }");

        Assert.Equal((Warheads, 4, 11), (error.FileName, error.Line, error.Column));
        Assert.Equal("the verses of 'ap_shell' have no share against armor 'steel'", error.Reason);
    }

    [Fact]
    public void A_unit_or_a_building_needs_armor_that_exists()
    {
        var missing = Fails(Units, "  armor: none\n", "");
        var unknown = Fails(Units, "armor: none", "armor: paper");

        Assert.Equal("'conscript' has no armor", missing.Reason);
        Assert.Equal((Units, 3, 10, "'conscript' refers to armor 'paper', which is not defined"), (unknown.FileName, unknown.Line, unknown.Column, unknown.Reason));
    }

    [Theory]
    [InlineData("war_factory]", "war_fortress]", "'standard' refers to building 'war_fortress', which is not defined")]
    [InlineData("war_factory]", "heavy_tank]", "'standard' refers to building 'heavy_tank', but 'heavy_tank' is unit")]
    [InlineData("[reactor, refinery, war_factory]", "[]", "build order 'standard' has no steps")]
    [InlineData("kind: build_order\n  faction: union", "kind: build_order\n  faction: allies", "'standard' refers to faction 'allies', which is not defined")]
    public void A_build_order_of_the_bots_is_checked_like_the_rest(string text, string replacement, string reason)
    {
        var error = Fails(BuildOrders, text, replacement);

        Assert.Equal(BuildOrders, error.FileName);
        Assert.Contains(reason, error.Reason);
    }

    [Theory]
    [InlineData("heavy_tank: 60%", "harvester: 60%", "'standard_army' gives 'heavy_tank' more against the enemy, but it has no share of the army")]
    [InlineData("heavy_tank: 60%", "heavy_tank: 50%", "the shares of army 'standard_army' do not make 100%")]
    [InlineData("conscript: 40%", "reactor: 40%", "'standard_army' refers to unit 'reactor', but 'reactor' is building")]
    [InlineData("kind: army\n  faction: union", "kind: army\n  faction: allies", "'standard_army' refers to faction 'allies', which is not defined")]
    public void An_army_of_the_bots_is_checked_like_the_rest(string text, string replacement, string reason)
    {
        var errors = Errors(Edited(Dataset(), BuildOrders, text, replacement));

        Assert.Contains(errors, error => error.FileName == BuildOrders && error.Reason.Contains(reason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tactics_period: 15", "tactics_period: 0", "'tactics_period' of 'ai_medium' must be above zero")]
    [InlineData("actions_per_second: 3", "actions_per_second: 0", "'actions_per_second' of 'ai_medium' must be above zero")]
    [InlineData("parallel_squads: 2", "parallel_squads: 0", "'parallel_squads' of 'ai_medium' must be above zero")]
    [InlineData("mistake_chance: 10%", "mistake_chance: 120%", "'mistake_chance' of 'ai_medium' is a share: 100% at most")]
    [InlineData("wounded: 35%", "wounded: 101%", "'wounded' of 'ai_medium' is a share: 100% at most")]
    [InlineData("micro: focus", "micro: smart", "'smart' must be one of none, focus, retreat_wounded, keep_range")]
    [InlineData("defend_harvesters: yes", "defend_harvesters: true", "'true' must be one of no, yes")]
    [InlineData("build_order_quality: standard", "build_order_quality: best", "'best' must be one of simple, standard, optimized")]
    [InlineData("  rebuild_destroyed: yes\n", "", "'ai_medium' has no 'rebuild_destroyed'")]
    [InlineData("gather_cells: 6", "gather_cells: -1", "'gather_cells' of 'ai_medium' must not be below zero")]
    [InlineData("retreat_at_strength: 30%", "retreat_at_strength: 100%", "'ai_medium' would turn back before it lost anything")]
    [InlineData("attack_growth_per_wave: 125%", "attack_growth_per_wave: 90%", "the waves of 'ai_medium' would shrink")]
    [InlineData("  max_defenses: 3\n", "", "max_defenses")]
    public void A_level_of_the_bots_is_checked_like_the_rest(string text, string replacement, string reason)
    {
        var errors = Errors(Edited(Dataset(), Levels, text, replacement));

        Assert.Contains(errors, error => error.FileName == Levels && error.Reason.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void A_wrong_word_of_a_level_is_reported_at_the_word()
    {
        var error = Fails(Levels, "micro: focus", "micro: smart");

        Assert.Equal((Levels, 57, 10), (error.FileName, error.Line, error.Column));
    }

    [Theory]
    [InlineData("economy: 1,", "economy: -1,", "the weight 'economy' of 'balanced' must not be below zero")]
    [InlineData("economy: 1,", "economy: much,", "'much' must be a number such as 1.25")]
    [InlineData("harassment: 1 }", "harassment: 1, luck: 2 }", "unknown key 'luck' in weights; it has economy, defense, army, tech, harassment")]
    [InlineData(" tech: 1,", "", "'tech' is missing from weights")]
    [InlineData("{ union: standard_army }", "{ union: elite_army }", "'balanced' refers to army 'elite_army', which is not defined")]
    [InlineData("{ union: standard_army }", "{ union: standard_army, allies: standard_army }", "'balanced' refers to faction 'allies', which is not defined")]
    [InlineData("  armies: { union: standard_army }\n", "", "'balanced' has no 'armies'")]
    [InlineData("simple: standard,", "simple: rush,", "'balanced' refers to build order 'rush', which is not defined")]
    [InlineData("optimized: standard }", "optimized: standard_army }", "'balanced' refers to build order 'standard_army', but 'standard_army' is army")]
    [InlineData(", optimized: standard }", " }", "'optimized' is missing from the build orders of 'union'")]
    [InlineData("    union: { simple", "    allies: { simple", "'balanced' has no build orders for faction 'union'")]
    [InlineData("min: 360s, max: 480s", "min: 500s, max: 480s", "the first attack of 'balanced' would start after its window ends")]
    [InlineData("wave_interval: 180s", "wave_interval: 0s", "'wave_interval' of 'balanced' must be above zero")]
    [InlineData("retreat_threshold: 70%", "retreat_threshold: 0%", "'retreat_threshold' of 'balanced' must be above 0% and 100% at most")]
    [InlineData("army, power]", "army, army]", "target 'army' repeats")]
    [InlineData("army, power]", "army, pwer]", "'pwer' must be one of production, army, power, harvesters, defense, core")]
    [InlineData(", army, power]", ", army]", "target_priority leaves out 'power'")]
    public void A_personality_of_the_bots_is_checked_like_the_rest(string text, string replacement, string reason)
    {
        var errors = Errors(Edited(Dataset(), Personalities, text, replacement));

        Assert.Contains(errors, error => error.FileName == Personalities && error.Reason.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void A_mistake_inside_a_personality_is_reported_where_it_is_written()
    {
        var weight = Fails(Personalities, "economy: 1,", "economy: -1,");
        var order = Fails(Personalities, "simple: standard,", "simple: rush,");
        var missing = Fails(Personalities, ", optimized: standard }", " }");

        Assert.Equal((5, 23), (weight.Line, weight.Column));
        Assert.Equal((8, 22), (order.Line, order.Column));
        Assert.Equal((8, 12), (missing.Line, missing.Column));
    }

    [Fact]
    public void A_personality_gives_every_faction_its_own_army_and_build_orders()
    {
        // A second faction with an army of its own: the personality that gives it the army of the Union is wrong.
        var files = Edited(Dataset(), Personalities, "{ union: standard_army }", "{ union: standard_army, soviet: standard_army }");
        files.Add((
            "zz.yaml",
            "soviet:\n  kind: faction\n  name: Soviet\n  mcv: mcv\n  construction_yard: construction_yard\n  refinery: refinery\n" +
            "  harvester: harvester\n"));

        var errors = Errors(files);

        Assert.Contains(errors, error => error.Reason == "'balanced' gives 'soviet' the army 'standard_army', which belongs to 'union'");
        Assert.Contains(errors, error => error.Reason == "'balanced' has no build orders for faction 'soviet'");
    }

    [Fact]
    public void A_building_needs_armor_and_a_weapon_that_exist()
    {
        var armor = Fails(Buildings, "  power: 150\n", "  power: 150\n  armor: paper\n");
        var weapon = Fails(Buildings, "  power: 150\n", "  power: 150\n  primary: reactor_gun\n");

        Assert.Equal((Buildings, 21, 10, "'reactor' refers to armor 'paper', which is not defined"), (armor.FileName, armor.Line, armor.Column, armor.Reason));
        Assert.Equal((Buildings, 21, 12), (weapon.FileName, weapon.Line, weapon.Column));
        Assert.Contains("weapon 'reactor_gun', which is not defined", weapon.Reason);
    }

    [Fact]
    public void Only_a_vehicle_crushes_and_only_infantry_is_crushed()
    {
        var crusher = Fails(Units, "  sight: 5\n  turret: false\n", "  sight: 5\n  turret: false\n  crusher: true\n");
        Assert.Equal((Units, 16, 12), (crusher.FileName, crusher.Line, crusher.Column));
        Assert.Contains("'conscript' cannot crush: only a vehicle (placement: cell) crushes infantry", crusher.Reason);

        var crushable = Fails(Units, "  cost: 3000\n", "  cost: 3000\n  crushable: true\n");
        Assert.Equal((Units, 23, 14), (crushable.FileName, crushable.Line, crushable.Column));
        Assert.Contains("'mcv' cannot be crushed: only infantry (placement: subcell) can be", crushable.Reason);
    }

    [Fact]
    public void A_wall_is_one_cell_that_extends_no_base()
    {
        var error = Fails(Buildings, "  power: 150\n", "  power: 150\n  wall: true\n");

        Assert.Equal((Buildings, 21, 9), (error.FileName, error.Line, error.Column));
        Assert.Equal("wall 'reactor' must have a footprint of [1, 1] and extends_base: false", error.Reason);
    }

    [Fact]
    public void A_secondary_weapon_needs_a_primary_one()
    {
        var error = Fails(Units, "primary: heavy_tank_cannon", "secondary: heavy_tank_cannon");

        Assert.Equal("'heavy_tank' has a secondary weapon but no primary one", error.Reason);
    }

    [Theory]
    [InlineData(Weapons, "warhead: ap_shell", "warhead: ap_shell\n  targets: [space]", "'space' must be one of ground, air")]
    [InlineData(Weapons, "warhead: ap_shell", "warhead: ap_shell\n  targets: [ground, ground]", "target 'ground' repeats")]
    [InlineData(Projectiles, "motion: ballistic", "motion: teleport", "'teleport' must be one of instant, ballistic, homing")]
    [InlineData(Warheads, "steel: 50% }", "steel: 50%, steel: 60% }", "Duplicate key steel")]
    [InlineData(Units, "primary: heavy_tank_cannon", "weapons: [heavy_tank_cannon]", "unknown key 'weapons' in 'heavy_tank'")]
    public void A_combat_value_that_does_not_read_is_reported(string fileName, string text, string replacement, string reason)
    {
        var errors = Errors(Edited(Dataset(), fileName, text, replacement));

        Assert.Contains(errors, error => error.FileName == fileName && error.Reason.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void A_faction_that_is_not_defined_is_reported()
    {
        var error = Fails(Units, "  name: Conscript\n  faction: union\n", "  name: Conscript\n  faction: soviet\n");

        Assert.Equal((Units, 5, 12), (error.FileName, error.Line, error.Column));
        Assert.Equal("'conscript' refers to faction 'soviet', which is not defined", error.Reason);
    }

    [Fact]
    public void A_locomotor_that_is_not_defined_is_reported()
    {
        var error = Fails(Units, "locomotor: foot", "locomotor: legs");

        Assert.Equal((Units, 8, 14), (error.FileName, error.Line, error.Column));
        Assert.Contains("locomotor 'legs', which is not defined", error.Reason);
    }

    [Fact]
    public void A_broken_reference_in_a_template_is_reported_once_in_the_template()
    {
        // Three units inherit the locomotor from the template; the error is where the value is written.
        var error = Fails(Templates, "locomotor: track", "locomotor: tracks");

        Assert.Equal((Templates, 8, 14), (error.FileName, error.Line, error.Column));
    }

    [Fact]
    public void A_speed_for_an_unknown_locomotor_is_reported_at_its_key()
    {
        var error = Fails(Terrain, "wheel: 120% }", "wheel: 120%, hover: 50% }");

        Assert.Equal((Terrain, 13, 50), (error.FileName, error.Line, error.Column));
        Assert.Equal("'road' refers to locomotor 'hover', which is not defined", error.Reason);
    }

    [Fact]
    public void A_land_type_needs_a_speed_for_every_locomotor()
    {
        var error = Fails(Terrain, "track: 100%, wheel: 120% }", "track: 100% }");

        Assert.Equal((Terrain, 13, 10), (error.FileName, error.Line, error.Column));
        Assert.Equal("'road' has no speed for locomotor 'wheel'", error.Reason);
    }

    [Fact]
    public void A_land_code_used_twice_is_reported_at_the_second_land_type()
    {
        var error = Fails(Terrain, "code: \"=\"", "code: \".\"");

        Assert.Equal((Terrain, 11, 9), (error.FileName, error.Line, error.Column));
        Assert.Equal("code '.' of 'road' is already used by 'clear'", error.Reason);
    }

    [Fact]
    public void An_inheritance_cycle_is_reported()
    {
        var files = Dataset();
        files.Add(("zz.yaml", "loop_a:\n  kind: weapon\n  inherits: loop_b\n  name: A\nloop_b:\n  kind: weapon\n  inherits: loop_a\n  name: B\n"));

        var error = Assert.Single(Errors(files));

        Assert.Equal(("zz.yaml", 7, 13), (error.FileName, error.Line, error.Column));
        Assert.Equal("inheritance cycle: loop_a -> loop_b -> loop_a", error.Reason);
    }

    [Fact]
    public void A_prerequisite_cycle_is_reported_once_with_the_cycle()
    {
        var error = Fails(Buildings, "  footprint: [3, 3]\n\nreactor:", "  footprint: [3, 3]\n  prerequisites: [refinery]\n\nreactor:");

        // Found from the construction yard, the first in id order; it closes at the reactor, one line lower after the edit.
        Assert.Equal((Buildings, 22, 19), (error.FileName, error.Line, error.Column));
        Assert.Equal("prerequisite cycle: construction_yard -> refinery -> reactor -> construction_yard", error.Reason);
    }

    [Fact]
    public void A_building_that_requires_itself_is_a_cycle()
    {
        var error = Fails(Buildings, "prerequisites: [refinery]", "prerequisites: [war_factory]");

        Assert.Equal("prerequisite cycle: war_factory -> war_factory", error.Reason);
    }

    [Fact]
    public void A_faction_without_its_harvester_is_reported()
    {
        var error = Fails(Factions, "harvester: harvester", "harvester: harvestr");

        Assert.Equal((Factions, 7, 14), (error.FileName, error.Line, error.Column));
        Assert.Equal("'union' refers to unit 'harvestr', which is not defined", error.Reason);
    }

    [Fact]
    public void A_faction_whose_refinery_is_a_unit_is_reported()
    {
        var error = Fails(Factions, "refinery: refinery", "refinery: harvester");

        Assert.Equal((Factions, 6, 13), (error.FileName, error.Line, error.Column));
        Assert.Equal("'union' refers to building 'harvester', but 'harvester' is unit", error.Reason);
    }

    [Fact]
    public void A_faction_with_the_construction_yard_of_another_faction_is_reported()
    {
        var files = Dataset();
        files.Add((
            "zz.yaml",
            "soviet:\n  kind: faction\n  name: Soviet\n  mcv: mcv\n  construction_yard: construction_yard\n  refinery: refinery\n" +
            "  harvester: harvester\n"));

        var errors = Errors(files);

        // A faction of its own needs its own army and build orders in every personality of the bots, too (M8 step 1).
        Assert.Equal(
            [
                "'balanced' has no army for faction 'soviet'",
                "'balanced' has no build orders for faction 'soviet'",
                "'soviet' refers to unit 'mcv', but it belongs to 'union'",
                "'soviet' refers to building 'construction_yard', but it belongs to 'union'",
                "'soviet' refers to building 'refinery', but it belongs to 'union'",
                "'soviet' refers to unit 'harvester', but it belongs to 'union'",
            ],
            errors.Select(error => error.Reason));
    }

    [Theory]
    [InlineData(Buildings, "cost: 600", "cost: 0", "'cost' of 'reactor' must be above zero")]
    [InlineData(Units, "hp: 150", "hp: -1", "'hp' of 'conscript' must be above zero")]
    [InlineData(Units, "speed: 40", "speed: -40", "'speed' of 'heavy_tank' must not be negative")]
    [InlineData(Buildings, "power: 150", "power: 150\n  sight: -2", "'sight' of 'reactor' must not be negative")]
    [InlineData(Buildings, "footprint: [2, 2]", "footprint: [7, 1]", "the footprint of 'reactor' must be 1 to 6 cells a side, not [7, 1]")]
    [InlineData(Buildings, "footprint: [2, 2]", "footprint: [0, 2]", "the footprint of 'reactor' must be 1 to 6 cells a side, not [0, 2]")]
    [InlineData(Buildings, "exit_cell: [1, 3]", "exit_cell: [1, 6]", "the exit cell [1, 6] of 'war_factory' must be in its footprint or next to it")]
    [InlineData(Locomotors, "slope: 65%", "slope: 120%", "'slope' of 'wheel' must be 0% to 100%")]
    [InlineData(Buildings, "  power: 150\n", "  power: 150\n  adjacent: 11\n", "'adjacent' of 'reactor' must be 0 to 10 cells")]
    [InlineData(Weapons, "damage: 120", "damage: -5", "'damage' of 'heavy_tank_cannon' must not be negative")]
    [InlineData(Weapons, "reload: 2s", "reload: 2s\n  burst: 0", "'burst' of 'heavy_tank_cannon' must be above zero")]
    [InlineData(Weapons, "range: 5.5", "range: 5.5\n  min_range: 5.5", "'min_range' of 'heavy_tank_cannon' must be 0 or more and below its range")]
    [InlineData(Weapons, "range: 5.5", "range: 0", "'range' of 'heavy_tank_cannon' must be above zero")]
    [InlineData(Warheads, "steel: 50%", "steel: 1500%", "the share against 'steel' of 'ap_shell' must be 0% to 1000%")]
    [InlineData(Warheads, "percent_at_max: 50%", "percent_at_max: 150%", "'percent_at_max' of 'ap_shell' must be 0% to 100%")]
    [InlineData(Warheads, "spread: 0.5", "spread: -0.5", "'spread' of 'ap_shell' must not be negative")]
    [InlineData(Projectiles, "speed: 40", "speed: 0", "'speed' of 'shell' must be above zero")]
    [InlineData(Projectiles, "motion: ballistic", "motion: homing\n  lifetime: 4s", "'turn_rate' of 'shell' must be above zero")]
    public void A_number_out_of_its_range_is_reported_at_the_number(string fileName, string text, string replacement, string reason)
    {
        var error = Fails(fileName, text, replacement);

        Assert.Equal(fileName, error.FileName);
        Assert.Equal(reason, error.Reason);
    }

    [Fact]
    public void Mistakes_in_several_files_come_together_in_file_and_line_order()
    {
        var files = Dataset();
        files = Edited(files, Terrain, "wheel: 120% }", "wheel: 120%, hover: 50% }");
        files = Edited(files, Buildings, "prerequisites: [reactor]", "prerequisites: [reacter]");
        files = Edited(files, Buildings, "cost: 600", "cost: 0");
        files = Edited(files, Factions, "harvester: harvester", "harvester: harvestr");

        var errors = Assert.Throws<RulesException>(() => RulesLoader.Load(files));

        Assert.Equal(
            [(Factions, 7), (Buildings, 16), (Buildings, 35), (Terrain, 13)],
            errors.Errors.Select(error => (error.FileName, error.Line)));
        Assert.Equal(4, errors.Message.Split('\n').Length);
        Assert.StartsWith($"{Factions}:7:14: ", errors.Message);
    }

    [Fact]
    public void Reading_goes_on_after_a_broken_file_and_a_broken_entry()
    {
        var files = Dataset();
        files.Add(("a.yaml", "broken: [\n"));
        files.Add(("b.yaml", "first:\n  kind: weapon\n  name: First\n  colour: red\nsecond:\n  kind: weapon\n  damage: 5\n"));

        var errors = Errors(files);

        Assert.Equal(3, errors.Count);
        Assert.Equal("a.yaml", errors[0].FileName);
        Assert.Contains("not valid YAML", errors[0].Reason);
        Assert.Equal(("b.yaml", 4, "unknown key 'colour' in 'first'"), (errors[1].FileName, errors[1].Line, errors[1].Reason));
        Assert.Equal(("b.yaml", 5, "'second' has no 'name'"), (errors[2].FileName, errors[2].Line, errors[2].Reason));
    }

    [Fact]
    public void The_meaning_is_not_checked_while_some_entry_cannot_be_read()
    {
        // The broken reactor is left out; the refinery's prerequisite would be a second, misleading error.
        var error = Fails(Buildings, "  power: 150\n", "  power: lots\n");

        Assert.Contains("'lots' must be a whole number", error.Reason);
    }
}
