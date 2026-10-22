namespace Rts.AI.Squads;

/// <summary>What a squad does (docs/implementation/M5_first_bot.md, 4.6, step 8).</summary>
public enum SquadState
{
    /// <summary>New units come together at the gathering point near the base.</summary>
    Gathering,

    /// <summary>Strong enough to attack; waits for the strategist to let it go.</summary>
    Ready,

    /// <summary>Goes to its target by attack-move, keeping together.</summary>
    Moving,

    /// <summary>Fights the armed enemies near it, its fire on one of them.</summary>
    Fighting,

    /// <summary>Goes back to the gathering point — beaten, or with nothing left to attack — and joins the squad gathering there.</summary>
    Retreating,
}
