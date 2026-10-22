using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// What every player may build (docs/implementation/M3_base_economy.md, 4.7): a unit or building of its faction whose prerequisites are
/// all among its ready buildings. Kept as a flag per definition, by its index in the rules (one set for units, one for buildings).
/// Worked out only when the buildings changed (<see cref="GameWorld.TechDirty"/>), late in the tick (4.15), so a building finished in
/// a tick opens its technologies for the next one. Whether a factory is there to build it is the business of production (step 8).
/// The construction yard of the faction is never built: it comes from the MCV, as in RA2.
/// </summary>
public static class TechTree
{
    public static void Update(GameWorld world)
    {
        if (!world.TechDirty)
        {
            return;
        }

        world.TechDirty = false;
        var rules = world.Rules.Database;
        var units = new bool[rules.Units.Length];
        var buildings = new bool[rules.Buildings.Length];
        var owned = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var player in world.Players)
        {
            owned.Clear();
            foreach (var entity in world.Entities)
            {
                if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && building.Owner == player.Id)
                {
                    owned.Add(def.Id);
                }
            }

            var faction = player.Faction.Id;
            for (var i = 0; i < units.Length; i++)
            {
                units[i] = rules.Units[i].Faction == faction && AllOwned(rules.Units[i].Prerequisites, owned);
            }

            for (var i = 0; i < buildings.Length; i++)
            {
                buildings[i] = rules.Buildings[i].Faction == faction && rules.Buildings[i].Id != player.Faction.ConstructionYard
                    && AllOwned(rules.Buildings[i].Prerequisites, owned);
            }

            player.SetAvailable(units, buildings);
        }
    }

    private static bool AllOwned(ImmutableArray<string> prerequisites, SortedSet<string> owned)
    {
        foreach (var prerequisite in prerequisites)
        {
            if (!owned.Contains(prerequisite))
            {
                return false;
            }
        }

        return true;
    }
}
