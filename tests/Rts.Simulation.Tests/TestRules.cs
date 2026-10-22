using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests;

/// <summary>
/// The rules of M2 built in code with <see cref="RulesBuilder"/> (docs/implementation/M3_base_economy.md, 4.5), so that the tests of the
/// simulation need no YAML. The numbers the simulation reads are those of <c>game/data</c>: a test of Rts.Data, which links this file,
/// checks that both give the same <see cref="GameRules"/>. The buildings of the Union (footprint, power, build-up, base rules,
/// prerequisites) and the prerequisites of the units are those of <c>game/data</c> too; costs, hit points and build times of the units
/// are placeholders, their hit points are those of <c>game/data</c>. The combat of M4 (armor, warheads, projectiles, weapons and who carries what) is that of <c>game/data</c>.
/// </summary>
internal static class TestRules
{
    /// <summary>The rules every simulation test plays by.</summary>
    public static GameRules Standard { get; } = GameRules.From(Database());

    /// <summary>The rules of one unit type.</summary>
    public static UnitArchetype Unit(UnitType type) => Standard.Unit(type);

    public static RulesDatabase Database()
    {
        var builder = new RulesBuilder();
        // Slope multipliers: foot, track, wheel, amphibious, naval, hover.
        int[] slopes = [80, 75, 65, 65, 100, 65];
        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            builder.Add(new LocomotorDef
            {
                Id = LandSpeeds.IdOf(locomotor),
                Name = LandSpeeds.IdOf(locomotor),
                Slope = Percent(slopes[(int)locomotor - 1]),
            });
        }

        int[][] speeds =
        [
            [100, 100, 100, 100, 0, 100],   // Clear
            [90, 90, 75, 75, 0, 75],        // Rough
            [100, 100, 120, 100, 0, 100],   // Road
            [90, 80, 70, 100, 0, 100],      // Beach
            [0, 0, 0, 100, 0, 100],         // Shallow
            [0, 0, 0, 100, 100, 100],       // Deep
            [0, 0, 0, 0, 0, 0],             // Rock
            [80, 90, 90, 100, 0, 100],      // Ice
        ];
        for (var land = LandType.Clear; land <= LandType.Ice; land++)
        {
            var row = ImmutableArray.CreateBuilder<LocomotorSpeed>(LandSpeeds.LocomotorCount);
            for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
            {
                row.Add(new LocomotorSpeed(LandSpeeds.IdOf(locomotor), Percent(speeds[(int)land - 1][(int)locomotor - 1])));
            }

            builder.Add(new TerrainDef
            {
                Id = LandSpeeds.IdOf(land),
                Name = LandSpeeds.IdOf(land),
                Code = TestMapParser.CodeOf(land),
                Buildable = land is LandType.Clear or LandType.Rough or LandType.Road or LandType.Beach,
                Speed = row.MoveToImmutable().Sort((a, b) => string.CompareOrdinal(a.Locomotor, b.Locomotor)),
            });
        }

        // Speed in leptons per tick, turn rate in BAngle units per tick, sight in cells: the M2 values of each type.
        builder.Add(InfantryDef(UnitType.Conscript, sight: 5) with { Armor = "flak", Primary = "conscript_rifle" });
        builder.Add(InfantryDef(UnitType.Rocketeer, sight: 5) with { Primary = "rocketeer_rocket" });
        builder.Add(InfantryDef(UnitType.Engineer, sight: 4));
        Vehicle(builder, UnitType.Mcv, Locomotor.Track, speed: 24, turret: false, sight: 6);
        builder.Add(VehicleDef(UnitType.Harvester, Locomotor.Track, speed: 24, turret: true, sight: 4, "war_factory", "refinery") with
        {
            HarvestCapacity = 1000,
            HarvestTime = 12,
            UnloadRate = 50,
            Armor = "medium",
            Primary = "harvester_machine_gun",
        });
        builder.Add(VehicleDef(UnitType.SupportVehicle, Locomotor.Wheel, speed: 48, turret: true, sight: 8) with
        {
            Primary = "flak_track_gun",
            Secondary = "flak_track_aa_gun",
        });
        builder.Add(VehicleDef(UnitType.HeavyTank, Locomotor.Track, speed: 36, turret: true, sight: 8) with { Primary = "heavy_tank_cannon" });
        builder.Add(VehicleDef(UnitType.TeslaTank, Locomotor.Track, speed: 36, turret: true, sight: 8, "war_factory", "tech_center") with
        {
            Primary = "tesla_tank_bolt",
        });
        Combat(builder);

