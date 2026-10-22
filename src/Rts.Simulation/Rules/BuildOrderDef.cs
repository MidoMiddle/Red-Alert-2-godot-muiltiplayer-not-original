using System.Collections.Immutable;

namespace Rts.Simulation.Rules;

/// <summary>
/// A build order of the bots (docs/implementation/M5_first_bot.md, 4.7, step 5): the buildings of a faction in the order a bot builds
/// them first. Data like the rest, so it is checked as it loads and its hash goes with the replay: a bot runs on every machine of a
/// match and must build the same.
/// </summary>
public sealed record BuildOrderDef : IDefinition
{
    public required string Id { get; init; }

    public required string Faction { get; init; }

    /// <summary>The buildings in order; one may come twice, such as a second refinery.</summary>
    public required ImmutableArray<string> Steps { get; init; }
}
