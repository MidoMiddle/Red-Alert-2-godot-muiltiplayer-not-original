using System;
using Rts.Simulation.World;

namespace Rts.AI;

/// <summary>
/// When a module of a bot thinks (docs/implementation/M5_first_bot.md, 4.3): not by the clock, which is not the same on every machine,
/// but once in its period, on the ticks where <c>(tick + player × 3 + offset) % period == 0</c>. The offsets spread the modules of a
/// bot, and the player spreads the bots, over different ticks.
/// </summary>
public static class AiSchedule
{
    /// <summary>The period of <paramref name="kind"/> in ticks.</summary>
    public static int Period(AiSettings settings, AiModuleKind kind) => kind switch
    {
        AiModuleKind.Strategist => settings.StrategyPeriod,
        AiModuleKind.Economy => settings.EconomyPeriod,
        AiModuleKind.BuildPlanner => settings.BuildPeriod,
        AiModuleKind.Production => settings.ProductionPeriod,
        AiModuleKind.Scouting => settings.ScoutingPeriod,
        AiModuleKind.Squads => settings.TacticsPeriod,
        AiModuleKind.Defense => settings.DefensePeriod,
        AiModuleKind.Team => settings.TacticsPeriod,
        AiModuleKind.Fleet => settings.TacticsPeriod,
        AiModuleKind.Engineers => settings.BuildPeriod,
        AiModuleKind.Garrisons => settings.BuildPeriod,
        AiModuleKind.Strikes => settings.TacticsPeriod,
        AiModuleKind.Spies => settings.BuildPeriod,
        AiModuleKind.Ferry => settings.TacticsPeriod,
        AiModuleKind.Deploys => settings.TacticsPeriod,
        AiModuleKind.Superweapons => settings.StrategyPeriod,
        AiModuleKind.Repairs => settings.BuildPeriod,
        AiModuleKind.Crates => settings.TacticsPeriod,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The offset of <paramref name="kind"/> in 4.3.</summary>
    public static int Offset(AiModuleKind kind) => kind switch
    {
        AiModuleKind.Strategist => 0,
        AiModuleKind.Economy => 1,
        AiModuleKind.BuildPlanner => 2,
        AiModuleKind.Production => 4,
        AiModuleKind.Scouting => 5,
        AiModuleKind.Squads => 0,
        AiModuleKind.Defense => 3,
        AiModuleKind.Team => 1,
        AiModuleKind.Fleet => 2,
        AiModuleKind.Engineers => 6,
        AiModuleKind.Garrisons => 7,
        AiModuleKind.Strikes => 1,
        AiModuleKind.Spies => 8,
        AiModuleKind.Ferry => 4,
        AiModuleKind.Deploys => 3,
        AiModuleKind.Superweapons => 5,
        AiModuleKind.Repairs => 9,
        AiModuleKind.Crates => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Whether <paramref name="kind"/> of the bot of <paramref name="player"/> thinks after <paramref name="tick"/>.</summary>
    public static bool IsDue(AiSettings settings, AiModuleKind kind, PlayerId player, int tick) =>
        (tick + player.Value * 3 + Offset(kind)) % Period(settings, kind) == 0;
}
