using Rts.AI.Profiles;
using Rts.Simulation.Rules;

namespace Rts.AI;

/// <summary>
/// The parameters of a level of difficulty (docs/implementation/M5_first_bot.md, 4.3, 4.7; docs/implementation/M8_ai_difficulty.md, 4.2):
/// how often each module thinks, in ticks, and the rest of a <c>difficulty</c> of the data — the profile of a level. The build order and
/// the army come from the personality (<see cref="AiProfile"/>), by <see cref="Quality"/> and the faction of the bot.
/// </summary>
public sealed record AiSettings
{
    /// <summary>
    /// «Medium» as the code knows it: the strategist every 3 s and the squads every second, as in M8, 4.2. The game takes its «Medium» from
    /// the data (<see cref="From"/>, <c>game/data/ai/difficulty.yaml</c>, step 14); these are the same numbers, for the tests of the modules.
    /// </summary>
    public static readonly AiSettings Medium = new();

    /// <summary>
    /// The settings of a level of the data (step 14, M8 step 1): every number by its key. The build order and the army stay those of the
    /// code until <see cref="AiProfile"/> gives the personality's.
    /// </summary>
    public static AiSettings From(DifficultyDef level)
    {
        var values = level.Values;
        return new AiSettings
        {
            StrategyPeriod = values["strategy_period"],
            EconomyPeriod = values["economy_period"],
            BuildPeriod = values["build_period"],
            ProductionPeriod = values["production_period"],
            ScoutingPeriod = values["scouting_period"],
            TacticsPeriod = values["tactics_period"],
            DefensePeriod = values["defense_period"],
            MaxRefineries = values["max_refineries"],
            HarvestersPerRefinery = values["harvesters_per_refinery"],
            PowerReservePercent = values["power_reserve"],
            DangerCells = values["danger_cells"],
            SecondFactoryCredits = values["second_factory_credits"],
            CounterShiftPercent = values["counter_shift"],
            ScoutingInterval = values["scouting_interval"],
            ScoutBlockCells = values["scout_block_cells"],
            ScoutDangerCells = values["scout_danger_cells"],
            MinAttackArmy = values["min_attack_army"],
            AttackGrowthPercent = values["attack_growth_per_wave"],
            AttackThresholdPercent = values["attack_threshold"],
            RetreatAtStrengthPercent = values["retreat_at_strength"],
            GatherCells = values["gather_cells"],
            SquadSpreadCells = values["squad_spread_cells"],
            EngageCells = values["engage_cells"],
            WoundedPercent = values["wounded"],
            PerceptionDelay = values["perception_delay"],
            ThreatClearTicks = values["threat_clear"],
            BaseCells = values["base_cells"],
            MaxDefenses = values["max_defenses"],
            AllInTicks = values["all_in_after"],
            AllInUnits = values["all_in_units"],
            SweepPlaces = values["sweep_places"],
            SellTicks = values["sell_every"],
            InfantryQueued = values["infantry_queued"],
            VehiclesQueued = values["vehicles_queued"],
            ActionsPerSecond = values["actions_per_second"],
            MistakePercent = values["mistake_chance"],
            Micro = (MicroLevel)values["micro"],
            Scouting = (ScoutingLevel)values["scouting"],
            CounterPickPercent = values["counter_pick"],
            FirstAttackDelay = values["first_attack_delay"],
            ParallelSquads = values["parallel_squads"],
            DefendHarvesters = values["defend_harvesters"] == 1,
            Expansion = (ExpansionLevel)values["expansion"],
            Quality = (BuildOrderQuality)values["build_order_quality"],
            Rebuild = (RebuildLevel)values["rebuild_destroyed"],
        };
    }

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
    public int MinAttackArmy { get; init; } = 6;

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

    /// <summary>
    /// How late the bot learns of an enemy in sight or an attack on its base, in ticks (M8, 4.3, <c>perceptionDelay</c>;
    /// <see cref="View.PerceptionBuffer"/>, step 3).
    /// </summary>
    public int PerceptionDelay { get; init; } = 15;

    /// <summary>The base is safe again once no enemy has been at it this long, in ticks (4.6: 10 s).</summary>
    public int ThreatClearTicks { get; init; } = 150;

    /// <summary>An enemy this near a building of the bot, in cells, is at its base.</summary>
    public int BaseCells { get; init; } = 8;

    /// <summary>The defenses the bot builds at most, those of the build order and those toward the attacks together.</summary>
    public int MaxDefenses { get; init; } = 3;

    /// <summary>A gathering squad that has not grown this long, in ticks, sets out with what it has (step 14): an army that stopped growing.</summary>
    public int AllInTicks { get; init; } = 1800;

    /// <summary>The fewest units a squad sets out with when it stopped growing.</summary>
    public int AllInUnits { get; init; } = 3;

    /// <summary>A wave that finds nobody where it went sweeps up to this many unseen blocks of the map for the enemy before it turns home (step 14).</summary>
    public int SweepPlaces { get; init; } = 6;

    /// <summary>With the ore gone and the factories idle this long, in ticks, a building the base can do without is sold — one so often (step 14).</summary>
    public int SellTicks { get; init; } = 450;

    /// <summary>The units the barracks keep in their queue (4.6).</summary>
    public int InfantryQueued { get; init; } = 2;

    /// <summary>
    /// The army units the war factory keeps in its queue: one, so that a harvester the economy orders waits for one tank at most — a
    /// queue runs first come, first served.
    /// </summary>
    public int VehiclesQueued { get; init; } = 1;

    // M8, 4.2: read from the data since M8 step 1; the bot acts on them from the step of each — the budget and the mistakes from step 3,
    // the micro from step 5, scouting from step 6, the counters from step 7.

    /// <summary>The commands the bot gives a second at most (M8, 4.3, <c>actionsPerSecond</c>).</summary>
    public int ActionsPerSecond { get; init; } = 3;

    /// <summary>The chance, in percent, that the bot takes one of the three best choices at random instead of the best (4.3).</summary>
    public int MistakePercent { get; init; } = 10;

    public MicroLevel Micro { get; init; } = MicroLevel.Focus;

    public ScoutingLevel Scouting { get; init; } = ScoutingLevel.Periodic;

    /// <summary>The share of the army, in percent, picked against what the enemy fields (4.6, <c>counterPick</c>).</summary>
    public int CounterPickPercent { get; init; } = 30;

    /// <summary>The earliest first attack, in ticks; 0 — the window of the personality decides (Hard, Brutal).</summary>
    public int FirstAttackDelay { get; init; } = 5400;

    /// <summary>The squads that act at once.</summary>
    public int ParallelSquads { get; init; } = 2;

    /// <summary>Whether the bot answers an attack on its harvesters.</summary>
    public bool DefendHarvesters { get; init; } = true;

    public ExpansionLevel Expansion { get; init; } = ExpansionLevel.Late;

    /// <summary>Which build order of its personality the bot takes (<c>buildOrderQuality</c>).</summary>
    public BuildOrderQuality Quality { get; init; } = BuildOrderQuality.Standard;

    public RebuildLevel Rebuild { get; init; } = RebuildLevel.Yes;
}
