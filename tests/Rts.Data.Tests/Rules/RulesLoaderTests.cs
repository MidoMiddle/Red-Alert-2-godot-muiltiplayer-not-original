using Rts.Data.Yaml;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Rules;

/// <summary>
/// The loader of the data files (docs/implementation/M3_base_economy.md, 4.1, 6): the test dataset loads, <c>inherits</c> works, and every
/// mistake in a file is reported at its file, line and column.
/// </summary>
public sealed class RulesLoaderTests
{
    private static RulesDatabase Dataset() => RulesLoader.LoadDirectory(RulesDataset.Folder);

    private static RulesDatabase Load(string text) => RulesLoader.Load([("test.yaml", text)]);

    /// <summary>Reads a snippet without checking its references, for tests of the schema and of inheritance alone.</summary>
    private static RulesDatabase Read(string text)
    {
        var errors = new List<RulesError>();
        var (rules, _) = RulesLoader.Read([("test.yaml", text)], [], errors);
        Assert.Empty(errors);
        return rules;
    }

    /// <summary>The one error that the text gives.</summary>
    private static RulesError Fails(string text) => Assert.Throws<RulesException>(() => Load(text)).Errors.Single();

    [Fact]
    public void The_dataset_loads_every_kind_in_id_order()
    {
        var rules = Dataset();

        Assert.Equal(new[] { "conscript", "harvester", "heavy_tank", "mcv" }, rules.Units.Select(unit => unit.Id));
        Assert.Equal(new[] { "construction_yard", "reactor", "refinery", "war_factory" }, rules.Buildings.Select(building => building.Id));
        Assert.Equal(new[] { "heavy_tank_cannon" }, rules.Weapons.Select(weapon => weapon.Id));
        Assert.Equal(new[] { "foot", "track", "wheel" }, rules.Locomotors.Select(locomotor => locomotor.Id));
        Assert.Equal(new[] { "clear", "road" }, rules.Terrains.Select(terrain => terrain.Id));
        Assert.Equal(new[] { "union" }, rules.Factions.Select(faction => faction.Id));
    }

    [Fact]
    public void Abstract_templates_create_no_definitions()
    {
        var rules = Dataset();

        Assert.False(rules.TryGetUnit("base_tracked_vehicle", out _));
        Assert.False(rules.TryGetBuilding("base_building", out _));
    }

    [Fact]
    public void A_field_inherited_from_a_template_is_read()
    {
        var heavyTank = Dataset().Units.Single(unit => unit.Id == "heavy_tank");

        Assert.Equal("track", heavyTank.Locomotor);
        Assert.Equal(UnitPlacement.Cell, heavyTank.Placement);
        Assert.Equal(2048, heavyTank.TurnRate);
        Assert.True(heavyTank.Turret);
        Assert.Equal(UnitCategory.Vehicle, heavyTank.Category);
    }

    [Fact]
    public void An_own_field_overrides_the_template_and_an_added_one_is_kept()
    {
        var rules = Dataset();

        Assert.False(rules.Units.Single(unit => unit.Id == "mcv").Turret);
        Assert.Equal(8, rules.Units.Single(unit => unit.Id == "heavy_tank").Sight);
        Assert.Equal(new[] { "war_factory" }, rules.Units.Single(unit => unit.Id == "heavy_tank").Prerequisites);
        Assert.Equal("heavy", rules.Units.Single(unit => unit.Id == "heavy_tank").Armor);
    }

    [Fact]
    public void A_list_is_replaced_by_the_child_list_and_not_merged()
    {
        var rules = Read(
            """
            base_unit:
              kind: unit
              abstract: true
              category: vehicle
              placement: cell
              locomotor: track
              turn_rate: 0
              sight: 1
              prerequisites: [a, b]
            child:
              kind: unit
              inherits: base_unit
              name: Child
              faction: union
              cost: 1
              build_time: 1s
              hp: 1
              speed: 1
              prerequisites: [c]
            """);

        Assert.Equal(new[] { "c" }, rules.Units.Single().Prerequisites);
    }

