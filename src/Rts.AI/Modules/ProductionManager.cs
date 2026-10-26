using System.Linq;
using System.Collections.Generic;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The army of a bot (docs/implementation/M5_first_bot.md, 4.6, step 6). The target is the army of the data
/// (<see cref="AiSettings.Army"/>): the share of every kind of unit the bot can build now — a factory of its category stands and its
/// prerequisites are there. The shares are not made a whole again: a kind not yet open keeps its share empty, so that the barracks
/// alone do not turn all the money into infantry before the war factory stands. The counters (M8, 4.6, step 7):
/// <see cref="AiSettings.CounterPickPercent"/> of the army is picked against what the enemy fields — the infantry and the vehicles of
/// <see cref="EnemyEstimate.Total"/>, fading with age — by the table of the army, <see cref="ArmyDef.AgainstInfantry"/> and
/// <see cref="ArmyDef.AgainstVehicles"/>; the rest is the army as it is (<see cref="Counter"/>). Among the factories whose queue holds fewer units than they keep (<see cref="AiSettings.InfantryQueued"/>,
/// <see cref="AiSettings.VehiclesQueued"/>), the kind that
/// falls shortest of its share — and short it must be: its share of the army with one more is above what there is — is ordered first,
/// the army counted with what is queued; one a factory a turn, equal ones to the smaller index; a kind that cannot be paid for yet is
/// saved for, not passed over for a cheaper one. It pays only from what is left when the queues and the building the planner waits for
/// (<see cref="AiPlan.NextBuilding"/>) are paid — that one not while the base is defended (<see cref="AiPlan.ArmyFirst"/>) — and not at
/// all when the strategist holds the army back. A factory none of whose kinds is short waits: the mix of the data holds, and money that
/// piles up goes to the second war factory of the planner, which makes the tanks — the slowest part of the army — come faster. While the
/// economy wants a harvester (<see cref="AiPlan.HarvestersWanted"/>) the war factory is left to it.
/// Since M11 (the bot that knows every weapon) the army of the data may be one of roles: the bot then picks the units itself, by what
/// their weapons do (<see cref="Roles"/>), builds a hero apart from them, and the shipyard and the air command are factories too.
/// </summary>
public sealed class ProductionManager(AiSettings settings) : IAiModule
{
    private static readonly ProductionCategory[] Factories =
        [ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Naval, ProductionCategory.Aircraft];

    /// <summary>The units of a role that share it, the best first, and their parts of it: 3 : 2 : 1 (M11).</summary>
    public static readonly int[] RoleParts = [3, 2, 1];

    private static readonly ProductionCategory[] Queues =
        [ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Building, ProductionCategory.Defense];

    private readonly List<(int Unit, long Share)> _target = new();
    private readonly SortedDictionary<int, long> _byUnit = new();
    private readonly List<Knowledge.UnitCard> _candidates = new();
    private readonly List<(int Unit, long Shortfall, int Order)> _short = new();
    private int[] _have = [];

    public AiModuleKind Kind => AiModuleKind.Production;

