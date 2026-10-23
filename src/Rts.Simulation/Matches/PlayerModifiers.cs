using Rts.Simulation.Rules;

namespace Rts.Simulation.Matches;

/// <summary>
/// The bonuses of a player (docs/implementation/M8_ai_difficulty.md, 4.5, step 2): more credits from every unload, faster production, the
/// whole map in sight. The bonuses of «Brutal» — of the player in the simulation, not of its bot, and named in the menu: none is hidden.
/// They come from the level of a bot in the data (<see cref="Of"/>) as the match starts; a person has none. <c>default</c> is no bonus.
/// </summary>
/// <param name="IncomeBonusPercent">Credits above the ore unloaded, in percent: 25 — ×1.25.</param>
/// <param name="BuildSpeedBonusPercent">Production faster by this, in percent: 20 — ×1.2, at the same cost.</param>
/// <param name="RevealMap">
/// The player sees everything (<see cref="Visibility.Vision.Sees"/>): its orders and its units, not the fog of the grid — the screen of a
/// person keeps it, and an ally gains nothing.
/// </param>
public readonly record struct PlayerModifiers(int IncomeBonusPercent, int BuildSpeedBonusPercent, bool RevealMap)
{
    /// <summary>The bonuses of a level of the data: <c>income_bonus</c>, <c>build_speed_bonus</c>, <c>reveal_map</c>.</summary>
    public static PlayerModifiers Of(DifficultyDef level) =>
        new(level.Values["income_bonus"], level.Values["build_speed_bonus"], level.Values["reveal_map"] == 1);
}
