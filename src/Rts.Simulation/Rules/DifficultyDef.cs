using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Matches;

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

    /// <summary>A word of <see cref="DifficultyDef.Choices"/>, <c>focus</c>, kept as its place in the list.</summary>
    Choice,
}

/// <summary>
/// A level of the bots (docs/implementation/M5_first_bot.md, 4.7, step 14; docs/implementation/M8_ai_difficulty.md, 4.2, step 1): how often
/// every module thinks, the economy, the base, the army, scouting, squads and defense, and the imperfection of M8, by <see cref="Keys"/>.
/// The build order and the army are the personality's (<see cref="PersonalityDef"/>); the level says only which quality of build order
/// it takes. Data like the rest: checked as it loads — every key there — and hashed, so that every machine of a match runs the same bot.
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
        ("danger_cells", DifficultyValue.Count), ("second_factory_credits", DifficultyValue.Count),
        ("infantry_queued", DifficultyValue.Count), ("vehicles_queued", DifficultyValue.Count),
        ("scouting_interval", DifficultyValue.Duration), ("scout_block_cells", DifficultyValue.Count), ("scout_danger_cells", DifficultyValue.Count),
        ("min_attack_army", DifficultyValue.Count), ("attack_growth_per_wave", DifficultyValue.Percent), ("attack_threshold", DifficultyValue.Percent),
        ("retreat_at_strength", DifficultyValue.Percent), ("gather_cells", DifficultyValue.Count), ("squad_spread_cells", DifficultyValue.Count),
        ("engage_cells", DifficultyValue.Count), ("wounded", DifficultyValue.Percent), ("sweep_places", DifficultyValue.Count),
        ("all_in_after", DifficultyValue.Duration), ("all_in_units", DifficultyValue.Count), ("sell_every", DifficultyValue.Duration),
        ("perception_delay", DifficultyValue.Duration), ("threat_clear", DifficultyValue.Duration), ("base_cells", DifficultyValue.Count),
        ("max_defenses", DifficultyValue.Count),
        // M8, 4.2: what makes the levels differ. The bot of M8 step 1 reads them; the steps that follow make it act on them.
        ("actions_per_second", DifficultyValue.Count), ("mistake_chance", DifficultyValue.Percent), ("micro", DifficultyValue.Choice),
        ("scouting", DifficultyValue.Choice), ("counter_pick", DifficultyValue.Percent), ("first_attack_delay", DifficultyValue.Duration),
        ("parallel_squads", DifficultyValue.Count), ("defend_harvesters", DifficultyValue.Choice), ("expansion", DifficultyValue.Choice),
        ("build_order_quality", DifficultyValue.Choice), ("rebuild_destroyed", DifficultyValue.Choice),
        // M8, 4.5 (step 2): the bonuses of the player, not of the bot — the simulation gives them at the start of a match.
        ("income_bonus", DifficultyValue.Percent), ("build_speed_bonus", DifficultyValue.Percent), ("reveal_map", DifficultyValue.Choice),
    ];

    /// <summary>The words of every <see cref="DifficultyValue.Choice"/> key, from the weakest bot to the strongest.</summary>
    public static readonly ImmutableSortedDictionary<string, ImmutableArray<string>> Choices = ImmutableSortedDictionary.CreateRange(
        StringComparer.Ordinal,
        [
            Choice("micro", "none", "focus", "retreat_wounded", "keep_range"),
            Choice("scouting", "none", "periodic", "constant", "constant_expansions"),
            Choice("defend_harvesters", "no", "yes"),
            Choice("expansion", "none", "late", "early"),
            Choice("build_order_quality", "simple", "standard", "optimized"),
            Choice("rebuild_destroyed", "partial", "yes", "priority"),
            Choice("reveal_map", "no", "yes"),
        ]);

    /// <summary>The id of a level in the data (M8 step 1): <c>ai_easy</c>, <c>ai_medium</c>, <c>ai_hard</c>, <c>ai_brutal</c>.</summary>
    public static string IdOf(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Easy => "ai_easy",
        AiDifficulty.Medium => "ai_medium",
        AiDifficulty.Hard => "ai_hard",
        AiDifficulty.Brutal => "ai_brutal",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };

    public required string Id { get; init; }

    /// <summary>
    /// The numbers by <see cref="Keys"/>: counts as they are, percentages as whole percent, durations in ticks, a choice as the place of
    /// its word in <see cref="Choices"/>.
    /// </summary>
    public required ImmutableSortedDictionary<string, int> Values { get; init; }

    private static KeyValuePair<string, ImmutableArray<string>> Choice(string key, params string[] words) => new(key, [.. words]);
}
