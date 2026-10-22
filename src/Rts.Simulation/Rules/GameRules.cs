using System;
using System.Collections.Generic;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Rules;

/// <summary>
/// A data mistake that only the engine can see: a definition it needs is missing, or a value it cannot use
/// (docs/implementation/M3_base_economy.md, 4.5). <see cref="Id"/> and <see cref="Key"/> name where it is written; an empty
/// <see cref="Id"/> means the whole data, an empty <see cref="Key"/> the whole definition.
/// </summary>
public readonly record struct RulesProblem(string Id, string Key, string Reason);

/// <summary>
/// The rules of a match as the simulation reads them every tick (docs/implementation/M3_base_economy.md, 4.5): made once from a
/// <see cref="RulesDatabase"/>, with the unit types of M2 (<see cref="UnitArchetype"/> by <see cref="UnitType"/>) and the land speeds
/// (<see cref="LandSpeeds"/>). The data must define every unit type, locomotor and land type the engine knows, by their ids;
/// <see cref="Problems"/> says what is missing. Other definitions may be there too: the buildings are used from M3 step 6 on.
/// </summary>
public sealed class GameRules
{
    private readonly UnitArchetype[] _units;
    private readonly bool[] _buildable;

    private GameRules(RulesDatabase database, UnitArchetype[] units, LandSpeeds land, bool[] buildable)
    {
        Database = database;
        _units = units;
        Land = land;
        _buildable = buildable;
    }

    /// <summary>The definitions the rules are made from.</summary>
    public RulesDatabase Database { get; }

    /// <summary>The speeds of every locomotor on every land type: the map of a match goes by them.</summary>
    public LandSpeeds Land { get; }

    /// <summary>The unit types, in <see cref="UnitType"/> order.</summary>
    public IReadOnlyList<UnitArchetype> Units => _units;

    /// <summary>The faction every player plays until the skirmish setup chooses one (M6): the first of the data, in id order.</summary>
    public FactionDef DefaultFaction => Database.Factions[0];

    /// <summary>The general rules of the economy: the one <c>economy</c> record of the data.</summary>
    public EconomyDef Economy => Database.Economies[0];

    /// <summary>Whether a building may stand on <paramref name="land"/>: <c>buildable</c> of the land type in the data.</summary>
    public bool IsBuildable(LandType land) => LandSpeeds.IsKnown(land) && _buildable[(int)land - 1];

    public UnitArchetype Unit(UnitType type) => UnitTypes.IsKnown(type)
        ? _units[(int)type - 1]
        : throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown unit type.");

    /// <summary>The rules made from <paramref name="database"/>.</summary>
    /// <exception cref="ArgumentException">The data has <see cref="Problems"/>; the message lists them.</exception>
    public static GameRules From(RulesDatabase database)
    {
        var problems = Problems(database);
        if (problems.Count > 0)
        {
            var reasons = new List<string>(problems.Count);
            foreach (var problem in problems)
            {
                reasons.Add(problem.Reason);
            }

            throw new ArgumentException($"The data cannot be played: {string.Join("; ", reasons)}.", nameof(database));
        }

        var units = new UnitArchetype[UnitTypes.All.Count];
        foreach (var type in UnitTypes.All)
        {
            database.TryGetUnit(UnitTypes.IdOf(type), out var unit);
            units[(int)type - 1] = new UnitArchetype(
                type,
                unit.Id,
                unit.Category,
                LocomotorOf(unit.Locomotor)!.Value,
                unit.Placement,
                unit.Speed,
                unit.TurnRate,
                unit.Turret,
                unit.Sight);
        }

        var percent = new int[LandSpeeds.LandCount, LandSpeeds.LocomotorCount];
        var slope = new int[LandSpeeds.LocomotorCount];
        var buildable = new bool[LandSpeeds.LandCount];
        for (var land = LandType.Clear; land <= LandType.Ice; land++)
        {
            database.TryGetTerrain(LandSpeeds.IdOf(land), out var terrain);
            buildable[(int)land - 1] = terrain.Buildable;
        }

        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            database.TryGetLocomotor(LandSpeeds.IdOf(locomotor), out var definition);
            slope[(int)locomotor - 1] = PercentOf(definition.Slope);
            for (var land = LandType.Clear; land <= LandType.Ice; land++)
            {
                database.TryGetTerrain(LandSpeeds.IdOf(land), out var terrain);
                percent[(int)land - 1, (int)locomotor - 1] = PercentOf(SpeedOn(terrain, LandSpeeds.IdOf(locomotor)));
            }
        }

