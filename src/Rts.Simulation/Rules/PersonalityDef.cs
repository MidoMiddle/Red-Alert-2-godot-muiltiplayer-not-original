using System.Collections.Immutable;
using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>What the strategist of a personality weighs, every weight 0 or more; they need not make a whole.</summary>
public readonly record struct PersonalityWeights(Fixed Economy, Fixed Defense, Fixed Army, Fixed Tech, Fixed Harassment);

/// <summary>The army of a personality for one faction: an <c>army</c> of the data.</summary>
public readonly record struct FactionArmy(string Faction, string Army);

/// <summary>The build orders of a personality for one faction, one for every quality a level asks for: <c>build_order</c>s of the data.</summary>
public readonly record struct FactionBuildOrders(string Faction, string Simple, string Standard, string Optimized);

/// <summary>
/// A personality of the bots (docs/implementation/M8_ai_difficulty.md, 4.7, step 1): what its strategist weighs, when it attacks first and
/// how often after, when a squad turns back, what it hits first, and its army and build orders for every faction. A level says how well a
/// bot plays (<see cref="DifficultyDef"/>), a personality how it plays. Data like the rest: checked as it loads and hashed.
/// </summary>
public sealed record PersonalityDef : IDefinition
{
    /// <summary>The targets a squad may go for, in the words of the data.</summary>
    public static readonly ImmutableArray<string> Targets = ["production", "army", "power", "harvesters", "defense", "core"];

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required PersonalityWeights Weights { get; init; }

    /// <summary>The army for every faction, sorted by faction id.</summary>
    public required ImmutableArray<FactionArmy> Armies { get; init; }

    /// <summary>The build orders for every faction, sorted by faction id.</summary>
    public required ImmutableArray<FactionBuildOrders> BuildOrders { get; init; }

    /// <summary>The earliest tick of the first attack, for the levels that leave it to the personality.</summary>
    public required int FirstAttackMin { get; init; }

    /// <summary>The latest tick of the first attack; not before <see cref="FirstAttackMin"/>.</summary>
    public required int FirstAttackMax { get; init; }

    /// <summary>The ticks between two waves.</summary>
    public required int WaveInterval { get; init; }

    /// <summary>A fighting squad turns back when its strength falls below this share of the enemy's near it: 0.7.</summary>
    public required Fixed RetreatThreshold { get; init; }

    /// <summary>Every word of <see cref="Targets"/> once, the first the one a squad goes for first.</summary>
    public required ImmutableArray<string> TargetPriority { get; init; }
}
