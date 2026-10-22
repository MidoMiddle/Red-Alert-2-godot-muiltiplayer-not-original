using Rts.Simulation.Geometry;

namespace Rts.AI;

/// <summary>What the strategist of a bot does (docs/implementation/M5_first_bot.md, 4.6, step 9).</summary>
public enum StrategyState
{
    /// <summary>Builds its base and army; a squad strong enough sets out.</summary>
    Developing,

    /// <summary>A squad is out attacking.</summary>
    Attacking,

    /// <summary>The base is attacked by more than its defense holds: no attack, the army first, squads called back.</summary>
    Defending,
}

/// <summary>
/// Armed enemies at the base of a bot: the one nearest its start, which the squads go for; the middle of them all, the side they came
/// from; how strong they are (Σ cost × the share of hit points left).
/// </summary>
public sealed record AiThreat(CellPos Cell, CellPos Middle, int Strength);