    public void Update(AiContext context)
    {
        var view = context.View;
        if (!context.Plan.ArmyAllowed || Target(context) == 0)
        {
            return;
        }

        var rules = view.Rules.Database;
        var money = view.Player.Credits - Promised(view)
            - (context.Plan.NextBuilding >= 0 && !context.Plan.ArmyFirst ? rules.Buildings[context.Plan.NextBuilding].Cost : 0);
        var army = CountArmy(view);
        var open = 0;
        foreach (var category in Factories)
        {
            // While the economy wants a harvester the war factory is left to it: a queue runs first come, first served.
            // The shipyard and the air command keep as many as the war factory.
            var queued = category == ProductionCategory.Infantry ? settings.InfantryQueued : settings.VehiclesQueued;
            if (view.Player.Queue(category).Count < queued && view.HasFactory(category)
                && !(category == ProductionCategory.Vehicle && context.Plan.HarvestersWanted))
            {
                open |= 1 << (int)category;
            }
        }

        // A hero first (M11), one at a time, with an army of roles.
        if (rules.TryGetArmy(settings.Army, out var def) && !def.Roles.IsEmpty)
        {
            Hero(context, ref open, ref money);
        }

        // The kind that falls shortest of its share comes first, whichever factory makes it; when it cannot be paid for yet, the money
        // is saved for it rather than spent on a cheaper kind, which would tilt the army.
        while (open != 0)
        {
            var unit = Shortest(context, rules, open, army);
            if (unit < 0)
            {
                return;
            }

            var cost = rules.Units[unit].Cost;
            if (cost > money)
            {
                return;
            }

            context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Unit, unit, 1));
            money -= cost;
            _have[unit]++;
            army++;
            open &= ~(1 << (int)CategoryOf(rules.Units[unit]));
        }
    }

    /// <summary>
    /// A hero the bot can build now (<see cref="Knowledge.UnitUse.Hero"/>), with fewer of its kind than the limit of the data living and
    /// queued, its factory open and the money there, is ordered (M11): one a turn, the first of the rules.
    /// </summary>
    private void Hero(AiContext context, ref int open, ref int money)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        foreach (var card in context.Units.Cards)
        {
            var unit = rules.Units[card.Index];
            var category = CategoryOf(unit);
            if (card.Use != Knowledge.UnitUse.Hero || (open & (1 << (int)category)) == 0 || !view.Player.CanBuildUnit(card.Index)
                || unit.Cost > money)
            {
                continue;
            }

            var have = 0;
            foreach (var own in view.OwnUnits)
            {
                have += own.Archetype.Id == unit.Id ? 1 : 0;
            }

            foreach (var item in view.Player.Queue(category))
            {
                have += item.Kind == ProductionKind.Unit && item.DefIndex == card.Index ? 1 : 0;
            }

            if (have < unit.BuildLimit)
            {
                context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Unit, card.Index, 1));
                context.Log?.Add(context.Tick, context.Module, $"orders the hero {unit.Id}");
                money -= unit.Cost;
                open &= ~(1 << (int)category);
                return;
            }
        }
    }

    /// <summary>The share of every unit that can be built now, in hundredths of a percent of the army (10 000 the whole), sorted by index.</summary>
    internal IReadOnlyList<(int Unit, long Share)> Shares => _target;

    /// <summary>Works the target out into <see cref="Shares"/>; returns how many kinds are in it.</summary>
    internal int Target(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        _target.Clear();
        if (!rules.TryGetArmy(settings.Army, out var army) || army.Faction != view.Player.Faction.Id)
        {
            return 0;
        }

        if (!army.Roles.IsEmpty)
        {
            return Roles(context, army);
        }

        long whole = 0;
        foreach (var share in army.Shares)
        {
            var index = rules.IndexOfUnit(share.Unit);
            if (index >= 0 && view.Player.CanBuildUnit(index) && view.HasFactory(CategoryOf(rules.Units[index])))
            {
                // Rounded: 40 % is not exact in fixed point.
                var part = (share.Share.Raw * 10_000 + (1L << 15)) >> 16;
                _target.Add((index, part));
                whole += part;
            }
        }

        if (whole == 0)
        {
            _target.Clear();
            return 0;
        }

        Counter(rules, army, context.Memory.Estimate.Total, whole);
        _target.Sort((a, b) => a.Unit.CompareTo(b.Unit));
        if (context.Log is not null)
        {
            // For the debug window (M8 step 12), only while the journal is on.
            context.Plan.ArmyTarget = [.. _target.Select(share => (rules.Units[share.Unit].Id, share.Share))];
        }

        return _target.Count;
    }

    /// <summary>
    /// The target of an army of roles (M11), into <see cref="Shares"/>: the fleet gets <see cref="ArmyDef.Navy"/> of it on a map whose
    /// water leads to an enemy (<see cref="Knowledge.MapReading.NavalRoute"/>) — all of it when no land does — and the army on the
    /// ground and in the air the rest. Each share of a role (<see cref="RoleWeights"/>) goes to the units of that use the bot can build
    /// now, a factory for them standing, that are worth the most in it (<see cref="Knowledge.UnitCard.Worth"/>): the best three, by
    /// <see cref="RoleParts"/>, equal ones to the smaller index. A role no such unit fights keeps its share empty, as a kind not yet open
    /// does. Returns how many kinds there are.
    /// </summary>
    private int Roles(AiContext context, ArmyDef army)
    {
        var view = context.View;
        var map = context.Map;
        // With no way by land, all of it ships — but where the ferry can carry the army over (M11 step 3), the army as on any map.
        var navy = !map.NavalRoute ? 0L : map.LandRoute || Ferries(context) ? Part(army.Navy) : 10_000L;
        var weights = RoleWeights(context, army);
        var pads = view.FreePads();
        _byUnit.Clear();
        foreach (var (use, pool) in new[] { (Knowledge.UnitUse.Army, 10_000L - navy), (Knowledge.UnitUse.Fleet, navy) })
        {
            if (pool == 0)
            {
                continue;
            }

            for (var role = 1; role <= Knowledge.UnitCard.RoleCount; role++)
            {
                _candidates.Clear();
                foreach (var card in context.Units.Cards)
                {
                    // A jet only while a pad is free for it: no more are built than the pads (M11 step 3).
                    var unit = view.Rules.Database.Units[card.Index];
                    if (context.Units.PoolOf(card) == use && card.Worth[role - 1] > 0 && view.Player.CanBuildUnit(card.Index)
                        && view.HasFactory(CategoryOf(unit)) && !(IsJet(unit) && pads <= 0))
                    {
                        _candidates.Add(card);
                    }
                }

                var r = role - 1;
                _candidates.Sort((a, b) => a.Worth[r] != b.Worth[r] ? b.Worth[r].CompareTo(a.Worth[r]) : a.Index.CompareTo(b.Index));
                var taken = IntMath.Min(_candidates.Count, RoleParts.Length);
                var parts = 0;
                for (var i = 0; i < taken; i++)
                {
                    parts += RoleParts[i];
                }

                for (var i = 0; i < taken; i++)
                {
                    var share = pool * weights[r] / 10_000 * RoleParts[i] / parts;
                    _byUnit[_candidates[i].Index] = _byUnit.GetValueOrDefault(_candidates[i].Index) + share;
                }
            }
        }

        _target.Clear();
        foreach (var (unit, share) in _byUnit)
        {
            if (share > 0)
            {
                _target.Add((unit, share));
            }
        }

        if (context.Log is not null)
        {
            context.Plan.ArmyTarget = [.. _target.Select(share => (view.Rules.Database.Units[share.Unit].Id, share.Share))];
        }

        return _target.Count;
    }

    /// <summary>
    /// The shares of the roles of <paramref name="army"/>, in hundredths of a percent, by role (M11): <see cref="AiSettings.CounterPickPercent"/>
    /// of the shares against soldiers, vehicles and what flies goes to them as the enemy fields them (<see cref="EnemyPicture.Composition"/>:
    /// a ship counts as a vehicle); the share against buildings stays as it is. As they are with no enemy seen.
    /// </summary>
    private long[] RoleWeights(AiContext context, ArmyDef army)
    {
        var weights = new long[Knowledge.UnitCard.RoleCount];
        foreach (var role in army.Roles)
        {
            weights[(int)role.Role - 1] = Part(role.Share);
        }

        var rules = context.View.Rules.Database;
        var seen = new long[Knowledge.UnitCard.RoleCount];
        long total = 0;
        foreach (var (type, weight) in context.Memory.Estimate.Total.Composition)
        {
            var index = rules.IndexOfUnit(UnitTypes.IdOf(type));
            if (index >= 0)
            {
                seen[(int)Knowledge.UnitKnowledge.RoleAgainst(rules.Units[index]) - 1] += weight;
                total += weight;
            }
        }

        var pick = settings.CounterPickPercent;
        if (total == 0 || pick == 0)
        {
            return weights;
        }

        var fighting = weights[0] + weights[1] + weights[2];
        for (var r = 0; r < 3; r++)
        {
            weights[r] = (weights[r] * (100 - pick) + fighting * seen[r] / total * pick) / 100;
        }

        return weights;
    }

    /// <summary>Whether a ferry can take the army over the water: a landing at an enemy, and a transport of the faction.</summary>
    private static bool Ferries(AiContext context)
    {
        if (context.Map.Landings.Length == 0)
        {
            return false;
        }

        foreach (var card in context.Units.Cards)
        {
            if (card.Use == Knowledge.UnitUse.Transport && context.View.Rules.Database.Units[card.Index].Faction == context.View.Player.Faction.Id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="unit"/> is a jet that lands on a pad to load its ammo.</summary>
    private static bool IsJet(UnitDef unit) => unit.Category == UnitCategory.Aircraft && unit.Ammo > 0;

    /// <summary>A fraction as hundredths of a percent, rounded: 40 % is not exact in fixed point.</summary>
    private static long Part(Fixed fraction) => (fraction.Raw * 10_000 + (1L << 15)) >> 16;

    /// <summary>
    /// Picks <see cref="AiSettings.CounterPickPercent"/> of the army against <paramref name="enemy"/> (M8 step 7): of the whole of the
    /// target, <paramref name="whole"/>, the part of the infantry of the enemy goes to the units of <see cref="ArmyDef.AgainstInfantry"/> in
    /// the target, the part of its vehicles to those of <see cref="ArmyDef.AgainstVehicles"/>, each among them by their shares; a part with
    /// no unit of the target to take it stays with the army as it is. The target is then that much of this army against the enemy and the
    /// rest of the army as it is — the whole the same. Nothing with no enemy known or no share to pick.
    /// </summary>
    private void Counter(RulesDatabase rules, ArmyDef army, EnemyPicture enemy, long whole)
    {
        var seen = (long)enemy.Infantry + enemy.Vehicles;
        if (seen == 0 || settings.CounterPickPercent == 0)
        {
            return;
        }

        var (againstInfantry, againstVehicles) = (0L, 0L);
        foreach (var (unit, share) in _target)
        {
            againstInfantry += army.AgainstInfantry.Contains(rules.Units[unit].Id) ? share : 0;
            againstVehicles += army.AgainstVehicles.Contains(rules.Units[unit].Id) ? share : 0;
        }

        // The parts of the enemy no unit of the target answers stay with the army as it is.
        var (infantry, vehicles) = (againstInfantry > 0 ? enemy.Infantry : 0L, againstVehicles > 0 ? enemy.Vehicles : 0L);
        var asItIs = seen - infantry - vehicles;
        var pick = settings.CounterPickPercent;
        for (var i = 0; i < _target.Count; i++)
        {
            var (unit, share) = _target[i];
            var id = rules.Units[unit].Id;
            var counter = share * asItIs / seen
                + (army.AgainstInfantry.Contains(id) ? whole * infantry / seen * share / againstInfantry : 0)
                + (army.AgainstVehicles.Contains(id) ? whole * vehicles / seen * share / againstVehicles : 0);
            _target[i] = (unit, (share * (100 - pick) + counter * pick) / 100);
        }
    }

    /// <summary>
    /// The unit of the factory categories in <paramref name="open"/> (bits) short of its share by the most — or, as often as the level
    /// errs, one of the three shortest (M8 step 3); -1 for none.
    /// </summary>
    private int Shortest(AiContext context, RulesDatabase rules, int open, int army)
    {
        _short.Clear();
        foreach (var (unit, share) in _target)
        {
            if ((open & (1 << (int)CategoryOf(rules.Units[unit]))) == 0)
            {
                continue;
            }

            // In hundredths of a percent: what the share asks of the army with one more, less what there is. Only a kind short of it.
            var shortfall = share * (army + 1) - (long)_have[unit] * 10_000;
            if (shortfall > 0)
            {
                _short.Add((unit, shortfall, _short.Count));
            }
        }

        if (_short.Count == 0)
        {
            return -1;
        }

        // The shortest first; of two as short, the first of the army.
        _short.Sort((a, b) => a.Shortfall != b.Shortfall ? b.Shortfall.CompareTo(a.Shortfall) : a.Order.CompareTo(b.Order));
        return context.Choose(_short).Unit;
    }

    /// <summary>Counts the fighting units of the bot — those of the army and of the fleet (M11) — and those queued, by kind; returns the sum.</summary>
    private int CountArmy(AiWorldView view)
    {
        var rules = view.Rules.Database;
        if (_have.Length != rules.Units.Length)
        {
            _have = new int[rules.Units.Length];
        }

        System.Array.Clear(_have);
        var army = 0;
        var knowledge = Knowledge.UnitKnowledge.Of(view.Rules);
        foreach (var unit in view.OwnUnits)
        {
            if (knowledge.PoolOf(knowledge.Of(unit)) is not null && unit.Harvester is null)
            {
                var index = rules.IndexOfUnit(unit.Archetype.Id);
                _have[index]++;
                army++;
            }
        }

        foreach (var category in Factories)
        {
            foreach (var item in view.Player.Queue(category))
            {
                if (item.Kind == ProductionKind.Unit && knowledge.PoolOf(knowledge[item.DefIndex]) is not null)
                {
                    _have[item.DefIndex]++;
                    army++;
                }
            }
        }

        return army;
    }

    private static ProductionCategory CategoryOf(UnitDef unit) => unit.ProducedIn;

    /// <summary>What the queues still have to pay.</summary>
    private static int Promised(AiWorldView view)
    {
        var promised = 0;
        foreach (var category in Queues)
        {
            foreach (var item in view.Player.Queue(category))
            {
                promised += ProductionRules.CostOf(view.Rules.Database, item.Kind, item.DefIndex) - item.Paid;
            }
        }

        return promised;
    }
}
