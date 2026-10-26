namespace Rts.AI;

/// <summary>
/// The modules of a bot (docs/implementation/M5_first_bot.md, 4.6), in the order they think on a tick they share: the order of the
/// table in 4.3.
/// </summary>
public enum AiModuleKind
{
    Strategist,
    Economy,
    BuildPlanner,
    Production,
    Scouting,
    Squads,
    Defense,

    /// <summary>The team of the bot (M8 step 11): the board of the allied bots and the person of the team.</summary>
    Team,

    /// <summary>The ships of the bot (M11): they sail apart from the squads.</summary>
    Fleet,
}
