namespace Rts.AI;

/// <summary>
/// The parameters of a level of difficulty (docs/implementation/M5_first_bot.md, 4.3, 4.7): how often each module thinks, in ticks.
/// Only «Medium» for now, in code; it moves to <c>game/data/ai/difficulty.yaml</c> in step 14.
/// </summary>
public sealed record AiSettings
{
    /// <summary>«Medium»: the strategist every 3 s and the squads every second, as in M8, 4.2.</summary>
    public static readonly AiSettings Medium = new();

    public int StrategyPeriod { get; init; } = 45;

    public int EconomyPeriod { get; init; } = 15;

    public int BuildPeriod { get; init; } = 15;

    public int ProductionPeriod { get; init; } = 15;

    public int ScoutingPeriod { get; init; } = 45;

    public int TacticsPeriod { get; init; } = 15;

    public int DefensePeriod { get; init; } = 5;
}
