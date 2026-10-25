using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>
/// The general rules of the economy (docs/implementation/M3_base_economy.md, 4.10, 4.12): one record of the data, <c>economy</c>.
/// Production multipliers, ore and its sources, selling and repairs.
/// </summary>
public sealed record EconomyDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The slowest production under low power, as a fraction: 0.5 for 50 percent (RA2 <c>MinLowPowerProductionSpeed</c>).</summary>
    public required Fixed LowPowerMinRate { get; init; }

    /// <summary>The fastest production under low power, as a fraction: 0.8 (RA2 <c>MaxLowPowerProductionSpeed</c>).</summary>
    public required Fixed LowPowerMaxRate { get; init; }

    /// <summary>What every factory of a category beyond the first multiplies the build time by: 0.8 (RA2 <c>MultipleFactory</c>).</summary>
    public required Fixed MultipleFactory { get; init; }

    /// <summary>Credits one stage of ore is worth: a cell of stage s holds (s + 1) × this.</summary>
    public required int OreValue { get; init; }

    /// <summary>Credits one stage of gems is worth.</summary>
    public required int GemValue { get; init; }

    /// <summary>Ticks between two rounds of ore growth and spread.</summary>
    public required int OreGrowthInterval { get; init; }

    /// <summary>The chance of a cell of ore to grow a stage in a round, as a fraction.</summary>
    public required Fixed OreGrowthChance { get; init; }

    /// <summary>The stage from which a cell of ore may seed a neighbour.</summary>
    public required int OreSpreadStage { get; init; }

    /// <summary>The chance of such a cell to seed a neighbour in a round, as a fraction.</summary>
    public required Fixed OreSpreadChance { get; init; }

    /// <summary>Ticks between two eruptions of an ore or gem source (M3 step 16).</summary>
    public required int SourceInterval { get; init; }

    /// <summary>How far from its cell a source throws ore, in cells: within the circle dx² + dy² ≤ radius².</summary>
    public required int SourceRadius { get; init; }

    /// <summary>The share of the cost a sold building gives back at full hit points, as a fraction (M3 step 11).</summary>
    public required Fixed SellRefund { get; init; }

    /// <summary>What a repair from nothing to full hit points costs, as a share of the cost of the building.</summary>
    public required Fixed RepairCost { get; init; }

    /// <summary>Hit points a repair gives at a time.</summary>
    public required int RepairRate { get; init; }

    /// <summary>The ticks between two steps of a repair (RA2 <c>RepairRate</c>, <c>URepairRate</c>); 1 for every tick.</summary>
    public int RepairDelay { get; init; } = 1;
}
