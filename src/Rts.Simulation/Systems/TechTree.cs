using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// What every player may build (docs/implementation/M3_base_economy.md, 4.7): a unit or building of its faction whose prerequisites are
/// all among its ready buildings. A unit or building of the other faction too, as in RA2, once the player holds a ready building of that
/// faction that makes it — barracks for its infantry, a war factory for its vehicles, its construction yard for its buildings — taken by
/// an engineer (the owner, 2026-10-26); not its specials: those of a country or of stolen technology. Kept as a flag per definition, by its index in the rules (one set for units, one for buildings).
/// Worked out only when the buildings changed (<see cref="GameWorld.TechDirty"/>), late in the tick (4.15), so a building finished in
/// a tick opens its technologies for the next one. Whether a factory is there to build it is the business of production (step 8).
/// The construction yard of the faction is never built: it comes from the MCV, as in RA2. A lost prerequisite refuses new orders,
/// but what was started goes on (M3, decision 4). When the last factory of a category is gone, though, its queue is cancelled with all
/// that was paid (M4 step 9, <see cref="CancelWithoutFactory"/>).
/// </summary>
public static class TechTree
{
    public static void Update(GameWorld world, List<Events.SimEvent>? events = null)
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
        var factories = new SortedSet<(string Faction, ProductionCategory Category)>();
        foreach (var player in world.Players)
        {
            owned.Clear();
            factories.Clear();
            var income = 0;
            foreach (var entity in world.Entities)
            {
                if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && building.Owner == player.Id)
                {
                    owned.Add(def.Id);
                    foreach (var category in def.Produces)
                    {
                        factories.Add((def.Faction, category));
                    }

                    // The prerequisite groups of RA2: a building taken from the other faction counts too.
                    foreach (var group in def.Provides)
                    {
                        owned.Add(group);
                    }

                    // The richer ore of the ore purifier: +25% for each (RA2 PurifierBonus), only with power (RA2; not so in Yuri's Revenge).
                    if (def.IncomeBonusPercent > 0 && Combat.Defenses.IsOnline(world, building))
                    {
                        income += def.IncomeBonusPercent;
                    }
                }
            }

            player.BuildingIncomeBonusPercent = income;

            var faction = player.Faction.Id;
            for (var i = 0; i < units.Length; i++)
            {
                // A unit available later (ships, aircraft) waits for its mechanic: never in the tree.
                // A unit of stolen technology (RA2) opens once a spy took it from that faction's battle lab.
                var unit = rules.Units[i];
                var own = unit.Faction == faction;
                units[i] = (own || Taken(unit.Faction, unit.ProducedIn, unit.Countries, unit.RequiresStolen.Length > 0, factories))
                    && !unit.AvailableLater && !unit.Spawned && AllOwned(unit.Prerequisites, owned)
                    && (unit.RequiresStolen.Length == 0 || player.StolenTech.Contains(unit.RequiresStolen))
                    && (!own || Allowed(player.Country, unit.Countries, unit.ForbiddenCountries));
            }

            for (var i = 0; i < buildings.Length; i++)
            {
                var building = rules.Buildings[i];
                var own = building.Faction == faction;
                buildings[i] = (own || Taken(building.Faction, building.Category, building.Countries, special: false, factories))
                    && !world.Rules.IsConstructionYard(building.Id)
                    && AllOwned(building.Prerequisites, owned)
                    && (!own || Allowed(player.Country, building.Countries, building.ForbiddenCountries))
                    && (world.Match is not { Options.Superweapons: false } || !SuperweaponSystem.IsStrike(building.Superweapon));
            }

            player.SetAvailable(units, buildings);
            CancelWithoutFactory(world, player, events);
        }
    }

    /// <summary>
    /// Whether a player of <paramref name="country"/> may have what only <paramref name="countries"/> have and
    /// <paramref name="forbidden"/> may not (loose ends, stage 5); a player of no country — a bot — has the whole faction.
    /// </summary>
    public static bool Allowed(string country, System.Collections.Immutable.ImmutableArray<string> countries, System.Collections.Immutable.ImmutableArray<string> forbidden) =>
        country.Length == 0 || ((countries.IsDefaultOrEmpty || countries.Contains(country)) && (forbidden.IsDefaultOrEmpty || !forbidden.Contains(country)));

    /// <summary>
    /// The queue of a category for which the player has no factory left — none standing, ready or growing — is cancelled, a ready
    /// building too; every item gives back what was paid for it (M4, 4.16, as in RA2). With another factory of the category it goes on.
    /// </summary>
    private static void CancelWithoutFactory(GameWorld world, PlayerState player, List<Events.SimEvent>? events)
    {
        for (var category = ProductionCategory.Infantry; category <= ProductionCategories.Last; category++)
        {
            var queue = player.QueueList(category);
            if (queue.Count == 0 || HasFactory(world, player.Id, category))
            {
                continue;
            }

            foreach (var item in queue)
            {
                player.Credits += item.Paid;
                events?.Add(new Events.ProductionCancelled(world.CurrentTick, player.Id, item.Kind, item.DefIndex, item.Paid));
            }

            queue.Clear();
        }
    }

    private static bool HasFactory(GameWorld world, PlayerId player, ProductionCategory category)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def: { } def } building && building.Owner == player && def.Produces.Contains(category))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an item of another faction is open for the player (RA2): it holds a ready building of <paramref name="faction"/> that makes
    /// the <paramref name="category"/> — taken by an engineer — and the item is none of the specials, of a country or of stolen technology.
    /// </summary>
    private static bool Taken(
        string faction, ProductionCategory category, ImmutableArray<string> countries, bool special, SortedSet<(string, ProductionCategory)> factories) =>
        faction.Length > 0 && !special && countries.IsDefaultOrEmpty && factories.Contains((faction, category));

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