        // The buildings of docs/implementation/M3_base_economy.md, 4.6: id, footprint, power, prerequisites.
        builder.Add(Def("construction_yard", 3, 3, power: 0) with
        {
            Produces = [ProductionCategory.Building, ProductionCategory.Defense],
            Armor = "concrete",
        });
        Building(builder, "reactor", 2, 2, power: 150, "construction_yard");
        builder.Add(Def("refinery", 3, 3, power: -50, "reactor") with { DockCell = new CellPos(1, 3) });
        builder.Add(Def("barracks", 2, 2, power: -10, "reactor") with
        {
            Armor = "steel",
            Produces = [ProductionCategory.Infantry],
            ExitCell = new CellPos(1, 2),
        });
        builder.Add(Def("war_factory", 3, 4, power: -25, "refinery") with
        {
            Produces = [ProductionCategory.Vehicle],
            ExitCell = new CellPos(1, 4),
        });
        builder.Add(Def("radar", 2, 2, power: -50, "refinery") with { Radar = true });
        Building(builder, "tech_center", 3, 3, power: -100, "war_factory", "radar");
        builder.Add(Def("wall", 1, 1, power: 0, "construction_yard") with
        {
            ExtendsBase = false,
            Category = ProductionCategory.Defense,
            Armor = "concrete",
        });
        builder.Add(Defense("sentry_bunker", 1, 1, power: 0, "sentry_machine_gun", "barracks"));
        builder.Add(Defense("tesla_coil", 2, 2, power: -75, "tesla_coil_bolt", "radar"));
        builder.Add(Defense("flak_cannon", 2, 2, power: -50, "flak_cannon_gun", "radar"));
        builder.Add(new EconomyDef
        {
            Id = "economy",
            Name = "Economy",
            LowPowerMinRate = Percent(25),
            MultipleFactoryBonus = Percent(50),
            MaxFactoryRate = Percent(300),
            OreValue = 25,
            GemValue = 50,
            OreGrowthInterval = 450,
            OreGrowthChance = Percent(20),
            OreSpreadStage = 6,
            OreSpreadChance = Percent(5),
            SellRefund = Percent(50),
            RepairCost = Percent(50),
            RepairRate = 5,
        });
        builder.Add(new FactionDef
        {
            Id = "union",
            Name = "Union",
            Mcv = "mcv",
            ConstructionYard = "construction_yard",
            Refinery = "refinery",
            Harvester = "harvester",
        });
        return builder.Build();
    }

    /// <summary>A building of the Union from <see cref="Database"/>.</summary>
    public static BuildingDef BuildingDef(string id) =>
        Standard.Database.TryGetBuilding(id, out var definition) ? definition : throw new System.ArgumentException($"No building '{id}'.");

    private static void Building(RulesBuilder builder, string id, int width, int height, int power, params string[] prerequisites) =>
        builder.Add(Def(id, width, height, power, prerequisites));

    // Grows in 1.5 s, 23 ticks, as the template of game/data.
    private static BuildingDef Def(string id, int width, int height, int power, params string[] prerequisites) =>
        new()
        {
            Id = id,
            Name = id,
            Faction = "union",
            Category = ProductionCategory.Building,
            Cost = 100,
            BuildTime = 15,
            Hp = 500,
            Footprint = new CellSize(width, height),
            Power = power,
            BuildUp = 23,
            Prerequisites = [.. prerequisites],
            Armor = "wood",
        };

    private static BuildingDef Defense(string id, int width, int height, int power, string weapon, params string[] prerequisites) =>
        Def(id, width, height, power, prerequisites) with { Category = ProductionCategory.Defense, Armor = "steel", Primary = weapon };

    // Rounded to the nearest, as the parser of the data files reads "5%".
    private static Fixed Percent(int percent) => Fixed.FromRaw(IntMath.DivRound(percent * Fixed.OneRaw, 100));

    // Rounded to the nearest, as the parser of the data files reads "0.3".
    private static Fixed Tenths(int tenths) => Fixed.FromRaw(IntMath.DivRound(tenths * Fixed.OneRaw, 10));

    // The armor types of game/data/combat/armor.yaml, in the order the verses below list their shares. A property: Standard, above,
    // is made before the static fields below it.
    private static string[] Armors => ["none", "flak", "plate", "light", "medium", "heavy", "wood", "steel", "concrete", "special_1", "special_2"];

    /// <summary>The armor, warheads, projectiles and weapons of game/data (M4 step 1), the same numbers.</summary>
    private static void Combat(RulesBuilder builder)
    {
        foreach (var armor in Armors)
        {
            builder.Add(new ArmorDef { Id = armor, Name = armor });
        }

        Warhead(builder, "small_arms", [100, 80, 70, 50, 25, 25, 75, 50, 25, 100, 100], "normal");
        Warhead(builder, "ap_shell", [25, 25, 25, 75, 100, 100, 65, 45, 45, 100, 100], "explode", spread: Tenths(3), atMax: 50, wallDamage: true);
        Warhead(builder, "electric", [100, 100, 100, 85, 100, 100, 50, 50, 50, 100, 100], "electro");
        Warhead(builder, "flak_ground", [150, 100, 50, 60, 10, 10, 30, 10, 10, 100, 100], "explode", spread: Fixed.One, atMax: 25);
        Warhead(builder, "flak_air", [100, 100, 100, 100, 20, 20, 0, 0, 0, 100, 100], "", spread: Fixed.One, atMax: 10, affectsAir: true);
        Warhead(builder, "light_mg", [100, 80, 70, 50, 20, 20, 20, 10, 10, 100, 100], "normal");
        Warhead(builder, "missile", [25, 25, 25, 75, 100, 100, 50, 50, 25, 100, 100], "explode", spread: Tenths(3), atMax: 50);

        builder.Add(new ProjectileDef { Id = "bullet", Name = "bullet", Motion = ProjectileMotion.Instant });
        builder.Add(new ProjectileDef { Id = "bolt", Name = "bolt", Motion = ProjectileMotion.Instant, Visual = "electric_bolt" });
        builder.Add(new ProjectileDef { Id = "shell", Name = "shell", Motion = ProjectileMotion.Ballistic, Speed = 40, Arc = Tenths(5) });
        builder.Add(new ProjectileDef
        {
            Id = "flak_shell",
            Name = "flak_shell",
            Motion = ProjectileMotion.Ballistic,
            Speed = 40,
            Arc = Fixed.One,
            Inaccuracy = Tenths(5),
        });
        builder.Add(new ProjectileDef { Id = "rocket", Name = "rocket", Motion = ProjectileMotion.Homing, Speed = 30, TurnRate = 2048, Lifetime = 90 });

        // Damage, reload in ticks, range in cells (quarters for 5.75 and halves for 5.5), projectile, warhead.
        Weapon(builder, "conscript_rifle", 15, 25, Fixed.FromInt(4), "bullet", "small_arms");
        Weapon(builder, "rocketeer_rocket", 50, 60, Fixed.FromInt(6), "rocket", "missile");
        Weapon(builder, "heavy_tank_cannon", 90, 65, Fixed.FromRatio(23, 4), "shell", "ap_shell");
        Weapon(builder, "tesla_tank_bolt", 135, 75, Fixed.FromInt(4), "bolt", "electric");
        Weapon(builder, "flak_track_gun", 25, 40, Fixed.FromInt(5), "flak_shell", "flak_ground");
        Weapon(builder, "flak_track_aa_gun", 35, 25, Fixed.FromInt(10), "bullet", "flak_air", WeaponTargets.Air);
        Weapon(builder, "harvester_machine_gun", 30, 20, Fixed.FromRatio(11, 2), "bullet", "light_mg");
        Weapon(builder, "sentry_machine_gun", 50, 26, Fixed.FromRatio(11, 2), "bullet", "small_arms");
        Weapon(builder, "tesla_coil_bolt", 200, 80, Fixed.FromInt(7), "bolt", "electric");
        Weapon(builder, "flak_cannon_gun", 40, 20, Fixed.FromInt(12), "flak_shell", "flak_air", WeaponTargets.Air);
    }

    private static void Warhead(
        RulesBuilder builder, string id, int[] verses, string death, Fixed spread = default, int atMax = 100, bool wallDamage = false,
        bool affectsAir = false) =>
        builder.Add(new WarheadDef
        {
            Id = id,
            Name = id,
            Verses = Armors.Select((armor, i) => new ArmorPercent(armor, Percent(verses[i]))).OrderBy(verse => verse.Armor, System.StringComparer.Ordinal)
                .ToImmutableArray(),
            Spread = spread,
            PercentAtMax = Percent(atMax),
            WallDamage = wallDamage,
            AffectsAir = affectsAir,
            InfantryDeath = death,
        });

    private static void Weapon(
        RulesBuilder builder, string id, int damage, int reload, Fixed range, string projectile, string warhead,
        WeaponTargets targets = WeaponTargets.Ground) =>
        builder.Add(new WeaponDef
        {
            Id = id,
            Name = id,
            Damage = damage,
            Reload = reload,
            Range = range,
            Projectile = projectile,
            Warhead = warhead,
            Targets = targets,
        });

    private static UnitDef InfantryDef(UnitType type, int sight) =>
        Unit(type, UnitCategory.Infantry, Locomotor.Foot, UnitPlacement.Subcell, speed: 24, turnRate: 0, turret: false, sight, ["barracks"]) with
        {
            Armor = "none",
        };

    private static void Vehicle(
        RulesBuilder builder, UnitType type, Locomotor locomotor, int speed, bool turret, int sight, params string[] prerequisites) =>
        builder.Add(VehicleDef(type, locomotor, speed, turret, sight, prerequisites));

    private static UnitDef VehicleDef(UnitType type, Locomotor locomotor, int speed, bool turret, int sight, params string[] prerequisites) =>
        Unit(
            type, UnitCategory.Vehicle, locomotor, UnitPlacement.Cell, speed, turnRate: 2048, turret, sight,
            prerequisites.Length > 0 ? prerequisites : ["war_factory"]) with
        {
            Armor = "heavy",
        };

    // The hit points of game/data (M4 step 2): the archetypes of both rules must be the same.
    private static int UnitHp(UnitType type) => type switch
    {
        UnitType.Conscript => 125,
        UnitType.Rocketeer => 100,
        UnitType.Engineer => 75,
        UnitType.Mcv or UnitType.Harvester => 1000,
        UnitType.SupportVehicle => 180,
        UnitType.HeavyTank => 400,
        _ => 300,
    };

    private static UnitDef Unit(
        UnitType type, UnitCategory category, Locomotor locomotor, UnitPlacement placement, int speed, int turnRate, bool turret, int sight,
        string[] prerequisites) =>
        new()
        {
            Id = UnitTypes.IdOf(type),
            Name = UnitTypes.IdOf(type),
            Faction = "union",
            Category = category,
            Cost = 100,
            BuildTime = 15,
            Hp = UnitHp(type),
            Speed = speed,
            TurnRate = turnRate,
            Sight = sight,
            Locomotor = LandSpeeds.IdOf(locomotor),
            Placement = placement,
            Turret = turret,
            Prerequisites = [.. prerequisites],
        };
}
