using System.Collections.Generic;
using Rts.AI.Knowledge;
using Rts.AI.View;
using Rts.Simulation.Map;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The base of a bot (docs/implementation/M5_first_bot.md, 4.6–4.7, steps 4–5). First the MCV deploys — where it stands, or on the nearest
/// cell within <see cref="FarReach"/> where the yard fits as far as the bot has seen and no defense of nobody reaches it (2026-10-27: on
/// WAR Pro the Grand Cannons of the middle reach the inner starts); with none seen it drives to the nearest ground not
/// explored yet, with all explored <see cref="ScoutStep"/> cells toward the middle of the map; a move that has not got it anywhere for
/// <see cref="StuckTicks"/> is given up for a new one. Then, on every turn, a ready building is placed where <see cref="BasePlacement"/>
/// finds room, and with a queue empty the next building is ordered, the most important first:
/// <list type="number">
/// <item>what the economy needs at once — a reactor, a refinery (<see cref="AiPlan.Wants"/>);</item>
/// <item>the next step of the build order of the data (<see cref="AiSettings.BuildOrder"/>) not done yet: a step is done when the base
/// has as many such buildings, standing, growing or in production, as the order has up to it — a lost one is built again; a step that
/// cannot be built yet (its prerequisite still grows) is waited for, nothing after it goes first;</item>
/// <item>the rest of the economy's needs — a war factory for the harvesters;</item>
/// <item>after the order — refineries up to <see cref="AiSettings.MaxRefineries"/>, a second war factory once the credits reach
/// <see cref="AiSettings.SecondFactoryCredits"/>, and the defense of the order once more when the base was attacked from a side no
/// defense covers (<see cref="AiPlan.AttackedFrom"/>, step 9), up to <see cref="AiSettings.MaxDefenses"/>; it faces that side;</item>
/// <item>M11: the shipyard of the faction on a map whose water leads to an enemy (<see cref="MapReading.NavalRoute"/>), at its harbour once
/// that has been seen — till then the nearest unit of the army goes to look, and a shipyard with no room on the water after all is
/// cancelled; and, but for the simple build orders, with the army as big as a squad that may set out and the money there, the building
/// that opens the most units of the army (<see cref="Opener"/>), then one of each superweapon it may build (<see cref="Superweapon"/>);</item>
/// <item>before the tuning: the purifier of the ore once a refinery stands (<see cref="Purifier"/>); toward the side attacked, the
/// defense that fights best what the enemy fields (<see cref="Against"/>); against its fliers, defenses that fire at the air — one, and
/// one more for every three seen, <see cref="MaxAirDefenses"/> at most (not for «Easy», which picks nothing against the enemy).</item>
/// </list>
/// A building is ordered only when the credits, less what the queues still have to pay, cover it; one order a queue a turn. The bot
/// gives no command it knows to be refused.
/// </summary>
public sealed class BuildPlanner(AiSettings settings) : IAiModule
{
    /// <summary>How far, in cells, the MCV may go to find flat ground for its yard.</summary>
    public const int DeployReach = 6;

    /// <summary>
    /// How far, in cells, the MCV looks over explored ground for its yard when nothing within <see cref="DeployReach"/> will do: on hills
    /// the yard of RA2, 4 × 4, may find flat ground only farther (the relief test map).
    /// </summary>
    public const int FarReach = 24;

    /// <summary>How far the MCV drives to see more ground when it found none for its yard.</summary>
    public const int ScoutStep = 4;

    /// <summary>An MCV on its way that has not moved for this many ticks is stuck: it is given a new way (6 s).</summary>
    public const int StuckTicks = 90;

    private Rts.Simulation.Geometry.WorldPos _mcvAt;
    private int _mcvStillSince;

    private static readonly ProductionCategory[] Categories =
        [ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Building, ProductionCategory.Defense];

    private readonly BasePlacement _placement = new();
    private readonly List<int> _choices = new();
    private readonly List<string> _upcoming = new();

    /// <summary>The steps of the build order the debug window shows ahead (M8 step 12).</summary>
    public const int UpcomingSteps = 5;
    private int[] _have = [];

    public AiModuleKind Kind => AiModuleKind.BuildPlanner;