    [Fact]
    public void A_chain_of_inheritance_passes_each_field_down()
    {
        var rules = Read(
            """
            first:
              kind: unit
              abstract: true
              category: infantry
              placement: subcell
              locomotor: foot
              hp: 100
              speed: 7
              turn_rate: 0
              sight: 2
            second:
              kind: unit
              abstract: true
              inherits: first
              cost: 5
            last:
              kind: unit
              inherits: second
              name: Last
              faction: union
              build_time: 1s
            """);

        var last = rules.Units.Single();
        Assert.Equal("last", last.Id);
        Assert.Equal(100, last.Hp);
        Assert.Equal(5, last.Cost);
        Assert.Equal(UnitCategory.Infantry, last.Category);
        Assert.Equal(Stance.Aggressive, last.Stance);
        Assert.False(last.Crusher || last.Crushable);
    }

    [Fact]
    public void A_stance_reads_as_one_of_three_and_another_word_points_at_its_value()
    {
        const string Unit =
            """
            sentry:
              kind: unit
              name: Sentry
              faction: union
              category: infantry
              placement: subcell
              locomotor: foot
              cost: 1
              build_time: 1s
              hp: 1
              speed: 1
              turn_rate: 0
              sight: 2
              stance: STANCE
            """;

        Assert.True(Read(Unit.Replace("STANCE", "guard\n  crushable: true")).Units.Single().Crushable);
        Assert.Equal(Stance.Guard, Read(Unit.Replace("STANCE", "guard")).Units.Single().Stance);
        Assert.Equal(Stance.HoldPosition, Read(Unit.Replace("STANCE", "hold")).Units.Single().Stance);
        var error = Fails(Unit.Replace("STANCE", "passive"));
        Assert.Equal((14, 11), (error.Line, error.Column));
        Assert.Contains("'passive' must be one of aggressive, guard, hold", error.Reason);
    }

    [Fact]
    public void A_building_reads_its_footprint_exit_cell_and_negative_power()
    {
        var factory = Dataset().Buildings.Single(building => building.Id == "war_factory");

        Assert.Equal(new CellSize(3, 4), factory.Footprint);
        Assert.Equal(new CellPos(1, 3), factory.ExitCell);
        Assert.Equal(-50, factory.Power);
        Assert.Equal(ProductionCategory.Building, factory.Category);
        Assert.Equal(new[] { "refinery" }, factory.Prerequisites);
    }

    [Fact]
    public void A_percentage_and_a_duration_read_as_the_fixed_point_and_ticks_do()
    {
        var rules = Dataset();

        Assert.Equal(52429, rules.Locomotors.Single(locomotor => locomotor.Id == "foot").Slope.Raw);
        var road = rules.Terrains.Single(terrain => terrain.Id == "road");
        Assert.Equal(78643, road.Speed.Single(speed => speed.Locomotor == "wheel").Multiplier.Raw);
        Assert.Equal('=', road.Code);
        Assert.True(road.Buildable);

        var cannon = rules.Weapons.Single();
        Assert.Equal(360448, cannon.Range.Raw);
        Assert.Equal(30, cannon.Reload);
    }

