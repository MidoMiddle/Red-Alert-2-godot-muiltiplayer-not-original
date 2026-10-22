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

    /// <summary>The refineries the bot builds at most (4.7).</summary>
    public int MaxRefineries { get; init; } = 2;

    /// <summary>The harvesters the bot keeps for each refinery (4.6).</summary>
    public int HarvestersPerRefinery { get; init; } = 2;

    /// <summary>A reactor is wanted when what is left of the power output falls below this share of it, in percent (4.6).</summary>
    public int PowerReservePercent { get; init; } = 20;

    /// <summary>A harvester that works this near a known enemy, in cells, is sent to a field farther away (4.6).</summary>
    public int DangerCells { get; init; } = 12;

    /// <summary>The build order the bot follows, a <c>build_order</c> of the data for its faction (4.7).</summary>
    public string BuildOrder { get; init; } = "standard";

    /// <summary>After the build order, a second war factory once the credits left reach this (4.6).</summary>
    public int SecondFactoryCredits { get; init; } = 4000;

    /// <summary>The army the bot builds, an <c>army</c> of the data for its faction (4.7, step 6).</summary>
    public string Army { get; init; } = "standard_army";

    /// <summary>
    /// How many percentage points of the army move to the counters when the enemy seen is mostly infantry or mostly vehicles — the
    /// coarse counter of «Medium» (4.6, M8's <c>counterComposition: coarse</c>).
    /// </summary>
    public int CounterShiftPercent { get; init; } = 20;

    /// <summary>How often the scout goes out once the enemy has been found: 3 minutes, as «Medium» of M8.</summary>
    public int ScoutingInterval { get; init; } = 2700;

    /// <summary>The side of the blocks of the map the scout looks into, in cells.</summary>
    public int ScoutBlockCells { get; init; } = 16;

    /// <summary>An armed enemy this near the scout, in cells, makes it run home.</summary>
    public int ScoutDangerCells { get; init; } = 6;

    /// <summary>The units the first squad needs before it may set out (4.7, M8's <c>minAttackArmy</c>).</summary>
    public int MinAttackArmy { get; init; } = 8;

    /// <summary>How much bigger every next wave is, in percent: 8, 10, 12, 15… units (4.7, <c>attackGrowthPerWave</c>).</summary>
    public int AttackGrowthPercent { get; init; } = 125;

    /// <summary>The strength a squad needs to set out, in percent of the enemy's the bot knows of (4.7, <c>attackThreshold</c>).</summary>
    public int AttackThresholdPercent { get; init; } = 100;

    /// <summary>A squad turns back below this share of its strength when it set out, in percent (4.7, <c>retreatAtStrength</c>).</summary>
    public int RetreatAtStrengthPercent { get; init; } = 30;

    /// <summary>The gathering point stands this many cells from the start toward the enemy.</summary>
    public int GatherCells { get; init; } = 6;

    /// <summary>A unit this far from the middle of its moving squad, in cells, makes the squad wait for it.</summary>
    public int SquadSpreadCells { get; init; } = 6;

    /// <summary>An armed enemy this near the middle of a squad, in cells, makes it fight.</summary>
    public int EngageCells { get; init; } = 8;

    /// <summary>A unit of a fighting squad with less than this share of its hit points, in percent, falls back behind the squad.</summary>
    public int WoundedPercent { get; init; } = 35;

    /// <summary>How long enemies stand at the base before the bot takes notice, in ticks (4.7, M8's <c>perceptionDelay</c>).</summary>
    public int PerceptionDelay { get; init; } = 15;

    /// <summary>The base is safe again once no enemy has been at it this long, in ticks (4.6: 10 s).</summary>
    public int ThreatClearTicks { get; init; } = 150;

    /// <summary>An enemy this near a building of the bot, in cells, is at its base.</summary>
    public int BaseCells { get; init; } = 8;

    /// <summary>The defenses the bot builds at most, those of the build order and those toward the attacks together.</summary>
    public int MaxDefenses { get; init; } = 3;

    /// <summary>The units the barracks keep in their queue (4.6).</summary>
    public int InfantryQueued { get; init; } = 2;

    /// <summary>
    /// The army units the war factory keeps in its queue: one, so that a harvester the economy orders waits for one tank at most — a
    /// queue runs first come, first served.
    /// </summary>
    public int VehiclesQueued { get; init; } = 1;
}
