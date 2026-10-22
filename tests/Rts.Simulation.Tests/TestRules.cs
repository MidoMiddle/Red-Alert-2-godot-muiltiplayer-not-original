using System.Collections.Immutable;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests;

/// <summary>
/// The rules of M2 built in code with <see cref="RulesBuilder"/> (docs/implementation/M3_base_economy.md, 4.5), so that the tests of the
/// simulation need no YAML. The numbers the simulation reads are those of <c>game/data</c>: a test of Rts.Data, which links this file,
/// checks that both give the same <see cref="GameRules"/>. Costs, hit points and build times are placeholders; nothing reads them yet.
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
        Vehicle(builder, UnitType.Harvester, Locomotor.Track, speed: 24, turret: true, sight: 4);
        Vehicle(builder, UnitType.SupportVehicle, Locomotor.Wheel, speed: 48, turret: true, sight: 8);
        Vehicle(builder, UnitType.HeavyTank, Locomotor.Track, speed: 36, turret: true, sight: 8);
        Vehicle(builder, UnitType.TeslaTank, Locomotor.Track, speed: 36, turret: true, sight: 8);
        return builder.Build();
    }

    private static Fixed Percent(int percent) => Fixed.FromRatio(percent, 100);

    private static void Infantry(RulesBuilder builder, UnitType type, int sight) =>
        builder.Add(Unit(type, UnitCategory.Infantry, Locomotor.Foot, UnitPlacement.Subcell, speed: 24, turnRate: 0, turret: false, sight));

    private static void Vehicle(RulesBuilder builder, UnitType type, Locomotor locomotor, int speed, bool turret, int sight) =>
        builder.Add(Unit(type, UnitCategory.Vehicle, locomotor, UnitPlacement.Cell, speed, turnRate: 2048, turret, sight));

    private static UnitDef Unit(
        UnitType type, UnitCategory category, Locomotor locomotor, UnitPlacement placement, int speed, int turnRate, bool turret, int sight) =>
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
        };
}
