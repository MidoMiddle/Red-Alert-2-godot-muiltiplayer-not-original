using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
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

    /// <summary>
    /// The player has as many as it may (the build limit: one ore purifier, one superweapon of a kind, one hero): the square stays,
    /// greyed, and the tooltip says so (the owner, 2026-10-29).
    /// </summary>
    public bool AtLimit { get; init; }

    /// <summary>
    /// The building whose superweapon the square is (the owner, 2026-10-30: on the Defense tab, first): charging while
    /// <see cref="CameoState.Building"/>, fired by a click when <see cref="CameoState.Ready"/>; null for an item to build.
    /// </summary>
    public EntityId? Superweapon { get; init; }

    /// <summary>The building of the superweapon by name, for the tooltip: Air Force Command, Airfield.</summary>
    public string From { get; init; } = "";

    /// <summary>The seconds the superweapon still charges, shown where an item shows its cost.</summary>
    public int SecondsLeft { get; init; }

    /// <summary>The superweapon is armed: the next click on the map aims it.</summary>
    public bool Armed { get; init; }

    /// <summary>What it is for, by which the square takes its colour until there are icons (the owner's wish, 2026-10-24).</summary>
    public CameoRole Role { get; init; }

    /// <summary>A short code of its name, big on the square until there are icons: GI, GBT, PT.</summary>
    public string Code { get; init; } = "";
}

/// <summary>What a unit or building of the sidebar is for: the colour of its square.</summary>
public enum CameoRole
{
    Support,
    Infantry,
    Vehicle,
    Naval,
    Aircraft,
    Power,
    Production,
    Defense,
    Superweapon,
    Wall,
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

    /// <summary>Whether a building of the tab is ready to place: the first of its queue, done (the tab blinks while not shown).</summary>
    public static bool HasReadyToPlace(GameWorld world, PlayerId player, BuildGroup group) =>
        group is BuildGroup.Buildings or BuildGroup.Defense
        && world.Player(player).Queue(CategoryOf(group)) is [{ Ready: true, Kind: ProductionKind.Building }, ..];

    /// <summary>
    /// A tab is open while the player has a ready factory of its category, the way production needs one: the Vehicles tab with a war
    /// factory, a shipyard or an air command, as the ships and the aircraft stand in it (RA2).
    /// </summary>
    public static bool TabOpen(GameWorld world, PlayerId player, BuildGroup group) =>
        ProductionRules.HasFactory(world, player, CategoryOf(group))
        || (group == BuildGroup.Defense && HasSuperweapon(world, player))
        || (group == BuildGroup.Vehicles
            && (ProductionRules.HasFactory(world, player, ProductionCategory.Naval) || ProductionRules.HasFactory(world, player, ProductionCategory.Aircraft)));

    /// <summary>
    /// The order of the squares of RA2 (the owner's list, 2026-10-24): every tab top to bottom, left to right, as the items open with
    /// the buildings — the Allies, then the Soviets. The items the list does not name (the countries' own, the service depot) stand by the tech of theirs, next to the nearest ones; an item not here comes last, in the order of the data.
    /// </summary>
    private static readonly string[] Ra2Order =
    [
        // Buildings (Q).
        "alliance_power_plant", "alliance_ore_refinery", "alliance_barracks", "alliance_war_factory", "alliance_air_force_command",
        "alliance_naval_shipyard", "alliance_service_depot", "alliance_battle_lab", "alliance_ore_purifier",
        "alliance_chronosphere", "alliance_weather_control_device",
        "reactor", "refinery", "barracks", "war_factory", "radar", "naval_shipyard", "service_depot", "tech_center", "nuclear_reactor",
        "cloning_vats", "iron_curtain", "nuclear_missile_silo",

        // Defenses (W).
        "alliance_wall", "alliance_pillbox", "alliance_patriot_missile_system", "alliance_prism_tower", "alliance_gap_generator",
        "alliance_spy_satellite_uplink", "alliance_grand_cannon",
        "wall", "sentry_bunker", "flak_cannon", "tesla_coil", "psychic_sensor",

        // Infantry (E).
        "alliance_attack_dog", "alliance_rifleman", "alliance_engineer", "alliance_rocketeer", "alliance_spy", "alliance_sniper",
        "alliance_tanya", "alliance_chrono_legionnaire", "alliance_chrono_commando", "alliance_psi_commando",
        "attack_dog", "conscript", "engineer", "rocketeer", "tesla_trooper", "crazy_ivan", "terrorist", "desolator", "yuri", "chrono_ivan", "yuri_prime",

        // Vehicles and aircraft (R).
        "alliance_harvester", "alliance_light_tank", "alliance_ifv", "alliance_tank_destroyer", "alliance_mirage_tank", "alliance_prism_tank",
        "alliance_mcv", "alliance_harrier", "alliance_black_eagle", "alliance_nighthawk",
        "alliance_amphibious_transport", "alliance_destroyer", "alliance_aegis_cruiser", "alliance_dolphin", "alliance_aircraft_carrier",
        "harvester", "heavy_tank", "support_vehicle", "terror_drone", "demolition_truck", "rocket_artillery", "tesla_tank", "apocalypse_tank",
        "mcv", "kirov_airship", "hover_transport", "typhoon_sub", "sea_scorpion", "giant_squid", "dreadnought",
    ];