    public void Update(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var yardIndex = rules.IndexOfBuilding(view.Player.Faction.ConstructionYard);
        Building? yard = null;
        foreach (var building in view.OwnBuildings)
        {
            if (building.DefIndex == yardIndex)
            {
                yard = building;
                break;
            }
        }

        if (yard is null)
        {
            Deploy(context, rules.Buildings[yardIndex]);
            return;
        }

        PlaceReady(context, yard);
        Order(context, yard);
    }

    /// <summary>The first ready building of the building queue, else of the defense queue, goes where the placement finds room.</summary>
    private void PlaceReady(AiContext context, Building yard)
    {
        var view = context.View;
        foreach (var category in new[] { ProductionCategory.Building, ProductionCategory.Defense })
        {
            if (view.Player.Queue(category) is not [{ Ready: true } ready, ..])
            {
                continue;
            }

            _placement.Toward = context.Plan.AttackedFrom;
            _placement.Harbour = context.Map.Harbour;
            if (!_placement.TryFind(view, context.Memory, yard, ready.DefIndex, out var corner))
            {
                return;
            }

            if (corner is null && view.Rules.Database.Buildings[ready.DefIndex].Water)
            {
                // No room on the water for the shipyard after all (M11): its money back, rather than a queue held for ever.
                context.Issue(new CancelProductionCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Building, ready.DefIndex));
                context.Log?.Add(context.Tick, context.Module, $"finds no water for {view.Rules.Database.Buildings[ready.DefIndex].Id}, cancels it");
            }

            if (corner is { } place)
            {
                context.Issue(new PlaceBuildingCommand(view.Me, context.ExecuteTick, context.NextSequence(), ready.DefIndex, place));
                context.Plan.LastPlacement = (ready.DefIndex, place);
                context.Log?.Add(context.Tick, context.Module, $"places {view.Rules.Database.Buildings[ready.DefIndex].Id} at {place.X},{place.Y}"
                    + (_placement.Toward is { } toward ? $", toward the attacks from {toward.X},{toward.Y}" : ""));
            }

            return;
        }
    }

    /// <summary>Orders the most important building there is room and money for, one a queue.</summary>
    private void Order(AiContext context, Building yard)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        CountBuildings(view);
        var money = view.Player.Credits - Promised(view);
        var buildingQueueFree = view.Player.Queue(ProductionCategory.Building).Count == 0;
        var defenseQueueFree = view.Player.Queue(ProductionCategory.Defense).Count == 0;
        Choose(context, yard);
        context.Plan.NextBuilding = -1;
        foreach (var index in _choices)
        {
            if (index < 0)
            {
                // A step of the order that cannot be built yet: nothing after it goes first.
                return;
            }

            var def = rules.Buildings[index];
            var free = def.Category == ProductionCategory.Defense ? defenseQueueFree : buildingQueueFree;
            if (!free || !view.Player.CanBuildBuilding(index))
            {
                continue;
            }

            if (def.Cost > money)
            {
                // The money is not there yet: wait for it rather than build something cheaper first, and keep the army off it.
                context.Plan.NextBuilding = index;
                return;
            }

            context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Building, index, 1));
            money -= def.Cost;
            if (def.Category == ProductionCategory.Defense)
            {
                defenseQueueFree = false;
            }
            else
            {
                buildingQueueFree = false;
            }
        }
    }

    /// <summary>The buildings to order, the most important first; -1 marks a step of the order that has to be waited for.</summary>
    private void Choose(AiContext context, Building yard)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var faction = view.Player.Faction;
        var refinery = rules.IndexOfBuilding(faction.Refinery);
        var reactor = EconomyManager.PowerPlant(rules, faction.Id);
        var factory = EconomyManager.Factory(rules, faction.Id, ProductionCategory.Vehicle);
        _choices.Clear();
        foreach (var wanted in context.Plan.Wants)
        {
            if (wanted == reactor || wanted == refinery)
            {
                _choices.Add(wanted);
            }
        }

        var orderDone = true;
        _upcoming.Clear();
        if (rules.TryGetBuildOrder(settings.BuildOrder, out var order) && order.Faction == faction.Id)
        {
            var counts = new int[rules.Buildings.Length];
            foreach (var step in order.Steps)
            {
                var index = rules.IndexOfBuilding(step);

                // With the ore gone the base is done: what only earns or opens technologies is not built again (step 14).
                if (context.Plan.OreGone && EconomyManager.Spare(rules.Buildings[index]))
                {
                    continue;
                }

                counts[index]++;
                if (_have[index] < counts[index])
                {
                    if (orderDone)
                    {
                        _choices.Add(view.Player.CanBuildBuilding(index) ? index : -1);
                        orderDone = false;
                    }

                    // The next steps too, for the debug window (M8 step 12), only while the journal is on.
                    _upcoming.Add(step);
                    if (context.Log is null || _upcoming.Count == UpcomingSteps)
                    {
                        break;
                    }
                }
            }
        }

        if (context.Log is not null)
        {
            context.Plan.Upcoming = [.. _upcoming];
        }

        foreach (var wanted in context.Plan.Wants)
        {
            if (wanted != reactor && wanted != refinery)
            {
                _choices.Add(wanted);
            }
        }

        if (orderDone)
        {
            if (_have[refinery] < settings.MaxRefineries && !context.Plan.OreGone)
            {
                _choices.Add(refinery);
            }

            if (factory >= 0 && _have[factory] < 2 && view.Player.Credits - Promised(view) >= settings.SecondFactoryCredits)
            {
                _choices.Add(factory);
            }

            // The shipyard on a map whose water leads to an enemy (M11), once its place has been seen; till then a unit goes to look.
            var shipyard = EconomyManager.Factory(rules, faction.Id, ProductionCategory.Naval);
            if (shipyard >= 0 && _have[shipyard] == 0 && context.Map.NavalRoute && !context.Plan.OreGone && view.Player.CanBuildBuilding(shipyard))
            {
                if (Seen(view, rules.Buildings[shipyard], context.Map.Harbour!.Value))
                {
                    _choices.Add(shipyard);
                }
                else
                {
                    LookAtHarbour(context, rules.Buildings[shipyard], context.Map.Harbour.Value);
                }
            }

            // The purifier of the ore (before the tuning): with a refinery standing and the money there, one.
            if (settings.Quality != Profiles.BuildOrderQuality.Simple && Purifier(context, refinery) is >= 0 and var purifier
                && view.Player.Credits - Promised(view) >= rules.Buildings[purifier].Cost)
            {
                _choices.Add(purifier);
            }

            // Up the technology (M11): with the army big enough to set out and the money there, the building that opens the most units.
            if (settings.Quality != Profiles.BuildOrderQuality.Simple && Opener(context) is >= 0 and var opener
                && view.Player.Credits - Promised(view) >= rules.Buildings[opener].Cost)
            {
                _choices.Add(opener);
            }

            // A superweapon (M11 step 3), one of each the faction may build, on the same terms.
            if (settings.Quality != Profiles.BuildOrderQuality.Simple && Superweapon(context) is >= 0 and var superweapon
                && view.Player.Credits - Promised(view) >= rules.Buildings[superweapon].Cost)
            {
                _choices.Add(superweapon);
            }

            // Against the side attacked, the defense that fights best what the enemy fields (before the tuning), else that of the order.
            if (context.Plan.AttackedFrom is { } from && order is not null && order.Faction == faction.Id
                && (Against(context, air: false) is >= 0 and var fitted ? fitted : DefenseOf(rules, order)) is >= 0 and var defense
                && Defenses(rules) < settings.MaxDefenses && !Covered(view, yard, from))
            {
                _choices.Add(defense);
            }

            // Against the air the enemy fields, defenses that fire at it: one, and one more for every three fliers seen.
            if (settings.CounterPickPercent > 0 && Fliers(context) is var fliers && fliers >= MinFliers
                && AirDefenses(rules) < IntMath.Min(MaxAirDefenses, 1 + fliers / 300) && Against(context, air: true) is >= 0 and var antiAir
                && view.Player.Queue(ProductionCategory.Defense).Count == 0)
            {
                _choices.Add(antiAir);
            }
        }
    }

    /// <summary>Whether every cell of <paramref name="def"/> on <paramref name="corner"/> has been explored.</summary>
    private static bool Seen(AiWorldView view, BuildingDef def, CellPos corner)
    {
        for (var y = corner.Y; y < corner.Y + def.Footprint.Height; y++)
        {
            for (var x = corner.X; x < corner.X + def.Footprint.Width; x++)
            {
                if (!view.IsExplored(new CellPos(x, y)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The unit of the army nearest the harbour, not the scout, goes to the land nearest its middle to see it (M11) — once, while it is not
    /// on its way there already; the squads take it back once it stands.
    /// </summary>
    private static void LookAtHarbour(AiContext context, BuildingDef shipyard, CellPos harbour)
    {
        var view = context.View;
        var middle = new CellPos(harbour.X + shipyard.Footprint.Width / 2, harbour.Y + shipyard.Footprint.Height / 2);
        CellPos? shore = null;
        for (var ring = 0; ring <= MapReading.HarbourReach && shore is null; ring++)
        {
            for (var dy = -ring; dy <= ring && shore is null; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(middle.X + dx, middle.Y + dy);
                    // By the map as it was before the match: in the shroud every cell is enterable to the bot.
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && view.CanEnterOnPreview(cell, Locomotor.Track))
                    {
                        shore = cell;
                        break;
                    }
                }
            }
        }

        if (shore is not { } look)
        {
            return;
        }

        Unit? nearest = null;
        var best = int.MaxValue;
        foreach (var unit in view.OwnUnits)
        {
            if (!context.Units.InArmy(unit) || unit.Id == context.Plan.Scout)
            {
                continue;
            }

            // One goes, and once there stays till the squads take it back.
            var distance = Distance(unit.Position.ToCell(), look);
            if (distance <= 1 || (unit.MoveTarget is { } going && Distance(going.ToCell(), look) == 0))
            {
                return;
            }

            if (distance < best)
            {
                (nearest, best) = (unit, distance);
            }
        }

        if (nearest is not null)
        {
            context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [nearest.Id], look.Center));
            context.Log?.Add(context.Tick, context.Module, $"sends {nearest.Archetype.Id} to look at the water for the shipyard at {look.X},{look.Y}");
        }
    }

    /// <summary>
    /// The building that opens the most units of the army not yet open (M11), by the prerequisites — of a faction, its own country's, no
    /// stolen technology — or, when none opens one at once, the one that opens the way to such a building; of as many the cheaper, then the
    /// first. Only one the bot does not have yet, may build now, and that is no defense, no shipyard and no superweapon; -1 for none, and
    /// none while the army is smaller than a squad that may set out (<see cref="AiSettings.MinAttackArmy"/>).
    /// </summary>
    private int Opener(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var army = 0;
        foreach (var unit in view.OwnUnits)
        {
            army += context.Units.InArmy(unit) ? 1 : 0;
        }

        if (army < settings.MinAttackArmy)
        {
            return -1;
        }

        var owned = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var building in view.OwnBuildings)
        {
            if (building is { IsReady: true, Def: { } def })
            {
                owned.Add(def.Id);
                owned.UnionWith(def.Provides);
            }
        }

        var (best, bestOpened, bestCost) = (-1, 0, int.MaxValue);
        for (var deep = 0; deep < 2 && best < 0; deep++)
        {
            for (var i = 0; i < rules.Buildings.Length; i++)
            {
                var def = rules.Buildings[i];
                if (_have[i] > 0 || !view.Player.CanBuildBuilding(i) || def.Category == ProductionCategory.Defense || def.Water
                    || def.Superweapon != SuperweaponKind.None)
                {
                    continue;
                }

                var with = new SortedSet<string>(owned, System.StringComparer.Ordinal) { def.Id };
                with.UnionWith(def.Provides);
                var opened = deep == 0 ? Opened(context, with, def) : Unlocked(context, with);
                if (opened > bestOpened || (opened == bestOpened && opened > 0 && def.Cost < bestCost))
                {
                    (best, bestOpened, bestCost) = (i, opened, def.Cost);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The first superweapon of the rules the bot may build now and has none of — standing, growing or queued — once the army is as big
    /// as a squad that may set out; -1 for none.
    /// </summary>
    private int Superweapon(AiContext context)
    {
        var view = context.View;
        var army = 0;
        foreach (var unit in view.OwnUnits)
        {
            army += context.Units.InArmy(unit) ? 1 : 0;
        }

        if (army < settings.MinAttackArmy)
        {
            return -1;
        }

        var rules = view.Rules.Database;
        for (var i = 0; i < rules.Buildings.Length; i++)
        {
            if (rules.Buildings[i].Superweapon != SuperweaponKind.None && _have[i] == 0 && view.Player.CanBuildBuilding(i))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>How many units of the army, not open now, <paramref name="owned"/> opens — with a factory for them, or <paramref name="built"/> one.</summary>
    private static int Opened(AiContext context, SortedSet<string> owned, BuildingDef? built)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var opened = 0;
        foreach (var card in context.Units.Cards)
        {
            var unit = rules.Units[card.Index];
            if (card.Use == Knowledge.UnitUse.Army && unit.Faction == view.Player.Faction.Id && !view.Player.CanBuildUnit(card.Index)
                && !unit.AvailableLater && unit.RequiresStolen.Length == 0
                && Rts.Simulation.Systems.TechTree.Allowed(view.Player.Country, unit.Countries, unit.ForbiddenCountries)
                && All(unit.Prerequisites, owned)
                && (view.HasFactory(unit.ProducedIn) || (built is not null && built.Produces.Contains(unit.ProducedIn))))
            {
                opened++;
            }
        }

        return opened;
    }

    /// <summary>The most units of the army one more building of the faction opens on top of <paramref name="owned"/>.</summary>
    private static int Unlocked(AiContext context, SortedSet<string> owned)
    {
        var rules = context.View.Rules.Database;
        var best = 0;
        foreach (var def in rules.Buildings)
        {
            if (def.Faction == context.View.Player.Faction.Id && !owned.Contains(def.Id) && !def.Water && def.Category != ProductionCategory.Defense
                && def.Superweapon == SuperweaponKind.None && All(def.Prerequisites, owned))
            {
                var with = new SortedSet<string>(owned, System.StringComparer.Ordinal) { def.Id };
                with.UnionWith(def.Provides);
                best = IntMath.Max(best, Opened(context, with, def));
            }
        }

        return best;
    }

    private static bool All(System.Collections.Immutable.ImmutableArray<string> prerequisites, SortedSet<string> owned)
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

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));

    /// <summary>The fewest fliers seen, in hundredths of a unit fresh (<see cref="EnemyEstimate.Weight"/>), the bot builds a defense against the air for.</summary>
    public const int MinFliers = 200;

    /// <summary>The most defenses against the air the bot builds.</summary>
    public const int MaxAirDefenses = 3;

    /// <summary>The fliers of the enemy the bot knows of, in hundredths of a unit: aircraft and soldiers with jet packs.</summary>
    private static int Fliers(AiContext context)
    {
        var rules = context.View.Rules.Database;
        var fliers = 0;
        foreach (var (type, weight) in context.Memory.Estimate.Total.Composition)
        {
            if (rules.IndexOfUnit(UnitTypes.IdOf(type)) is >= 0 and var index && Knowledge.UnitKnowledge.RoleAgainst(rules.Units[index]) == ArmyRole.AntiAir)
            {
                fliers += weight;
            }
        }

        return fliers;
    }

    /// <summary>The defenses that fire at the air the base has: standing, growing, or in a queue.</summary>
    private int AirDefenses(RulesDatabase rules)
    {
        var count = 0;
        for (var i = 0; i < _have.Length; i++)
        {
            count += IsDefense(rules, i, air: true) ? _have[i] : 0;
        }

        return count;
    }

    /// <summary>Whether the building at <paramref name="index"/> is a defense with a weapon that fires at the air, or at the ground.</summary>
    private static bool IsDefense(RulesDatabase rules, int index, bool air)
    {
        var def = rules.Buildings[index];
        return def.Category == ProductionCategory.Defense && !def.Wall && def.Superweapon == SuperweaponKind.None
            && rules.IndexOfWeapon(def.Primary) is >= 0 and var weapon
            && (rules.Weapons[weapon].Targets & (air ? WeaponTargets.Air : WeaponTargets.Ground)) != 0;
    }

    /// <summary>
    /// The defense the bot may build now that fights best, for its cost, what the enemy fields — its fliers with <paramref name="air"/>,
    /// else the rest — by the damage of its weapon (<see cref="Knowledge.UnitKnowledge.DefenseDamagePerMinute"/>) × its hit points over
    /// its cost to the power 1.5, as the worth of a unit; -1 for none, or nothing of that seen.
    /// </summary>
    private static int Against(AiContext context, bool air)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var (best, bestWorth) = (-1, 0L);
        for (var i = 0; i < rules.Buildings.Length; i++)
        {
            if (!IsDefense(rules, i, air) || !view.Player.CanBuildBuilding(i) || rules.Buildings[i].Cost <= 0)
            {
                continue;
            }

            long damage = 0;
            foreach (var (type, weight) in context.Memory.Estimate.Total.Composition)
            {
                if (rules.IndexOfUnit(UnitTypes.IdOf(type)) is >= 0 and var target
                    && (Knowledge.UnitKnowledge.RoleAgainst(rules.Units[target]) == ArmyRole.AntiAir) == air)
                {
                    damage += context.Units.DefenseDamagePerMinute(i, target) * weight;
                }
            }

            var def = rules.Buildings[i];
            var worth = damage * def.Hp / ((long)def.Cost * IntMath.Sqrt((long)def.Cost * 256) / 16 + 1);
            if (worth > bestWorth)
            {
                (best, bestWorth) = (i, worth);
            }
        }

        return best;
    }

    /// <summary>The purifier of the ore of the faction the bot may build now and has none of, with a refinery of its own; -1 for none.</summary>
    private int Purifier(AiContext context, int refinery)
    {
        var rules = context.View.Rules.Database;
        if (refinery < 0 || _have[refinery] == 0)
        {
            return -1;
        }

        for (var i = 0; i < rules.Buildings.Length; i++)
        {
            if (rules.Buildings[i].IncomeBonusPercent > 0 && _have[i] == 0 && context.View.Player.CanBuildBuilding(i))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The defense of the build order — its last step with a weapon — or -1 for none.</summary>
    private static int DefenseOf(RulesDatabase rules, BuildOrderDef order)
    {
        var defense = -1;
        foreach (var step in order.Steps)
        {
            var index = rules.IndexOfBuilding(step);
            if (rules.Buildings[index].Primary.Length > 0)
            {
                defense = index;
            }
        }

        return defense;
    }

    /// <summary>The defenses with a weapon the base has: standing, growing or in a queue.</summary>
    private int Defenses(RulesDatabase rules)
    {
        var count = 0;
        for (var i = 0; i < _have.Length; i++)
        {
            count += rules.Buildings[i].Primary.Length > 0 ? _have[i] : 0;
        }

        return count;
    }

    /// <summary>
    /// Whether the side of the base toward <paramref name="from"/> has a defense: one with a weapon within
    /// <see cref="BasePlacement.DefenseAhead"/> of the point that far from the yard toward it, or one in the defense queue — it goes there.
    /// </summary>
    private static bool Covered(AiWorldView view, Building yard, CellPos from)
    {
        if (view.Player.Queue(ProductionCategory.Defense).Count > 0)
        {
            return true;
        }

        var centre = BasePlacement.Middle(yard);
        var (dx, dy) = (from.X - centre.X, from.Y - centre.Y);
        var length = IntMath.Max(IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)), 1);
        var point = new CellPos(centre.X + dx * BasePlacement.DefenseAhead / length, centre.Y + dy * BasePlacement.DefenseAhead / length);
        foreach (var building in view.OwnBuildings)
        {
            if (building.Weapons.Length > 0
                && IntMath.Max(IntMath.Abs(building.Cell.X - point.X), IntMath.Abs(building.Cell.Y - point.Y)) <= BasePlacement.DefenseAhead)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>How many of each building the base has: standing, growing, or in a queue.</summary>
    private void CountBuildings(AiWorldView view)
    {
        var buildings = view.Rules.Database.Buildings.Length;
        if (_have.Length != buildings)
        {
            _have = new int[buildings];
        }

        System.Array.Clear(_have);
        foreach (var building in view.OwnBuildings)
        {
            if (building.Def is not null)
            {
                _have[building.DefIndex]++;
            }
        }

        foreach (var category in new[] { ProductionCategory.Building, ProductionCategory.Defense })
        {
            foreach (var item in view.Player.Queue(category))
            {
                _have[item.DefIndex]++;
            }
        }
    }

    /// <summary>What the queues still have to pay.</summary>
    private static int Promised(AiWorldView view)
    {
        var promised = 0;
        foreach (var category in Categories)
        {
            foreach (var item in view.Player.Queue(category))
            {
                promised += ProductionRules.CostOf(view.Rules.Database, item.Kind, item.DefIndex) - item.Paid;
            }
        }

        return promised;
    }

    /// <summary>The MCV deploys where its yard fits, or drives to the nearest cell where it does; it waits while it drives.</summary>
    private void Deploy(AiContext context, BuildingDef yard)
    {
        var view = context.View;
        Unit? mcv = null;
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Archetype.Id == view.Player.Faction.Mcv)
            {
                mcv = unit;
                break;
            }
        }

        if (mcv is null)
        {
            return;
        }

        // On its way it is waited for — unless it has not moved for StuckTicks: on a slope a move may never end.
        if (mcv.Position != _mcvAt)
        {
            (_mcvAt, _mcvStillSince) = (mcv.Position, context.Tick);
        }

        if (mcv.MoveTarget is not null && context.Tick - _mcvStillSince < StuckTicks)
        {
            return;
        }

        var here = mcv.Position.ToCell();
        if (Deploys(context, yard, here, mcv))
        {
            context.Issue(new DeployCommand(view.Me, context.ExecuteTick, context.NextSequence(), mcv.Id));
            return;
        }

        for (var ring = 1; ring <= FarReach; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(here.X + dx, here.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && view.CanEnter(cell, mcv.Archetype.Locomotor)
                        && Deploys(context, yard, cell, mcv))
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [mcv.Id], cell.Center));
                        return;
                    }
                }
            }
        }

        // Nothing seen will do: to the nearest ground not explored yet, to see it.
        for (var ring = 1; ring <= FarReach; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(here.X + dx, here.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && view.Contains(cell) && !view.IsExplored(cell)
                        && view.CanEnterOnPreview(cell, mcv.Archetype.Locomotor) && context.Memory.NobodysDefenseReaching(cell) is null)
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [mcv.Id], cell.Center));
                        return;
                    }
                }
            }
        }

        // All of it explored: a few cells toward the middle of the map, to the nearest cell it can enter there.
        var middle = new CellPos(view.Width / 2, view.Height / 2);
        var ahead = new CellPos(
            here.X + IntMath.Max(-ScoutStep, IntMath.Min(ScoutStep, middle.X - here.X)),
            here.Y + IntMath.Max(-ScoutStep, IntMath.Min(ScoutStep, middle.Y - here.Y)));
        for (var ring = 0; ring <= 2; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(ahead.X + dx, ahead.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && cell != here && view.CanEnter(cell, mcv.Archetype.Locomotor)
                        && context.Memory.NobodysDefenseReaching(cell) is null)
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [mcv.Id], cell.Center));
                        return;
                    }
                }
            }
        }
    }

    /// <summary>Whether the yard of <paramref name="mcv"/> fits with its middle on <paramref name="cell"/>, out of the reach of every defense of nobody.</summary>
    private static bool Deploys(AiContext context, BuildingDef yard, CellPos cell, Unit mcv)
    {
        var corner = Corner(cell, yard);
        var far = new CellPos(corner.X + yard.Footprint.Width - 1, corner.Y + yard.Footprint.Height - 1);
        return context.Memory.NobodysDefenseReaching(corner, far, 0) is null
            && context.View.CanPlace(yard, corner, needsBase: false, ignoreUnit: mcv.Id);
    }

    /// <summary>The north-west cell of <paramref name="def"/> centred on <paramref name="centre"/>, as a deploy places the yard.</summary>
    private static CellPos Corner(CellPos centre, BuildingDef def) =>
        new(centre.X - (def.Footprint.Width - 1) / 2, centre.Y - (def.Footprint.Height - 1) / 2);
}
