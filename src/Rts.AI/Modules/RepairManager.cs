using System.Collections.Generic;
using Rts.AI.Squads;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The repairs of a bot (M11, before the tuning): its hurt buildings and vehicles are mended, as a person mends them.
/// <list type="bullet">
/// <item><b>Buildings</b>: one below <see cref="BuildingPercent"/> of its hit points has its repair turned on — once the money left after
/// the queues is <see cref="Reserve"/> or more, or at once below <see cref="CriticalPercent"/>. The rules turn it off at full hit points;
/// without the credits it waits. Not a wall: the rules mend none.</item>
/// <item><b>Vehicles and ships</b> below <see cref="UnitPercent"/> at home — in no squad that is out, not the scout, on no errand, not
/// deployed — go beside the nearest workshop of their kind (the service depot for vehicles, the shipyard for ships), one to a workshop,
/// and stay on an errand (<see cref="AiPlan.Errands"/>) till whole, or <see cref="MendTicks"/> at most; the squads then take them back.</item>
/// </list>
/// </summary>
public sealed class RepairManager : IAiModule
{
    /// <summary>A building below this share of its hit points is repaired, in percent.</summary>
    public const int BuildingPercent = 80;

    /// <summary>A building below this share is repaired whatever the money.</summary>
    public const int CriticalPercent = 35;

    /// <summary>The credits left after the queues below which a building not in danger waits for its repair.</summary>
    public const int Reserve = 300;

    /// <summary>A vehicle or a ship below this share of its hit points goes to a workshop, in percent.</summary>
    public const int UnitPercent = 50;

    /// <summary>A unit not whole this long after it was sent, in ticks (90 s), is let go: it did not get beside the workshop.</summary>
    public const int MendTicks = 1350;

    /// <summary>A repair turned on is not turned on again for this many ticks: the order may wait in the budget.</summary>
    public const int ToggleTicks = 60;

    private readonly SortedDictionary<int, (EntityId Workshop, int Since)> _mending = new();
    private readonly SortedDictionary<int, int> _toggled = new();
    private readonly List<int> _done = new();
    private readonly HashSet<int> _away = new();

    public AiModuleKind Kind => AiModuleKind.Repairs;

    public void Update(AiContext context)
    {
        Buildings(context);
        Release(context);
        Vehicles(context);
    }

    private void Buildings(AiContext context)
    {
        var view = context.View;
        var spare = view.SpareCredits();
        foreach (var building in view.OwnBuildings)
        {
            if (building.Repairing || !building.IsReady
                || (_toggled.TryGetValue(building.Id.Value, out var at) && context.Tick - at < ToggleTicks) || building.Hp * 100 >= building.MaxHp * BuildingPercent
                || building.Def is { Wall: true } || (building.Def is null && !RepairSystem.Repairable(building)))
            {
                continue;
            }

            if (spare >= Reserve || building.Hp * 100 < building.MaxHp * CriticalPercent)
            {
                _toggled[building.Id.Value] = context.Tick;
                context.Issue(new ToggleRepairCommand(view.Me, context.ExecuteTick, context.NextSequence(), building.Id));
                context.Log?.Add(context.Tick, context.Module, $"repairs {building.Def?.Id ?? building.Kind.ToString()} at {building.HpPercent()}%");
            }
        }
    }

    /// <summary>The units mended whole, gone, or whose workshop fell are off their errand.</summary>
    private void Release(AiContext context)
    {
        var view = context.View;
        _done.Clear();
        foreach (var (id, (workshop, since)) in _mending)
        {
            Unit? unit = null;
            foreach (var own in view.OwnUnits)
            {
                unit = own.Id.Value == id ? own : unit;
            }

            var standing = false;
            foreach (var building in view.OwnBuildings)
            {
                standing |= building.Id == workshop;
            }

            if (unit is null || unit.Hp >= unit.MaxHp || !standing || context.Tick - since >= MendTicks)
            {
                _done.Add(id);
            }
        }

        foreach (var id in _done)
        {
            _mending.Remove(id);
            context.Plan.Errands.Remove(id);
        }
    }

