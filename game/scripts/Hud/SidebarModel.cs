using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>What a square of the sidebar shows about its item (docs/implementation/M3_base_economy.md, 4.17).</summary>
public enum CameoState
{
    /// <summary>Can be ordered now.</summary>
    Available,

    /// <summary>Prerequisites are missing; <see cref="Cameo.Missing"/> names them.</summary>
    Unavailable,

    /// <summary>Its queue is full: another building is in work, or fifteen units wait.</summary>
    Busy,

    /// <summary>In work or waiting in its queue; <see cref="Cameo.Progress"/> is the clock.</summary>
    Building,

    /// <summary>The player paused it.</summary>
    OnHold,

    /// <summary>It stands still until the credits are there.</summary>
    WaitingForFunds,

    /// <summary>A building is done and waits to be placed.</summary>
    Ready,
}

/// <summary>
/// One square of a tab: the item, its state, the clock from 0 to 1 and how many of it are in the queue. Unavailable items name the
/// missing prerequisites for the tooltip, joined by commas. A plain value: equal squares draw the same.
/// </summary>
public sealed record Cameo(
    ProductionKind Kind,
    int DefIndex,
    string Name,
    int Cost,
    CameoState State,
    float Progress,
    int Count,
    string Missing)
{
    /// <summary>A building whose own ability, or a unit whose weapon, comes later: the tooltip says so.</summary>
    public bool AbilityLater { get; init; }

    /// <summary>A unit that cannot be built until its mechanic comes (ships, aircraft): the tooltip says so instead of what is missing.</summary>
    public bool AvailableLater { get; init; }
}

/// <summary>What a click on a square asks for.</summary>
public enum CameoActionKind
{
    None,
    Produce,
    Pause,
    Cancel,

    /// <summary>The ready building goes into the placement mode.</summary>
    Place,
}

public readonly record struct CameoAction(CameoActionKind Kind, ProductionKind Item, int DefIndex, int Count = 1)
{
    public static readonly CameoAction None = new(CameoActionKind.None, ProductionKind.Building, -1, 0);
}

/// <summary>
/// The sidebar read from the simulation, without Godot (docs/implementation/M3_base_economy.md, 4.17): which tabs are open, the squares
/// of a tab and what a click on a square does. It reads the world and changes nothing; the host turns an action into a command.
/// </summary>
public static class SidebarModel
{
    /// <summary>The production category behind a tab: the tabs and the queues come in a different order.</summary>
    public static ProductionCategory CategoryOf(BuildGroup group) => group switch
    {
        BuildGroup.Buildings => ProductionCategory.Building,
        BuildGroup.Defense => ProductionCategory.Defense,
        BuildGroup.Infantry => ProductionCategory.Infantry,
        _ => ProductionCategory.Vehicle,
    };

    /// <summary>
    /// A tab is open while the player has a ready factory of its category, the way production needs one: the Vehicles tab with a war
    /// factory, a shipyard or an air command, as the ships and the aircraft stand in it (RA2).
    /// </summary>
    public static bool TabOpen(GameWorld world, PlayerId player, BuildGroup group) =>
        ProductionRules.HasFactory(world, player, CategoryOf(group))
        || (group == BuildGroup.Vehicles
            && (ProductionRules.HasFactory(world, player, ProductionCategory.Naval) || ProductionRules.HasFactory(world, player, ProductionCategory.Aircraft)));

    /// <summary>
    /// The squares of a tab, in the order of the data: every item of the player's faction in the tab's category. Units are only those
    /// the simulation knows how to drive. The construction yard is left out: it comes from the MCV.
    /// </summary>
    public static IReadOnlyList<Cameo> Cameos(GameWorld world, PlayerId player, BuildGroup group)
    {
        var rules = world.Rules.Database;
        var state = world.Player(player);
        var category = CategoryOf(group);
        var queue = state.Queue(category);
        var owned = ReadyBuildings(world, player);
        var cameos = new List<Cameo>();
        if (category is ProductionCategory.Building or ProductionCategory.Defense)
        {
            for (var i = 0; i < rules.Buildings.Length; i++)
            {
                var def = rules.Buildings[i];
                if (def.Faction == state.Faction.Id && def.Category == category && def.Id != state.Faction.ConstructionYard)
                {
                    cameos.Add(CameoOf(rules, queue, ProductionKind.Building, i, def.Name, def.Cost, state.CanBuildBuilding(i), def.Prerequisites, owned));
                }
            }
        }
        else
        {
            // The Vehicles tab shows the ships and the aircraft too, each with the queue of its own category (stages 2a, 2b).
            for (var i = 0; i < rules.Units.Length; i++)
            {
                var def = rules.Units[i];
                var shown = category == ProductionCategory.Infantry
                    ? def.Category == UnitCategory.Infantry
                    : def.Category is UnitCategory.Vehicle or UnitCategory.Naval or UnitCategory.Aircraft;
                if (def.Faction == state.Faction.Id && shown && !def.Spawned && UnitTypes.TryGetById(def.Id, out _))
                {
                    var own = state.Queue(ProductionRules.CategoryOf(rules, ProductionKind.Unit, i));
                    // The price of the player: a vehicle costs less with a discount (stage 2c).
                    var cost = ProductionRules.CostOf(rules, state, ProductionKind.Unit, i);
                    cameos.Add(CameoOf(rules, own, ProductionKind.Unit, i, def.Name, cost, state.CanBuildUnit(i), def.Prerequisites, owned));
                }
            }
        }

        return cameos;
    }

