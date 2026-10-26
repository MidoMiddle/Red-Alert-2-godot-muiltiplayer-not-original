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

    /// <summary>The engineers of the bot (M11 step 3): they take the tech buildings of the map.</summary>
    Engineers,

    /// <summary>The soldiers the bot puts into the buildings of the map by its base (M11 step 3).</summary>
    Garrisons,

    /// <summary>The units that fire only when ordered (M11 step 3): the bot sends them at their targets.</summary>
    Strikes,

    /// <summary>The spies of the bot (M11 step 3): into the enemy's buildings.</summary>
    Spies,

    /// <summary>The transports of the bot (M11 step 3): its squads over the water where no tank drives to the enemy.</summary>
    Ferry,

    /// <summary>The soldiers of the bot that deploy (M11 step 3): dig in, poison, the psychic wave.</summary>
    Deploys,

    /// <summary>The superweapons of the bot (M11 step 3).</summary>
    Superweapons,

    /// <summary>The repairs of the bot: its buildings, and its vehicles and ships at the workshops.</summary>
    Repairs,

    /// <summary>The crates the bot's units take (M11 a6).</summary>
    Crates,
}
