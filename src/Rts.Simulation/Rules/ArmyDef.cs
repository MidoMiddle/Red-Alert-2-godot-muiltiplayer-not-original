using System.Collections.Immutable;
using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>One kind of unit in an army and its share of it, as a fraction: 0.4 for 40 percent.</summary>
public readonly record struct UnitShare(string Unit, Fixed Share);

/// <summary>
/// The army a bot builds (docs/implementation/M5_first_bot.md, 4.6–4.7, step 6): the share of every kind of unit, and which of them gain
/// when the enemy fields mostly infantry or mostly vehicles. Data like the rest: checked as it loads, its hash goes with the replay.
/// </summary>
public sealed record ArmyDef : IDefinition
{
    public required string Id { get; init; }

    public required string Faction { get; init; }

    /// <summary>The shares of the kinds of units, sorted by unit id; together a whole.</summary>
    public required ImmutableArray<UnitShare> Shares { get; init; }

    /// <summary>The units that gain against an enemy of mostly infantry — those that crush or mow it down.</summary>
    public ImmutableArray<string> AgainstInfantry { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>The units that gain against an enemy of mostly vehicles.</summary>
    public ImmutableArray<string> AgainstVehicles { get; init; } = ImmutableArray<string>.Empty;
}