    /// <summary>
    /// A left click: a ready building goes to the placement mode, a paused item goes on (the order command resumes it), anything that
    /// can be ordered is ordered, one or, for units with Shift, five. Unavailable and busy squares do nothing.
    /// </summary>
    public static CameoAction LeftClick(Cameo cameo, bool shift) => cameo.State switch
    {
        CameoState.Ready => new CameoAction(CameoActionKind.Place, cameo.Kind, cameo.DefIndex),
        CameoState.OnHold => new CameoAction(CameoActionKind.Produce, cameo.Kind, cameo.DefIndex),
        CameoState.Unavailable or CameoState.Busy => CameoAction.None,
        // Units queue behind the one in work; a building takes its queue alone, so a second order of it does nothing.
        CameoState.Building or CameoState.WaitingForFunds when cameo.Kind == ProductionKind.Building => CameoAction.None,
        _ => new CameoAction(CameoActionKind.Produce, cameo.Kind, cameo.DefIndex, shift && cameo.Kind == ProductionKind.Unit ? ProductionRules.MaxOrder : 1),
    };

    /// <summary>
    /// A right click, as in RA2: an item in work is put on hold; one on hold or ready is cancelled, the last of its kind, with the
    /// credits back.
    /// </summary>
    public static CameoAction RightClick(Cameo cameo) => cameo.State switch
    {
        CameoState.Building or CameoState.WaitingForFunds => new CameoAction(CameoActionKind.Pause, cameo.Kind, cameo.DefIndex),
        CameoState.OnHold or CameoState.Ready => new CameoAction(CameoActionKind.Cancel, cameo.Kind, cameo.DefIndex),
        _ => CameoAction.None,
    };

    private static Cameo CameoOf(
        RulesDatabase rules,
        IReadOnlyList<ProductionItem> queue,
        ProductionKind kind,
        int index,
        string name,
        int cost,
        bool available,
        ImmutableArray<string> prerequisites,
        HashSet<string> owned)
    {
        ProductionItem? first = null;
        var count = 0;
        foreach (var item in queue)
        {
            if (item.Kind == kind && item.DefIndex == index)
            {
                first ??= item;
                count++;
            }
        }

        var availableLater = kind == ProductionKind.Unit && rules.Units[index].AvailableLater;
        var missing = available || availableLater ? "" : Missing(rules, prerequisites, owned);
        var later = kind == ProductionKind.Building ? rules.Buildings[index].AbilityLater : rules.Units[index].AbilityLater;
        if (first is not null)
        {
            var progress = Math.Clamp(first.Progress / (float)ProductionRules.TotalProgress(rules, kind, index), 0f, 1f);
            var state = first.Ready ? CameoState.Ready
                : first.Paused ? CameoState.OnHold
                : first.WaitingForFunds ? CameoState.WaitingForFunds
                : CameoState.Building;
            return new Cameo(kind, index, name, cost, state, first.Ready ? 1f : progress, count, missing) { AbilityLater = later, AvailableLater = availableLater };
        }

        var full = kind == ProductionKind.Building ? queue.Count > 0 : queue.Count >= ProductionRules.MaxUnitQueue;
        var free = !available ? CameoState.Unavailable : full ? CameoState.Busy : CameoState.Available;
        return new Cameo(kind, index, name, cost, free, 0f, 0, missing) { AbilityLater = later, AvailableLater = availableLater };
    }

    private static string Missing(RulesDatabase rules, ImmutableArray<string> prerequisites, HashSet<string> owned)
    {
        var names = new List<string>();
        foreach (var id in prerequisites)
        {
            if (!owned.Contains(id))
            {
                var index = rules.IndexOfBuilding(id);
                names.Add(index >= 0 ? rules.Buildings[index].Name : id);
            }
        }

        return string.Join(", ", names);
    }

    private static HashSet<string> ReadyBuildings(GameWorld world, PlayerId player)
    {
        var owned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && building.Owner == player)
            {
                owned.Add(def.Id);
            }
        }

        return owned;
    }
}
