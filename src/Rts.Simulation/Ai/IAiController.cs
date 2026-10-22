using Rts.Simulation.World;

namespace Rts.Simulation.Ai;

/// <summary>
/// A bot as the simulation sees it (docs/implementation/M5_first_bot.md, 3, 4.1–4.2): it plays one player and thinks once a tick, after
/// the systems of the tick and the removal of the dead, giving commands for the next tick through <see cref="AiTurn"/>. The bots
/// themselves live in Rts.AI, which depends on the simulation and not the other way round. A bot must be as deterministic as the
/// simulation: it runs on every machine of a match and its commands are what the replay keeps.
/// </summary>
public interface IAiController
{
    /// <summary>The player the bot plays, 1–8.</summary>
    PlayerId Player { get; }

    /// <summary>One turn of the bot, after tick <see cref="AiTurn.Tick"/>.</summary>
    void Think(AiTurn turn);
}
