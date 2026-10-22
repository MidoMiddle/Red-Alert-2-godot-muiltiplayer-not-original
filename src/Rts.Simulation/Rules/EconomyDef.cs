using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>
/// The general rules of the economy (docs/implementation/M3_base_economy.md, 4.10): one record of the data, <c>economy</c>. Production
/// multipliers now; selling, repairs and ore join it in M3 steps 9–11.
/// </summary>
public sealed record EconomyDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The slowest production under low power, as a fraction: 0.25 for 25 percent.</summary>
    public required Fixed LowPowerMinRate { get; init; }

    /// <summary>What every factory of a category beyond the first adds to its speed, as a fraction: 0.5 for 50 percent.</summary>
    public required Fixed MultipleFactoryBonus { get; init; }

    /// <summary>The fastest a category can produce with many factories, as a fraction: 3 for 300 percent.</summary>
    public required Fixed MaxFactoryRate { get; init; }
}
