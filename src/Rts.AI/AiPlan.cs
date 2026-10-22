using System.Collections.Generic;
using Rts.Simulation.World;

namespace Rts.AI;

/// <summary>
/// What the modules of a bot agree on (docs/implementation/M5_first_bot.md, 4.6): the buildings the economy wants now, most important
/// first, as indices of the buildings of the rules (step 4); the building the planner waits for money for, which the army does not take
/// from it (step 6); and whether the strategist lets the army be built (step 9 decides; until then always).
/// </summary>
public sealed class AiPlan
{
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

    /// <summary>The economy's needs of this turn, in place of those of its last one.</summary>
    internal void SetEconomyWants(IEnumerable<int> wants)
    {
        _economy.Clear();
        _economy.AddRange(wants);
    }
}