    [Fact]
    public void The_combat_kinds_read_every_key_and_leave_out_nothing_but_defaults()
    {
        // M4 step 1: armor, a warhead, a projectile of each motion and a weapon, with every key and with the defaults.
        var rules = Read(
            """
            heavy:
              kind: armor
              name: Heavy
            ap:
              kind: warhead
              name: AP
              verses: { heavy: 100%, none: 25% }
              spread: 0.5
              percent_at_max: 40%
              wall_damage: true
              affects_air: true
              infantry_death: explode
            plain:
              kind: warhead
              name: Plain
              verses: { heavy: 10% }
            rocket:
              kind: projectile
              name: Rocket
              motion: homing
              speed: 30
              turn_rate: 2048
              lifetime: 6s
              arc: 1.5
              inaccuracy: 0.25
              visual: smoke
            bullet:
              kind: projectile
              name: Bullet
              motion: instant
            cannon:
              kind: weapon
              name: Cannon
              damage: 90
              reload: 4.33s
              range: 5.75
              min_range: 1
              projectile: rocket
              warhead: ap
              burst: 2
              burst_delay: 0.2s
              targets: [air, ground]
              report: boom
            rifle:
              kind: weapon
              name: Rifle
              damage: 15
              reload: 1s
              range: 4
              projectile: bullet
              warhead: plain
            """);

        Assert.Equal(["heavy"], rules.Armors.Select(armor => armor.Id));
        var ap = rules.Warheads.Single(warhead => warhead.Id == "ap");
        Assert.Equal(["heavy", "none"], ap.Verses.Select(verse => verse.Armor));
        Assert.Equal((Fixed.One, Fixed.FromRatio(1, 4)), (ap.Verses[0].Percent, ap.Verses[1].Percent));
        Assert.Equal((Fixed.FromRatio(1, 2), true, true, "explode"), (ap.Spread, ap.WallDamage, ap.AffectsAir, ap.InfantryDeath));
        var plain = rules.Warheads.Single(warhead => warhead.Id == "plain");
        Assert.Equal((Fixed.Zero, Fixed.One, false, false, ""), (plain.Spread, plain.PercentAtMax, plain.WallDamage, plain.AffectsAir, plain.InfantryDeath));

        var rocket = rules.Projectiles.Single(projectile => projectile.Id == "rocket");
        Assert.Equal((ProjectileMotion.Homing, 30, 2048, 90, Fixed.FromRatio(3, 2), Fixed.FromRatio(1, 4), "smoke"),
            (rocket.Motion, rocket.Speed, rocket.TurnRate, rocket.Lifetime, rocket.Arc, rocket.Inaccuracy, rocket.Visual));
        Assert.Equal(ProjectileMotion.Instant, rules.Projectiles.Single(projectile => projectile.Id == "bullet").Motion);

        var cannon = rules.Weapons.Single(weapon => weapon.Id == "cannon");
        Assert.Equal((90, 65, Fixed.FromRatio(23, 4), Fixed.One, "rocket", "ap", 2, 3, WeaponTargets.Ground | WeaponTargets.Air, "boom"),
            (cannon.Damage, cannon.Reload, cannon.Range, cannon.MinRange, cannon.Projectile, cannon.Warhead, cannon.Burst, cannon.BurstDelay,
             cannon.Targets, cannon.Report));
        var rifle = rules.Weapons.Single(weapon => weapon.Id == "rifle");
        Assert.Equal((Fixed.Zero, 1, 0, WeaponTargets.Ground, ""), (rifle.MinRange, rifle.Burst, rifle.BurstDelay, rifle.Targets, rifle.Report));
    }

    [Fact]
    public void A_unit_and_a_building_carry_armor_and_weapons()
    {
        var rules = Dataset();

        var heavyTank = rules.Units.Single(unit => unit.Id == "heavy_tank");
        Assert.Equal(("heavy", "heavy_tank_cannon", ""), (heavyTank.Armor, heavyTank.Primary, heavyTank.Secondary));
        Assert.Equal("none", rules.Units.Single(unit => unit.Id == "conscript").Armor);
        Assert.Equal(("steel", ""), (rules.Buildings[0].Armor, rules.Buildings[0].Primary));
    }

    [Fact]
    public void A_file_without_definitions_loads_an_empty_database()
    {
        var rules = Load(string.Empty);

        Assert.Empty(rules.Units);
        Assert.Empty(rules.Buildings);
    }

