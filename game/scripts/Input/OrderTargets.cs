using System;
using System.Collections.Generic;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>
/// Which of the selected units an order reaches: the living units the player owns, in the order they were selected, at most
/// <paramref name="limit"/> of them (a command holds at most <see cref="UnitsCommand.MaxUnits"/>). Pure: the host says what is
/// alive and whose each unit is.
/// </summary>
public static class OrderTargets
{
    public static List<EntityId> Pick(IEnumerable<EntityId> selected, Func<EntityId, bool> isOwnLiving, int limit = UnitsCommand.MaxUnits)
    {
        var picked = new List<EntityId>();
        foreach (var unit in selected)
        {
            if (picked.Count >= limit)
            {
                break;
            }

            if (isOwnLiving(unit))
            {
                picked.Add(unit);
            }
        }

        return picked;
    }
}