    private void Vehicles(AiContext context)
    {
        var view = context.View;
        _away.Clear();
        foreach (var squad in context.Plan.Squads)
        {
            if (squad.State is not (SquadState.Gathering or SquadState.Ready))
            {
                foreach (var id in squad.Units)
                {
                    _away.Add(id.Value);
                }
            }
        }

        foreach (var unit in view.OwnUnits)
        {
            var category = unit.Archetype.Category;
            if (category is not (UnitCategory.Vehicle or UnitCategory.Naval) || unit.Harvester is not null || unit.Hp * 100 >= unit.MaxHp * UnitPercent
                || unit.Deployed || unit.Id == context.Plan.Scout || context.Plan.Errands.Contains(unit.Id.Value) || _away.Contains(unit.Id.Value)
                || Workshop(context, unit) is not { } workshop || BusyWith(workshop.Id))
            {
                continue;
            }

            if (Beside(context, workshop, unit) is not { } cell)
            {
                continue;
            }

            _mending[unit.Id.Value] = (workshop.Id, context.Tick);
            context.Plan.Errands.Add(unit.Id.Value);
            context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [unit.Id], cell.Center));
            context.Log?.Add(context.Tick, context.Module, $"sends {unit.Archetype.Id} at {unit.HpPercent()}% to {workshop.Def?.Id} for repairs");
        }
    }

    /// <summary>The nearest ready workshop of the bot for <paramref name="unit"/>'s kind: on the water for a ship, on land for a vehicle.</summary>
    private static Building? Workshop(AiContext context, Unit unit)
    {
        Building? best = null;
        var bestDistance = int.MaxValue;
        var here = unit.Position.ToCell();
        foreach (var building in context.View.OwnBuildings)
        {
            if (building is not { IsReady: true, Def: { RepairsVehicles: true } def } || def.Water != (unit.Archetype.Category == UnitCategory.Naval))
            {
                continue;
            }

            var distance = Distance(here, building.Cell);
            if (distance < bestDistance)
            {
                (best, bestDistance) = (building, distance);
            }
        }

        return best;
    }

    private bool BusyWith(EntityId workshop)
    {
        foreach (var (target, _) in _mending.Values)
        {
            if (target == workshop)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A cell next to the footprint of <paramref name="workshop"/> the unit can stand on, the nearest it first; null for none.</summary>
    private static CellPos? Beside(AiContext context, Building workshop, Unit unit)
    {
        var view = context.View;
        var locomotor = unit.Archetype.Locomotor;
        var here = unit.Position.ToCell();
        CellPos? best = null;
        var bestDistance = int.MaxValue;
        for (var y = workshop.Cell.Y - 1; y <= workshop.FarCell.Y + 1; y++)
        {
            for (var x = workshop.Cell.X - 1; x <= workshop.FarCell.X + 1; x++)
            {
                var cell = new CellPos(x, y);
                var ring = x < workshop.Cell.X || x > workshop.FarCell.X || y < workshop.Cell.Y || y > workshop.FarCell.Y;
                if (ring && view.CanEnter(cell, locomotor) && view.IsExplored(cell) && Distance(here, cell) < bestDistance)
                {
                    (best, bestDistance) = (cell, Distance(here, cell));
                }
            }
        }

        return best;
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}

internal static class HitPoints
{
    /// <summary>The share of its hit points a building has, in percent, for the journal.</summary>
    public static int HpPercent(this Building building) => building.MaxHp > 0 ? building.Hp * 100 / building.MaxHp : 0;

    /// <summary>The share of its hit points a unit has, in percent, for the journal.</summary>
    public static int HpPercent(this Unit unit) => unit.MaxHp > 0 ? unit.Hp * 100 / unit.MaxHp : 0;
}