    [Fact]
    public void An_unknown_key_points_at_the_key()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
              name: Heavy Tank
              hp_max: 400
            """);

        Assert.Equal(("test.yaml", 4, 3), (error.FileName, error.Line, error.Column));
        Assert.Contains("unknown key 'hp_max' in 'heavy_tank'", error.Reason);
    }

    [Fact]
    public void A_required_field_that_is_missing_points_at_the_id()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
              name: Heavy Tank
              faction: union
              category: vehicle
              placement: cell
              locomotor: track
              build_time: 12s
              hp: 400
              speed: 40
              turn_rate: 2048
              sight: 8
            """);

        Assert.Equal(("test.yaml", 1, 1), (error.FileName, error.Line, error.Column));
        Assert.Contains("'heavy_tank' has no 'cost'", error.Reason);
    }

    [Fact]
    public void A_fraction_where_a_whole_number_belongs_points_at_the_value()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
              name: Heavy Tank
              faction: union
              category: vehicle
              placement: cell
              locomotor: track
              cost: 900
              build_time: 12s
              hp: 400
              speed: 40
              turn_rate: 2.5
              sight: 8
            """);

        Assert.Equal((12, 14), (error.Line, error.Column));
        Assert.Contains("'2.5' must be a whole number", error.Reason);
    }

    [Fact]
    public void An_unknown_kind_points_at_its_value()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: spaceship
              name: Heavy Tank
            """);

        Assert.Equal((2, 9), (error.Line, error.Column));
        Assert.Contains("'spaceship' must be one of unit, building, weapon, locomotor, terrain, faction", error.Reason);
    }

    [Fact]
    public void A_category_of_another_kind_points_at_its_value()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
              name: Heavy Tank
              faction: union
              category: building
            """);

        Assert.Equal((5, 13), (error.Line, error.Column));
        Assert.Contains("'building' must be one of infantry, vehicle", error.Reason);
    }

    [Fact]
    public void A_template_with_a_wrong_value_is_still_an_error()
    {
        var error = Fails(
            """
            base:
              kind: unit
              abstract: true
              turn_rate: 2.5
            """);

        Assert.Equal((4, 14), (error.Line, error.Column));
    }

    [Fact]
    public void The_same_id_in_two_files_is_an_error_at_the_second_one()
    {
        var errors = Assert.Throws<RulesException>(() => RulesLoader.Load(
        [
            ("a.yaml", "heavy_tank:\n  kind: unit\n"),
            ("b.yaml", "heavy_tank:\n  kind: unit\n"),
        ])).Errors;

        // The first one has no fields either; that is a separate error in a.yaml.
        var error = errors.Single(error => error.FileName == "b.yaml");

        Assert.Equal(("b.yaml", 1, 1), (error.FileName, error.Line, error.Column));
        Assert.Contains("'heavy_tank' is already defined in a.yaml", error.Reason);
    }

    [Fact]
    public void A_repeated_key_in_one_entry_is_an_error_at_the_second_key()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
              hp: 400
              hp: 500
            """);

        // YamlDotNet itself refuses a repeated key while it builds the document; its message names the key, the position is ours.
        Assert.Equal((4, 3), (error.Line, error.Column));
        Assert.Contains("Duplicate key hp", error.Reason);
    }

    [Fact]
    public void An_invalid_id_is_an_error_at_the_key()
    {
        var error = Fails(
            """
            Heavy-Tank:
              kind: unit
            """);

        Assert.Equal((1, 1), (error.Line, error.Column));
        Assert.Contains("'Heavy-Tank' must be an id", error.Reason);
    }

    [Fact]
    public void An_unknown_parent_is_an_error_at_inherits()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
              inherits: nobody
            """);

        Assert.Equal((3, 13), (error.Line, error.Column));
        Assert.Contains("'heavy_tank' inherits 'nobody', which is not defined", error.Reason);
    }

    [Fact]
    public void An_inheritance_cycle_is_an_error_at_the_inherits_that_closes_it()
    {
        var error = Fails(
            """
            a:
              kind: unit
              inherits: b
            b:
              kind: unit
              inherits: a
            """);

        Assert.Equal((6, 13), (error.Line, error.Column));
        Assert.Contains("inheritance cycle: a -> b -> a", error.Reason);
    }

    [Fact]
    public void A_parent_of_another_kind_is_an_error_at_the_kind()
    {
        var error = Fails(
            """
            base:
              kind: building
              abstract: true
              category: building
            unit_x:
              kind: unit
              inherits: base
            """);

        Assert.Equal((6, 9), (error.Line, error.Column));
        Assert.Contains("'unit_x' is unit, but it inherits 'base', which is building", error.Reason);
    }

    [Fact]
    public void An_anchor_is_an_error_because_definitions_inherit_through_inherits()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
              name: &n Heavy Tank
              faction: *n
            """);

        Assert.Equal(3, error.Line);
        Assert.Contains("anchor '&n' is not used", error.Reason);
    }

    [Fact]
    public void Two_documents_in_one_file_are_an_error()
    {
        var error = Fails(
            """
            heavy_tank:
              kind: unit
            ---
            other:
              kind: unit
            """);

        Assert.Contains("a data file holds one YAML document", error.Reason);
    }

    [Fact]
    public void A_file_that_is_not_a_mapping_is_an_error_at_its_start()
    {
        var error = Fails(
            """
            - heavy_tank
            - other
            """);

        Assert.Equal((1, 1), (error.Line, error.Column));
        Assert.Contains("a data file is a mapping from ids to definitions", error.Reason);
    }
}
