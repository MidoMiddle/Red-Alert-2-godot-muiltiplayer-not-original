using System.Collections.Generic;
using Rts.AI.Squads;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI;

/// <summary>
/// What the modules of a bot agree on (docs/implementation/M5_first_bot.md, 4.6): the buildings the economy wants now, most important
/// first, as indices of the buildings of the rules (step 4); the building the planner waits for money for, which the army does not take
/// from it (step 6); and whether the strategist lets the army be built; the squads (step 8); the state of the strategist, the threat to the
/// base and where it came from (step 9).
/// </summary>
public sealed class AiPlan
{
    /// <summary>The strategist goes into the defense of the base at once, from what it did, which it comes back to later.</summary>
    internal void Defend()
    {
        if (Strategy != StrategyState.Defending)
        {
            BeforeDefense = Strategy;
        }

        (Strategy, AttackAllowed, ArmyFirst) = (StrategyState.Defending, false, true);
    }

    /// <summary>The base is safe again: back to what the strategist did before.</summary>
    internal void EndDefense()
    {
        if (Strategy == StrategyState.Defending)
        {
            Strategy = BeforeDefense;
        }

        (Threat, AttackAllowed, ArmyFirst) = (null, true, false);
    }

    private readonly List<int> _economy = new();

    /// <summary>The buildings the economy wants, most important first.</summary>
    public IReadOnlyList<int> Wants => _economy;

    /// <summary>The building the planner will order next and saves the money for; -1 when there is none.</summary>
    public int NextBuilding { get; internal set; } = -1;

    /// <summary>The unit that scouts (step 7); the squads of step 8 leave it alone. Null without one.</summary>
    public EntityId? Scout { get; internal set; }

    /// <summary>Whether the economy wants a harvester now: the army leaves the war factory to it.</summary>
    public bool HarvestersWanted { get; internal set; }

    /// <summary>Whether the army may be built now; the strategist of step 9 holds it back.</summary>
    public bool ArmyAllowed { get; internal set; } = true;

    /// <summary>Whether a ready squad may set out (step 8); the strategist holds it back while the base is defended (step 9).</summary>
    public bool AttackAllowed { get; internal set; } = true;

    /// <summary>What the strategist does (step 9).</summary>
    public StrategyState Strategy { get; internal set; }

    /// <summary>The state the strategist comes back to once the base is safe again.</summary>
    internal StrategyState BeforeDefense { get; set; }

    /// <summary>Armed enemies at the base the bot has noticed (step 9); null while there are none.</summary>
    public AiThreat? Threat { get; internal set; }

    /// <summary>Where the last threat the defense of the base was too weak for came from; null before one (step 9).</summary>
    public CellPos? AttackedFrom { get; internal set; }

    /// <summary>While the base is defended the army does not save the money of the next building for it (step 9).</summary>
    public bool ArmyFirst { get; internal set; }

    /// <summary>
    /// No ore left that the bot knows of, after it saw some (step 14): the base is done — the planner builds no refinery, radar or tech
    /// centre any more, the economy sells them for the army once the factories stand idle, the scout goes looking at once; a field found
    /// turns it back.
    /// </summary>
    public bool OreGone { get; internal set; }

    /// <summary>Where the squads gather (step 8); for the debug overlay (step 13), no module decides by it. Null without a start.</summary>
    public CellPos? GatherPoint { get; internal set; }

    /// <summary>Where the scout goes now (step 7), for the debug overlay; null while it waits at home.</summary>
    public CellPos? ScoutErrand { get; internal set; }

    /// <summary>
    /// A squad half ready by its own count is to wait only for not knowing the enemy (M8 step 10): the scout goes to look at the enemy base
    /// at once, whatever its interval.
    /// </summary>
    public bool LookAtTheEnemy { get; internal set; }

    /// <summary>The building the planner placed last and where (step 5), for the debug overlay; null before the first.</summary>
    public (int DefIndex, CellPos Cell)? LastPlacement { get; internal set; }

    /// <summary>The squads of the bot (step 8), in the order they were formed; the squad manager keeps them.</summary>
    public IReadOnlyList<Squad> Squads { get; internal set; } = [];

    // M8 step 11, the team (Team/TeamCoordinator): what the squads tell the team, and what the team asks of the squads.

    /// <summary>The units of the squad gathering at home, as the squads last counted them.</summary>
    public int GatheringUnits { get; internal set; }

    /// <summary>Its strength.</summary>
    public int GatheringStrength { get; internal set; }

    /// <summary>The units it needs to set out by itself.</summary>
    public int NeededUnits { get; internal set; }

    /// <summary>Where the attack of the team goes that the squad at home joins; null for none.</summary>
    public CellPos? JoinAttack { get; internal set; }

    /// <summary>Where an ally is attacked and the bot helps; null for nowhere.</summary>
    public CellPos? HelpAt { get; internal set; }

    /// <summary>The economy's needs of this turn, in place of those of its last one.</summary>
    internal void SetEconomyWants(IEnumerable<int> wants)
    {
        _economy.Clear();
        _economy.AddRange(wants);
    }
}