    /// <summary>The place of an item in the order of RA2 (<see cref="Ra2Order"/>); one not named comes after all of them.</summary>
    public static int Ra2Place(string id)
    {
        var place = Array.IndexOf(Ra2Order, id);
        return place < 0 ? Ra2Order.Length : place;
    }

    /// <summary>
    /// The squares a tab shows, as in RA2 (the owner, 2026-10-26): those the player can order now or has in its queue — an item whose
    /// prerequisites are missing is not there until they are, another country's own never — of its own faction and of the other, whose
    /// items a building taken from it opens (<see cref="TechTree"/>).
    /// </summary>
    public static IReadOnlyList<Cameo> Cameos(
        GameWorld world, PlayerId player, BuildGroup group, Func<string, string, string>? nameOf = null, string paradropName = "Paradrop") =>
        group == BuildGroup.Defense
            ? [.. Superweapons(world, player, nameOf, paradropName), .. Squares(world, player, group, nameOf, everyFaction: true, availableOnly: true)]
            : Squares(world, player, group, nameOf, everyFaction: true, availableOnly: true);

    /// <summary>
    /// The superweapons of the player, first on the Defense tab (the owner, 2026-10-30), in the order their buildings stand in the world:
    /// a square each, its clock the charge, Ready when it may be fired, On Hold without power. A paradrop is named Paradrop whichever building sends it — the
    /// USA with a taken airfield has two — the tooltip names the building.
    /// </summary>
    public static IReadOnlyList<Cameo> Superweapons(
        GameWorld world, PlayerId player, Func<string, string, string>? nameOf = null, string paradropName = "Paradrop")
    {
        var cameos = new List<Cameo>();
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, IsReady: true } building || building.Owner != player
                || SuperweaponSystem.KindOf(world, building) is var kind && kind == SuperweaponKind.None)
            {
                continue;
            }

            var from = building.Def is { } def ? nameOf?.Invoke(def.Id, def.Name) ?? def.Name
                : world.Rules.Database.TryGetMapBuilding(building.Type.Id, out var map) ? map.Name : building.Type.Id;
            var name = kind == SuperweaponKind.Paradrop ? paradropName : from;
            var recharge = Math.Max(1, SuperweaponSystem.RechargeOf(building));
            var ready = building.ChargeLeft == 0;
            // Without power it neither charges nor fires: On Hold, as in RA2.
            var state = !Rts.Simulation.Combat.Defenses.IsOnline(world, building) ? CameoState.OnHold
                : ready ? CameoState.Ready : CameoState.Building;
            var seconds = (building.ChargeLeft + SimulationSettings.DefaultTicksPerSecond - 1) / SimulationSettings.DefaultTicksPerSecond;
            cameos.Add(new Cameo(
                ProductionKind.Building, -1, name, 0, state,
                ready ? 1f : Math.Clamp(1f - building.ChargeLeft / (float)recharge, 0f, 1f), 0, "")
            {
                Superweapon = building.Id, From = from, SecondsLeft = seconds, Role = CameoRole.Superweapon, Code = CodeOf(name),
            });
        }

        return cameos;
    }

    /// <summary>
    /// Every square of a tab, in the order of RA2 (<see cref="Ra2Place"/>): every item of the player's faction in the tab's category, the
    /// unavailable ones too. Units are only those the simulation knows how to drive. The construction yard is left out: it comes from the
    /// MCV.
    /// </summary>
    public static IReadOnlyList<Cameo> AllCameos(GameWorld world, PlayerId player, BuildGroup group, Func<string, string, string>? nameOf = null) =>
        Squares(world, player, group, nameOf, everyFaction: false, availableOnly: false);

    /// <summary>
    /// The squares of a tab in the order of RA2: of the player's faction, or of every faction when <paramref name="everyFaction"/>; with
    /// <paramref name="availableOnly"/> not the unavailable ones — left out before their names and what they miss are made: the tab is
    /// built on every tick, and most items of every faction are unavailable (the performance log, 2026-10-28).
    /// </summary>
    private static IReadOnlyList<Cameo> Squares(
        GameWorld world, PlayerId player, BuildGroup group, Func<string, string, string>? nameOf, bool everyFaction, bool availableOnly)
    {
        // The name on screen of an id and its name of the data (DisplayNames: those of RA2 for now).
        string Name(string id, string name) => nameOf?.Invoke(id, name) ?? name;

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
                if ((everyFaction || def.Faction == state.Faction.Id) && def.Category == category && !world.Rules.IsConstructionYard(def.Id)
                    && state.CanBuildBuilding(i) is var available && (available || !availableOnly || Queued(queue, ProductionKind.Building, i)))
                {
                    var name = Name(def.Id, def.Name);
                    var limit = !SuperweaponSystem.UnderLimit(world, player, i);
                    cameos.Add(CameoOf(rules, queue, ProductionKind.Building, i, name, def.Cost, available, def.Prerequisites, owned, nameOf, limit)
                        with { Role = RoleOf(def), Code = CodeOf(name) });
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
                if ((everyFaction || def.Faction == state.Faction.Id) && shown && !def.Spawned && UnitTypes.TryGetById(def.Id, out _)
                    && state.Queue(ProductionRules.CategoryOf(rules, ProductionKind.Unit, i)) is var own
                    && (state.CanBuildUnit(i) || !availableOnly || Queued(own, ProductionKind.Unit, i)))
                {
                    var cost = ProductionRules.CostOf(rules, ProductionKind.Unit, i);
                    var name = Name(def.Id, def.Name);
                    var limit = !ProductionRules.UnderUnitLimit(world, player, i);
                    cameos.Add(CameoOf(rules, own, ProductionKind.Unit, i, name, cost, state.CanBuildUnit(i), def.Prerequisites, owned, nameOf, limit)
                        with { Role = RoleOf(def.Category), Code = CodeOf(name) });
                }
            }
        }

        // A stable sort: the items not named keep the order of the data.
        return [.. cameos.OrderBy(cameo => Ra2Place(IdOf(rules, cameo)))];
    }

    private static bool Queued(IReadOnlyList<ProductionItem> queue, ProductionKind kind, int index)
    {
        foreach (var item in queue)
        {
            if (item.Kind == kind && item.DefIndex == index)
            {
                return true;
            }
        }

        return false;
    }

    private static string IdOf(RulesDatabase rules, Cameo cameo) =>
        cameo.Kind == ProductionKind.Building ? rules.Buildings[cameo.DefIndex].Id : rules.Units[cameo.DefIndex].Id;

    /// <summary>
    /// The rows a tab of <paramref name="count"/> squares may be scrolled by: none while it fits the grid
    /// (<see cref="SidebarLayout.Slots"/>), at most so far that its last row is the grid's last.
    /// </summary>
    public static int ScrollRows(int count, int rows)
    {
        var extra = Math.Max(0, count - SidebarLayout.Slots);
        return Math.Clamp(rows, 0, (extra + SidebarLayout.Columns - 1) / SidebarLayout.Columns);
    }

    /// <summary>
    /// A left click: a ready building goes to the placement mode, a paused item goes on (the order command resumes it), anything that
    /// can be ordered is ordered, one or, for units with Shift, five. Unavailable and busy squares do nothing.
    /// </summary>
    public static CameoAction LeftClick(Cameo cameo, bool shift) => cameo.Superweapon is not null ? CameoAction.None : cameo.State switch
    {
        CameoState.Ready => new CameoAction(CameoActionKind.Place, cameo.Kind, cameo.DefIndex),
        CameoState.OnHold => new CameoAction(CameoActionKind.Produce, cameo.Kind, cameo.DefIndex),
        CameoState.Unavailable or CameoState.Busy => CameoAction.None,
        // Units queue behind the one in work; a building takes its queue alone, so a second order of it does nothing.
        CameoState.Building or CameoState.WaitingForFunds when cameo.Kind == ProductionKind.Building => CameoAction.None,
        _ => new CameoAction(CameoActionKind.Produce, cameo.Kind, cameo.DefIndex, shift && cameo.Kind == ProductionKind.Unit ? ProductionRules.MaxOrder : 1),
    };

    /// <summary>
    /// The building of a tab ready to place, its place in the rules; null with none. The tab's key takes it up as a click on its square
    /// would (the owner, 2026-10-26: Q with a power plant ready shows where it can stand).
    /// </summary>
    public static int? ReadyToPlace(IReadOnlyList<Cameo> cameos)
    {
        foreach (var cameo in cameos)
        {
            if (cameo is { State: CameoState.Ready, Kind: ProductionKind.Building, Superweapon: null })
            {
                return cameo.DefIndex;
            }
        }

        return null;
    }

    /// <summary>
    /// A right click, as in RA2: an item in work is put on hold; one on hold or ready is cancelled, the last of its kind, with the
    /// credits back.
    /// </summary>
    public static CameoAction RightClick(Cameo cameo) => cameo.Superweapon is not null ? CameoAction.None : cameo.State switch
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
        HashSet<string> owned,
        Func<string, string, string>? nameOf = null,
        bool atLimit = false)
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
        var faction = kind == ProductionKind.Building ? rules.Buildings[index].Faction : rules.Units[index].Faction;
        var missing = available || availableLater ? "" : Missing(rules, prerequisites, owned, nameOf, faction);
        var later = kind == ProductionKind.Building ? rules.Buildings[index].AbilityLater : rules.Units[index].AbilityLater;
        if (first is not null)
        {
            var progress = Math.Clamp(first.Progress / (float)ProductionRules.TotalProgress(rules, kind, index), 0f, 1f);
            var state = first.Ready ? CameoState.Ready
                : first.Paused ? CameoState.OnHold
                : first.WaitingForFunds ? CameoState.WaitingForFunds
                : CameoState.Building;
            return new Cameo(kind, index, name, cost, state, first.Ready ? 1f : progress, count, missing)
            {
                AbilityLater = later, AvailableLater = availableLater, AtLimit = atLimit,
            };
        }

        var full = kind == ProductionKind.Building ? queue.Count > 0 : queue.Count >= ProductionRules.MaxUnitQueue;
        var free = !available || atLimit ? CameoState.Unavailable : full ? CameoState.Busy : CameoState.Available;
        return new Cameo(kind, index, name, cost, free, 0f, 0, missing) { AbilityLater = later, AvailableLater = availableLater, AtLimit = atLimit };
    }

    /// <summary>
    /// The short code of a name: the first letters of up to three words (Grizzly Battle Tank: GBT); a word with a digit whole (V3);
    /// a name of one word, its first three letters (Barracks: BAR); a name of two letters as it is (GI).
    /// </summary>
    public static string CodeOf(string name)
    {
        var words = name.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1)
        {
            return words[0].Length <= 3 ? words[0].ToUpperInvariant() : words[0][..3].ToUpperInvariant();
        }

        var code = new System.Text.StringBuilder();
        foreach (var word in words)
        {
            code.Append(word.Any(char.IsDigit) ? word.ToUpperInvariant() : char.ToUpperInvariant(word[0]).ToString());
            if (code.Length >= 3)
            {
                break;
            }
        }

        return code.Length > 4 ? code.ToString()[..4] : code.ToString();
    }

    /// <summary>The role of a unit by its category.</summary>
    public static CameoRole RoleOf(UnitCategory category) => category switch
    {
        UnitCategory.Infantry => CameoRole.Infantry,
        UnitCategory.Naval => CameoRole.Naval,
        UnitCategory.Aircraft => CameoRole.Aircraft,
        _ => CameoRole.Vehicle,
    };

    /// <summary>The role of a building: its superweapon, a wall, power, production, a weapon or a garrison, or support.</summary>
    public static CameoRole RoleOf(BuildingDef def) =>
        def.Superweapon != SuperweaponKind.None ? CameoRole.Superweapon
        : def.Wall ? CameoRole.Wall
        : def.Power > 0 ? CameoRole.Power
        : def.Produces.Length > 0 ? CameoRole.Production
        : def.Primary.Length > 0 || def.GarrisonCapacity > 0 ? CameoRole.Defense
        : CameoRole.Support;

    private static string Missing(
        RulesDatabase rules, ImmutableArray<string> prerequisites, HashSet<string> owned, Func<string, string, string>? nameOf, string faction)
    {
        var names = new List<string>();
        foreach (var id in prerequisites)
        {
            if (!owned.Contains(id))
            {
                // A group of RA2 (power, proc, radar, barracks, tech) by the first building of the faction in it.
                var index = rules.IndexOfBuilding(id);
                if (index < 0 || rules.Buildings[index].Faction != faction)
                {
                    var member = Array.FindIndex(rules.Buildings.ToArray(), def => def.Faction == faction && def.Provides.Contains(id));
                    index = member >= 0 ? member : index;
                }

                names.Add(index >= 0 ? nameOf?.Invoke(rules.Buildings[index].Id, rules.Buildings[index].Name) ?? rules.Buildings[index].Name : id);
            }
        }

        return string.Join(", ", names);
    }

    private static bool HasSuperweapon(GameWorld world, PlayerId player)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true } building && building.Owner == player
                && SuperweaponSystem.KindOf(world, building) != SuperweaponKind.None)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> ReadyBuildings(GameWorld world, PlayerId player)
    {
        var owned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && building.Owner == player)
            {
                owned.Add(def.Id);
                owned.UnionWith(def.Provides);
            }
        }

        return owned;
    }
}
