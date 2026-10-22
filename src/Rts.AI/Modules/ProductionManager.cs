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
/// alone do not turn all the money into infantry before the war factory stands. «Medium» counters coarsely:
/// when the enemy armies it remembers are more than half infantry, <see cref="AiSettings.CounterShiftPercent"/> points move to the units
/// of <see cref="ArmyDef.AgainstInfantry"/>, when more than half vehicles to those of <see cref="ArmyDef.AgainstVehicles"/>, taken from the
/// rest by their shares. Among the factories whose queue holds fewer units than they keep (<see cref="AiSettings.InfantryQueued"/>,
/// <see cref="AiSettings.VehiclesQueued"/>), the kind that
/// falls shortest of its share — and short it must be: its share of the army with one more is above what there is — is ordered first,
/// the army counted with what is queued; one a factory a turn, equal ones to the smaller index; a kind that cannot be paid for yet is
/// saved for, not passed over for a cheaper one. It pays only from what is left when the
/// queues and the building the planner waits for (<see cref="AiPlan.NextBuilding"/>) are paid, and not at all when the strategist holds
/// the army back. A factory none of whose kinds is short waits: the mix of the data holds, and money that piles up goes to the second war
/// factory of the planner, which makes the tanks — the slowest part of the army — come faster. While the economy wants a harvester
/// (<see cref="AiPlan.HarvestersWanted"/>) the war factory is left to it.
/// </summary>
public sealed class ProductionManager(AiSettings settings) : IAiModule
{
    private static readonly ProductionCategory[] Factories = [ProductionCategory.Infantry, ProductionCategory.Vehicle];

    private static readonly ProductionCategory[] Queues =
        [ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Building, ProductionCategory.Defense];

    private readonly List<(int Unit, long Share)> _target = new();
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
            - (context.Plan.NextBuilding >= 0 ? rules.Buildings[context.Plan.NextBuilding].Cost : 0);
        var army = CountArmy(view);
        var open = 0;
        foreach (var category in Factories)
        {
            // While the economy wants a harvester the war factory is left to it: a queue runs first come, first served.
            var queued = category == ProductionCategory.Infantry ? settings.InfantryQueued : settings.VehiclesQueued;
            if (view.Player.Queue(category).Count < queued && view.HasFactory(category)
                && !(category == ProductionCategory.Vehicle && context.Plan.HarvestersWanted))
            {
                open |= 1 << (int)category;
            }
        }

        // The kind that falls shortest of its share comes first, whichever factory makes it; when it cannot be paid for yet, the money
        // is saved for it rather than spent on a cheaper kind, which would tilt the army.
        while (open != 0)
        {
            var unit = Shortest(rules, open, army);
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

        var counters = EnemyMostly(context.Memory) switch
        {
            UnitCategory.Infantry => army.AgainstInfantry,
            UnitCategory.Vehicle => army.AgainstVehicles,
            _ => default,
        };
        if (!counters.IsDefaultOrEmpty)
        {
            Shift(rules, counters);
        }

        _target.Sort((a, b) => a.Unit.CompareTo(b.Unit));
        return _target.Count;
    }

    /// <summary>
    /// Moves <see cref="AiSettings.CounterShiftPercent"/> points to the units of <paramref name="counters"/> in the target, by their shares,
    /// taken from the others by theirs; nothing when either side is empty.
    /// </summary>
    private void Shift(RulesDatabase rules, System.Collections.Immutable.ImmutableArray<string> counters)
    {
        long favoured = 0;
        long others = 0;
        foreach (var (unit, share) in _target)
        {
            if (counters.Contains(rules.Units[unit].Id))
            {
                favoured += share;
            }
            else
            {
                others += share;
            }
        }

        if (favoured == 0 || others == 0)
        {
            return;
        }

        var shift = IntMath.Min(settings.CounterShiftPercent * 100, (int)others);
        for (var i = 0; i < _target.Count; i++)
        {
            var (unit, share) = _target[i];
            _target[i] = (unit, counters.Contains(rules.Units[unit].Id) ? share + shift * share / favoured : share - shift * share / others);
        }
    }

    /// <summary>Infantry when more than half of the enemy units remembered are infantry, vehicles when more than half are not; else null.</summary>
    internal static UnitCategory? EnemyMostly(AiMemory memory)
    {
        var (units, infantry) = (0, 0);
        foreach (var army in memory.Armies)
        {
            units += army.Units;
            infantry += army.Infantry;
        }

        return units == 0 ? null : infantry * 2 > units ? UnitCategory.Infantry : (units - infantry) * 2 > units ? UnitCategory.Vehicle : null;
    }

    /// <summary>The unit of the factory categories in <paramref name="open"/> (bits) short of its share by the most; -1 for none.</summary>
    private int Shortest(RulesDatabase rules, int open, int army)
    {
        var best = -1;
        var bestShortfall = long.MinValue;
        foreach (var (unit, share) in _target)
        {
            if ((open & (1 << (int)CategoryOf(rules.Units[unit]))) == 0)
            {
                continue;
            }

            // In hundredths of a percent: what the share asks of the army with one more, less what there is. Only a kind short of it.
            var shortfall = share * (army + 1) - (long)_have[unit] * 10_000;
            if (shortfall > 0 && shortfall > bestShortfall)
            {
                (best, bestShortfall) = (unit, shortfall);
            }
        }

        return best;
    }

    /// <summary>Counts the fighting units of the bot — those with a weapon, not the harvesters — and those queued, by kind; returns the sum.</summary>
    private int CountArmy(AiWorldView view)
    {
        var rules = view.Rules.Database;
        if (_have.Length != rules.Units.Length)
        {
            _have = new int[rules.Units.Length];
        }

        System.Array.Clear(_have);
        var army = 0;
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Archetype.Armed && unit.Harvester is null)
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
                if (item.Kind == ProductionKind.Unit && rules.Units[item.DefIndex].Primary.Length > 0 && rules.Units[item.DefIndex].HarvestCapacity == 0)
                {
                    _have[item.DefIndex]++;
                    army++;
                }
            }
        }

        return army;
    }

    private static ProductionCategory CategoryOf(UnitDef unit) =>
        unit.Category == UnitCategory.Infantry ? ProductionCategory.Infantry : ProductionCategory.Vehicle;

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
