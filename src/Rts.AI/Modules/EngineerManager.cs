using System.Collections.Generic;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The engineers of a bot (M11 step 3, the bot that knows every weapon): they take the tech buildings of the map — the oil derrick, the
/// hospital, the airfield, the outpost — that nobody holds.
/// <list type="bullet">
/// <item>The target is the nearest such building to the start the bot has seen (<see cref="AiMemory.MapBuildings"/>), within
/// <see cref="CaptureCells"/> of it, that no armed enemy the bot sees or remembers stands within <see cref="GuardCells"/> of.</item>
/// <item>With a target and no engineer, one is ordered from the barracks — of the faction's engineers the first it may build — once the
/// money left after the queues covers it; one at a time. An engineer lost waits <see cref="RetryTicks"/> before the next.</item>
/// <item>An engineer is sent into the target (<see cref="EnterCommand"/>); once the building is the bot's, it looks for the next.</item>
/// </list>
/// Not for the simple build orders of «Easy». An engineer is not given the order it has and still carries out.
/// </summary>
public sealed class EngineerManager(AiSettings settings) : IAiModule
{
    /// <summary>How far from the start a building is worth taking, in cells.</summary>
    public const int CaptureCells = 40;

    /// <summary>An armed enemy this near a building, in cells, guards it.</summary>
    public const int GuardCells = 6;

    /// <summary>How long after an engineer was lost the next is ordered, in ticks: 90 s.</summary>
    public const int RetryTicks = 1350;

    private readonly List<Unit> _engineers = new();
    private int _retryAt;
    private (EntityId Engineer, EntityId Building)? _sent;

    public AiModuleKind Kind => AiModuleKind.Engineers;

    public void Update(AiContext context)
    {
        if (settings.Quality == Profiles.BuildOrderQuality.Simple)
        {
            return;
        }

        var view = context.View;
        _engineers.Clear();
        foreach (var unit in view.OwnUnits)
        {
            if (context.Units.Of(unit).Use == Knowledge.UnitUse.Engineer)
            {
                _engineers.Add(unit);
            }
        }

        // The engineer sent is gone: in the building, or — the building not the bot's — lost on the way.
        if (_sent is { } sent && !_engineers.Exists(unit => unit.Id == sent.Engineer))
        {
            _sent = null;
            var taken = false;
            foreach (var building in context.Memory.MapBuildings)
            {
                taken |= building.Id == sent.Building && building.Owner == view.Me;
            }

            _retryAt = taken ? 0 : context.Tick + RetryTicks;
        }

        if (Target(context) is not { } target)
        {
            return;
        }

        if (_engineers.Count == 0)
        {
            Order(context);
            return;
        }

        foreach (var engineer in _engineers)
        {
            if (engineer.EnterTarget == target.Id)
            {
                continue;
            }

            context.Issue(new EnterCommand(view.Me, context.ExecuteTick, context.NextSequence(), [engineer.Id], target.Id));
            context.Log?.Add(context.Tick, context.Module, $"sends {engineer.Archetype.Id} to take {target.Kind} at {target.Cell.X},{target.Cell.Y}");
            _sent = (engineer.Id, target.Id);
            return;
        }
    }

    /// <summary>The tech building to take: nobody's, seen, near the start, not guarded; the nearest first; null for none.</summary>
    internal static RememberedMapBuilding? Target(AiContext context)
    {
        var view = context.View;
        if (view.Start is not { } start)
        {
            return null;
        }

        RememberedMapBuilding? best = null;
        var bestDistance = int.MaxValue;
        foreach (var building in context.Memory.MapBuildings)
        {
            var distance = Distance(building.Cell, start);
            if (!building.Owner.IsNeutral || !context.View.Rules.MapTypes.IsKnown(building.Kind) || !context.View.Rules.MapTypes.Get(building.Kind).Capturable
                || building.Kind == BuildingKind.TechBridgeRepairHut || distance > CaptureCells || distance >= bestDistance || Guarded(context, building))
            {
                continue;
            }

            (best, bestDistance) = (building, distance);
        }

        return best;
    }

    private static bool Guarded(AiContext context, RememberedMapBuilding building)
    {
        foreach (var enemy in context.View.VisibleEnemies)
        {
            if (enemy is Unit { Archetype.Armed: true, Harvester: null } unit && Near(building, unit.Position.ToCell()))
            {
                return true;
            }
        }

        foreach (var army in context.Memory.Armies)
        {
            if (army.Armed > 0 && Near(building, army.Centre.ToCell()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Near(RememberedMapBuilding building, CellPos cell) =>
        cell.X >= building.Cell.X - GuardCells && cell.X <= building.FarCell.X + GuardCells
        && cell.Y >= building.Cell.Y - GuardCells && cell.Y <= building.FarCell.Y + GuardCells;

    /// <summary>An engineer from the barracks, unless one is queued, the last was lost lately or the money is not there.</summary>
    private void Order(AiContext context)
    {
        var view = context.View;
        if (context.Tick < _retryAt || !view.HasFactory(ProductionCategory.Infantry))
        {
            return;
        }

        foreach (var item in view.Player.Queue(ProductionCategory.Infantry))
        {
            if (item.Kind == ProductionKind.Unit && context.Units[item.DefIndex].Use == Knowledge.UnitUse.Engineer)
            {
                return;
            }
        }

        foreach (var card in context.Units.Cards)
        {
            var def = view.Rules.Database.Units[card.Index];
            if (card.Use == Knowledge.UnitUse.Engineer && view.Player.CanBuildUnit(card.Index) && def.Cost <= view.SpareCredits())
            {
                context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Unit, card.Index, 1));
                context.Log?.Add(context.Tick, context.Module, $"orders {def.Id} to take a tech building");
                return;
            }
        }
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
