namespace Rts.AI.Profiles;

// The words of the choices of a level (docs/implementation/M8_ai_difficulty.md, 4.2), in the order of DifficultyDef.Choices: the value of
// the data is the place of the word, so the order here is the order there — a test holds them together.

/// <summary>What the squads do in a fight (4.4).</summary>
public enum MicroLevel
{
    /// <summary>Attack-move to the target, the fight as it comes.</summary>
    None,

    /// <summary>Fire on the weakest in reach.</summary>
    Focus,

    /// <summary>And the wounded fall back.</summary>
    RetreatWounded,

    /// <summary>And the long-ranged keep their distance; a squad that loses the trade falls back under its defenses.</summary>
    KeepRange,
}

/// <summary>How the bot scouts (4.6).</summary>
public enum ScoutingLevel
{
    None,

    /// <summary>An outing every few minutes.</summary>
    Periodic,

    /// <summary>A scout always out.</summary>
    Constant,

    /// <summary>A scout always out, and the fields where the enemy may expand looked at.</summary>
    ConstantExpansions,
}

/// <summary>Whether the bot builds a refinery by the far fields.</summary>
public enum ExpansionLevel
{
    None,
    Late,
    Early,
}

/// <summary>Which build order of its personality the bot takes (4.7).</summary>
public enum BuildOrderQuality
{
    Simple,
    Standard,
    Optimized,
}

/// <summary>How the bot builds again what it lost.</summary>
public enum RebuildLevel
{
    /// <summary>Some of it.</summary>
    Partial,

    /// <summary>All of it, in the order of the build order.</summary>
    Yes,

    /// <summary>All of it, before the rest.</summary>
    Priority,
}

/// <summary>What the bot must know of the enemy before it attacks one it has not seen whole (M8 step 10a, <c>attack_knowledge</c>).</summary>
public enum AttackKnowledge
{
    /// <summary>A look at its army, or into its base with next to nothing there: a bot that looks seldom knows a passive enemy so.</summary>
    Base,

    /// <summary>A look at its army only, or an attack of it beaten at home: a look into a base does not show the army beside it.</summary>
    Army,
}
