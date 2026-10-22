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
/// are placeholders.
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
        Infantry(builder, UnitType.Conscript, sight: 5);
        Infantry(builder, UnitType.Rocketeer, sight: 5);
        Infantry(builder, UnitType.Engineer, sight: 4);
        Vehicle(builder, UnitType.Mcv, Locomotor.Track, speed: 24, turret: false, sight: 6);
        builder.Add(VehicleDef(UnitType.Harvester, Locomotor.Track, speed: 24, turret: true, sight: 4, "war_factory", "refinery") with
        {
            HarvestCapacity = 1000,
            HarvestTime = 12,
            UnloadRate = 50,
        });
        Vehicle(builder, UnitType.SupportVehicle, Locomotor.Wheel, speed: 48, turret: true, sight: 8);
        Vehicle(builder, UnitType.HeavyTank, Locomotor.Track, speed: 36, turret: true, sight: 8);
        Vehicle(builder, UnitType.TeslaTank, Locomotor.Track, speed: 36, turret: true, sight: 8, "war_factory", "tech_center");

        // The buildings of docs/implementation/M3_base_economy.md, 4.6: id, footprint, power, prerequisites.
        builder.Add(Def("construction_yard", 3, 3, power: 0) with { Produces = [ProductionCategory.Building, ProductionCategory.Defense] });
        Building(builder, "reactor", 2, 2, power: 150, "construction_yard");
        builder.Add(Def("refinery", 3, 3, power: -50, "reactor") with { DockCell = new CellPos(1, 3) });
        builder.Add(Def("barracks", 2, 2, power: -10, "reactor") with
        {
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
        builder.Add(Def("wall", 1, 1, power: 0, "construction_yard") with { ExtendsBase = false, Category = ProductionCategory.Defense });
        builder.Add(Def("sentry_bunker", 1, 1, power: 0, "barracks") with { Category = ProductionCategory.Defense });
        builder.Add(Def("tesla_coil", 2, 2, power: -75, "radar") with { Category = ProductionCategory.Defense });
        builder.Add(Def("flak_cannon", 2, 2, power: -50, "radar") with { Category = ProductionCategory.Defense });
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
        };

    // Rounded to the nearest, as the parser of the data files reads "5%".
    private static Fixed Percent(int percent) => Fixed.FromRaw(IntMath.DivRound(percent * Fixed.OneRaw, 100));

    private static void Infantry(RulesBuilder builder, UnitType type, int sight) =>
        builder.Add(Unit(type, UnitCategory.Infantry, Locomotor.Foot, UnitPlacement.Subcell, speed: 24, turnRate: 0, turret: false, sight, ["barracks"]));

    private static void Vehicle(
        RulesBuilder builder, UnitType type, Locomotor locomotor, int speed, bool turret, int sight, params string[] prerequisites) =>
        builder.Add(VehicleDef(type, locomotor, speed, turret, sight, prerequisites));

    private static UnitDef VehicleDef(UnitType type, Locomotor locomotor, int speed, bool turret, int sight, params string[] prerequisites) =>
        Unit(
            type, UnitCategory.Vehicle, locomotor, UnitPlacement.Cell, speed, turnRate: 2048, turret, sight,
            prerequisites.Length > 0 ? prerequisites : ["war_factory"]);

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
            Hp = 100,
            Speed = speed,
            TurnRate = turnRate,
            Sight = sight,
            Locomotor = LandSpeeds.IdOf(locomotor),
            Placement = placement,
            Turret = turret,
            Prerequisites = [.. prerequisites],
        };
}
