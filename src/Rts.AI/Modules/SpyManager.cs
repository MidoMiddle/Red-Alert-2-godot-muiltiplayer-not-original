using System.Collections.Generic;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The spies of a bot (M11 step 3, the bot that knows every weapon): an infiltrator (<see cref="Knowledge.UnitUse.Spy"/>) goes into the
/// enemy building that gives the most (<see cref="BuildingDef.SpyEffect"/>), as the enemy's own soldier.
/// <list type="bullet">
/// <item>What a building gives, the most first (<see cref="Gain"/>): the technology of a battle lab of a faction not stolen from yet; the
/// veterans of a war factory, of barracks, while the bot's own do not come out veterans yet; half the credits of a refinery; a
/// superweapon's charge from the start; a blackout; the map forgotten. Of buildings that give as much the nearest the start.</item>
/// <item>The building is one the bot remembers (<see cref="AiMemory.Buildings"/>).</item>
/// <item>With a target and no spy, one is ordered from the barracks once the army is as big as a squad that may set out
/// (<see cref="AiSettings.MinAttackArmy"/>) and the money left after the queues covers it; one at a time; one lost — the next after
/// <see cref="RetryTicks"/>.</item>
/// </list>
/// Not for the simple build orders of «Easy». A spy is not given the order it has and still carries out.
/// </summary>
public sealed class SpyManager(AiSettings settings) : IAiModule
{
    /// <summary>How long after a spy was lost the next is ordered, in ticks: 2 minutes.</summary>
    public const int RetryTicks = 1800;

    private readonly List<Unit> _spies = new();
    private EntityId? _sent;
    private int _retryAt;

    public AiModuleKind Kind => AiModuleKind.Spies;

    public void Update(AiContext context)
    {
        if (settings.Quality == Profiles.BuildOrderQuality.Simple)
        {
            return;
        }

        var view = context.View;
        _spies.Clear();
        foreach (var unit in view.OwnUnits)
        {
            if (context.Units.Of(unit).Use == Knowledge.UnitUse.Spy)
            {
                _spies.Add(unit);
            }
        }

        // The spy sent is gone: in the building, or lost; the next waits either way — a building given once gives again later at most.
        if (_sent is { } sent && !_spies.Exists(spy => spy.Id == sent))
        {
            (_sent, _retryAt) = (null, context.Tick + RetryTicks);
        }

        if (Target(context) is not { } target)
        {
            return;
        }

        if (_spies.Count == 0)
        {
            Order(context);
            return;
        }

        foreach (var spy in _spies)
        {
            if (spy.EnterTarget == target.Id)
            {
                continue;
            }

            context.Issue(new EnterCommand(view.Me, context.ExecuteTick, context.NextSequence(), [spy.Id], target.Id));
            context.Log?.Add(context.Tick, context.Module, $"sends {spy.Archetype.Id} into {view.Rules.Database.Buildings[target.DefIndex].Id}");
            _sent = spy.Id;
            return;
        }
    }

    /// <summary>The enemy building that gives the most to a spy, the nearest the start of as many; null for none worth it.</summary>
    internal static RememberedBuilding? Target(AiContext context)
    {
        var view = context.View;
        var from = view.Start ?? new CellPos(view.Width / 2, view.Height / 2);
        RememberedBuilding? best = null;
        var (bestGain, bestDistance) = (0, int.MaxValue);
        foreach (var building in context.Memory.Buildings)
        {
            if (building.DefIndex < 0)
            {
                continue;
            }

            var gain = Gain(view, view.Rules.Database.Buildings[building.DefIndex]);
            var distance = IntMath.Max(IntMath.Abs(building.Cell.X - from.X), IntMath.Abs(building.Cell.Y - from.Y));
            if (gain > bestGain || (gain == bestGain && gain > 0 && distance < bestDistance))
            {
                (best, bestGain, bestDistance) = (building, gain, distance);
            }
        }

        return best;
    }

    /// <summary>What going into a building of <paramref name="def"/> gives the bot, the more the better; 0 for nothing.</summary>
    public static int Gain(AiWorldView view, BuildingDef def) => def.SpyEffect switch
    {
        SpyEffect.StealTech => view.Player.StolenTech.Contains(def.Faction) ? 0 : 7,
        SpyEffect.VeteranVehicles => view.Player.VeteranVehicles ? 0 : 6,
        SpyEffect.StealCredits => 5,
        SpyEffect.VeteranInfantry => view.Player.VeteranInfantry ? 0 : 4,
        SpyEffect.ResetCharge => 3,
        SpyEffect.Blackout => 2,
        SpyEffect.ResetMap => 1,
        _ => 0,
    };

    /// <summary>A spy from the barracks, unless one is queued, the last went lately, the army is small or the money is not there.</summary>
    private void Order(AiContext context)
    {
        var view = context.View;
        if (context.Tick < _retryAt || !view.HasFactory(ProductionCategory.Infantry))
        {
            return;
        }

        var army = 0;
        foreach (var unit in view.OwnUnits)
        {
            army += context.Units.InArmy(unit) ? 1 : 0;
        }

        foreach (var item in view.Player.Queue(ProductionCategory.Infantry))
        {
            if (item.Kind == ProductionKind.Unit && context.Units[item.DefIndex].Use == Knowledge.UnitUse.Spy)
            {
                return;
            }
        }

        if (army < settings.MinAttackArmy)
        {
            return;
        }

        foreach (var card in context.Units.Cards)
        {
            var def = view.Rules.Database.Units[card.Index];
            if (card.Use == Knowledge.UnitUse.Spy && view.Player.CanBuildUnit(card.Index) && def.Cost <= view.SpareCredits())
            {
                context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Unit, card.Index, 1));
                context.Log?.Add(context.Tick, context.Module, $"orders {def.Id} for the enemy's buildings");
                return;
            }
        }
    }
}
