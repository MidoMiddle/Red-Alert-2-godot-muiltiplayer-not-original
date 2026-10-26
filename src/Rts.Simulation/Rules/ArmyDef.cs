using System.Collections.Immutable;
using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>One kind of unit in an army and its share of it, as a fraction: 0.4 for 40 percent.</summary>
public readonly record struct UnitShare(string Unit, Fixed Share);

/// <summary>
/// What a part of an army is for: the kind of enemy it is bought to fight. The bot picks the units of a role itself, by what their weapons
/// do to that enemy (Rts.AI <c>UnitKnowledge</c>). Values go into the data hash: never renumber.
/// </summary>
public enum ArmyRole : byte
{
    /// <summary>Soldiers on foot.</summary>
    AntiInfantry = 1,

    /// <summary>Vehicles on the ground; for the ships of the fleet, ships and what is under the water.</summary>
    AntiArmor = 2,

    /// <summary>What flies: aircraft and soldiers with jet packs.</summary>
    AntiAir = 3,

    /// <summary>Buildings and defenses.</summary>
    AntiBuilding = 4,
}

/// <summary>One role in an army and its share of it, as a fraction.</summary>
public readonly record struct RoleShare(ArmyRole Role, Fixed Share);

/// <summary>
/// The army a bot builds (docs/implementation/M5_first_bot.md, 4.6–4.7, step 6). Either the share of every kind of unit, and which of
/// them gain when the enemy fields mostly infantry or mostly vehicles; or (M11, the bot that knows every weapon) the share of every
/// <see cref="ArmyRole"/>, and of the fleet on a map whose water leads to the enemy: the units of a role are those of the faction that
/// fight that enemy best, worked out by the bot from their weapons. Data like the rest: checked as it loads, its hash goes with the replay.
/// </summary>
public sealed record ArmyDef : IDefinition
{
    public required string Id { get; init; }

    public required string Faction { get; init; }

    /// <summary>The shares of the kinds of units, sorted by unit id; together a whole. Empty for an army of <see cref="Roles"/>.</summary>
    public ImmutableArray<UnitShare> Shares { get; init; } = ImmutableArray<UnitShare>.Empty;

    /// <summary>The shares of the roles, sorted by role; together a whole. Empty for an army of <see cref="Shares"/>.</summary>
    public ImmutableArray<RoleShare> Roles { get; init; } = ImmutableArray<RoleShare>.Empty;

    /// <summary>
    /// The share of the army that is ships, on a map whose deep water joins the base to an enemy's (an army of <see cref="Roles"/>); the
    /// fleet has the same roles. Zero for none.
    /// </summary>
    public Fixed Navy { get; init; }

    /// <summary>The units that gain against an enemy of mostly infantry — those that crush or mow it down.</summary>
    public ImmutableArray<string> AgainstInfantry { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>The units that gain against an enemy of mostly vehicles.</summary>
    public ImmutableArray<string> AgainstVehicles { get; init; } = ImmutableArray<string>.Empty;
}
