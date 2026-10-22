using System.Collections.Generic;

namespace Rts.AI;

/// <summary>
/// What the modules of a bot agree on (docs/implementation/M5_first_bot.md, 4.6): the buildings wanted now, most important first, as
/// indices of the buildings of the rules. The economy writes its needs on its turn (step 4); the build planner builds the first one it
/// can. The build order (step 5) and the priorities of the strategist (step 9) come in here too.
/// </summary>
public sealed class AiPlan
{
    private readonly List<int> _economy = new();

    /// <summary>The buildings the economy wants, most important first.</summary>
    public IReadOnlyList<int> Wants => _economy;

    /// <summary>The economy's needs of this turn, in place of those of its last one.</summary>
    internal void SetEconomyWants(IEnumerable<int> wants)
    {
        _economy.Clear();
        _economy.AddRange(wants);
    }
}