        return new GameRules(database, units, new LandSpeeds(percent, slope), buildable);
    }

    /// <summary>
    /// What keeps the engine from playing <paramref name="database"/>: an engine locomotor, land type or unit type without a definition,
    /// a land code other than the one of the map format, a locomotor the engine does not know, a speed over 255 percent, no faction.
    /// </summary>
    public static List<RulesProblem> Problems(RulesDatabase database)
    {
        var problems = new List<RulesProblem>();
        if (database.Factions.Length == 0)
        {
            problems.Add(new RulesProblem("", "", "the engine needs a faction for the players, and none is defined"));
        }

        if (database.Economies.Length != 1)
        {
            problems.Add(new RulesProblem(
                database.Economies.Length > 1 ? database.Economies[1].Id : "",
                "",
                $"the engine needs exactly one economy record, and there are {database.Economies.Length}"));
        }

        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            if (!database.TryGetLocomotor(LandSpeeds.IdOf(locomotor), out _))
            {
                problems.Add(new RulesProblem("", "", $"the engine needs locomotor '{LandSpeeds.IdOf(locomotor)}', which is not defined"));
            }
        }

        foreach (var locomotor in database.Locomotors)
        {
            if (LocomotorOf(locomotor.Id) is null)
            {
                problems.Add(new RulesProblem(locomotor.Id, "", $"the engine has no locomotor '{locomotor.Id}'; it knows {KnownLocomotors()}"));
            }
        }

        for (var land = LandType.Clear; land <= LandType.Ice; land++)
        {
            var id = LandSpeeds.IdOf(land);
            if (!database.TryGetTerrain(id, out var terrain))
            {
                problems.Add(new RulesProblem("", "", $"the engine needs land type '{id}', which is not defined"));
                continue;
            }

            var code = TestMapParser.CodeOf(land);
            if (terrain.Code != code)
            {
                problems.Add(new RulesProblem(id, "code", $"'{id}' is written '{code}' in map files, so its code must be \"{code}\""));
            }

            foreach (var speed in terrain.Speed)
            {
                if (PercentOf(speed.Multiplier) > 255)
                {
                    problems.Add(new RulesProblem(id, "speed", $"the speed of '{speed.Locomotor}' on '{id}' must be 255% at most"));
                }
            }
        }

        foreach (var type in UnitTypes.All)
        {
            var id = UnitTypes.IdOf(type);
            if (!database.TryGetUnit(id, out var unit))
            {
                problems.Add(new RulesProblem("", "", $"the engine needs unit '{id}', which is not defined"));
            }
            else if (LocomotorOf(unit.Locomotor) is null)
            {
                problems.Add(new RulesProblem(id, "locomotor", $"the engine has no locomotor '{unit.Locomotor}'; it knows {KnownLocomotors()}"));
            }
        }

        return problems;
    }

    private static Locomotor? LocomotorOf(string id)
    {
        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            if (string.Equals(LandSpeeds.IdOf(locomotor), id, StringComparison.Ordinal))
            {
                return locomotor;
            }
        }

        return null;
    }

    private static string KnownLocomotors()
    {
        var ids = new List<string>(LandSpeeds.LocomotorCount);
        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            ids.Add(LandSpeeds.IdOf(locomotor));
        }

        return string.Join(", ", ids);
    }

    /// <summary>The multiplier of a locomotor on a land type; zero when the land type does not list it.</summary>
    private static Fixed SpeedOn(TerrainDef terrain, string locomotor)
    {
        foreach (var speed in terrain.Speed)
        {
            if (speed.Locomotor == locomotor)
            {
                return speed.Multiplier;
            }
        }

        return Fixed.Zero;
    }

    /// <summary>A fraction as whole percent, rounded to the nearest: 0.9 (written <c>90%</c>) is 90.</summary>
    private static int PercentOf(Fixed fraction) =>
        (int)IntMath.Clamp(IntMath.DivRound(fraction.Raw * 100, Fixed.OneRaw), int.MinValue, int.MaxValue);
}
