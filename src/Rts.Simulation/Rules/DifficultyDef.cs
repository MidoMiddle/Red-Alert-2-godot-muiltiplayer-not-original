using System.Collections.Immutable;

namespace Rts.Simulation.Rules;

/// <summary>How a number of a level of the bots is written in the data: a plain count, a percentage, or a duration in seconds.</summary>
public enum DifficultyValue
{
    /// <summary>A whole number: ticks of a period, cells, units, credits.</summary>
    Count,

    /// <summary><c>125%</c>, kept as the whole percent, 125.</summary>
    Percent,

    /// <summary><c>3s</c>, kept in ticks.</summary>
    Duration,
}

/// <summary>
/// A level of the bots (docs/implementation/M5_first_bot.md, 4.7, step 14; the whole table of levels — M8, 4.2): how often every module
/// thinks, the economy, the base, the army, scouting, squads and defense, by <see cref="Keys"/>; the build order and the army it uses.
/// Data like the rest: checked as it loads — every key there — and hashed, so that every machine of a match runs the same bot.
/// </summary>
public sealed record DifficultyDef : IDefinition
{
    /// <summary>The numbers of a level, every one required, in the order of the data file.</summary>
    public static readonly ImmutableArray<(string Key, DifficultyValue Kind)> Keys =
    [
        ("strategy_period", DifficultyValue.Count), ("economy_period", DifficultyValue.Count), ("build_period", DifficultyValue.Count),
        ("production_period", DifficultyValue.Count), ("scouting_period", DifficultyValue.Count), ("tactics_period", DifficultyValue.Count),
        ("defense_period", DifficultyValue.Count),
        ("max_refineries", DifficultyValue.Count), ("harvesters_per_refinery", DifficultyValue.Count), ("power_reserve", DifficultyValue.Percent),
        ("danger_cells", DifficultyValue.Count), ("second_factory_credits", DifficultyValue.Count), ("counter_shift", DifficultyValue.Percent),
        ("infantry_queued", DifficultyValue.Count), ("vehicles_queued", DifficultyValue.Count),
        ("scouting_interval", DifficultyValue.Duration), ("scout_block_cells", DifficultyValue.Count), ("scout_danger_cells", DifficultyValue.Count),
        ("min_attack_army", DifficultyValue.Count), ("attack_growth_per_wave", DifficultyValue.Percent), ("attack_threshold", DifficultyValue.Percent),
        ("retreat_at_strength", DifficultyValue.Percent), ("gather_cells", DifficultyValue.Count), ("squad_spread_cells", DifficultyValue.Count),
        ("engage_cells", DifficultyValue.Count), ("wounded", DifficultyValue.Percent), ("sweep_places", DifficultyValue.Count),
        ("all_in_after", DifficultyValue.Duration), ("all_in_units", DifficultyValue.Count), ("sell_every", DifficultyValue.Duration),
        ("perception_delay", DifficultyValue.Duration), ("threat_clear", DifficultyValue.Duration), ("base_cells", DifficultyValue.Count),
        ("max_defenses", DifficultyValue.Count),
    ];

    public required string Id { get; init; }

    /// <summary>The <c>build_order</c> of the data the bot follows.</summary>
    public required string BuildOrder { get; init; }

    /// <summary>The <c>army</c> of the data the bot builds.</summary>
    public required string Army { get; init; }

    /// <summary>The numbers by <see cref="Keys"/>: counts as they are, percentages as whole percent, durations in ticks.</summary>
    public required ImmutableSortedDictionary<string, int> Values { get; init; }
}
