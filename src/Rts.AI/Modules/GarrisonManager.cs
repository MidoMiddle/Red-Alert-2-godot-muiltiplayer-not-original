using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.AI.Squads;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The garrisons of a bot (M11 step 3, the bot that knows every weapon): soldiers who may sit in a building (RA2 <c>Occupier</c>: the GI,
/// the conscript) hold the civilian buildings of the map by its base, which shoot from them at what comes.
/// <list type="bullet">
/// <item>The buildings are the civilian ones in sight (<see cref="Rts.AI.View.AiWorldView.MapBuildingsInSight"/>) the bot may hold —
/// nobody's or its own, not about to fall — within <see cref="ReachCells"/> of a building of its base, the nearest to its start first;
/// <see cref="MaxBuildings"/> of them.</item>
/// <item>Up to <see cref="MaxSoldiers"/> soldiers in them and on their way, from those at home — in no squad, or in the squad that
/// gathers — the nearest first. On the way a soldier is on an errand (<see cref="AiPlan.Errands"/>): the squads leave it alone.</item>
/// </list>
/// Not for the simple build orders of «Easy».
/// </summary>
public sealed class GarrisonManager(AiSettings settings) : IAiModule
{
    /// <summary>How far from a building of the base a civilian building is held, in cells between them.</summary>
    public const int ReachCells = 8;

    /// <summary>How many buildings are held.</summary>
    public const int MaxBuildings = 2;

    /// <summary>How many soldiers sit in them, those on the way counted.</summary>
    public const int MaxSoldiers = 6;

    private readonly SortedDictionary<int, EntityId> _walking = new();
    private readonly List<int> _gone = new();
    private readonly List<Building> _held = new();
    private readonly HashSet<int> _away = new();

    public AiModuleKind Kind => AiModuleKind.Garrisons;

    public void Update(AiContext context)
    {
        if (settings.Quality == Profiles.BuildOrderQuality.Simple)
        {
            return;
        }

        var view = context.View;
        Forget(context);
        Buildings(context);
        if (_held.Count == 0)
        {
            return;
        }

        var soldiers = _walking.Count;
        foreach (var building in _held)
        {
            soldiers += building.Owner == view.Me ? building.Occupants.Count : 0;
        }

        // The units of the squads that are out are not at home.
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

        foreach (var building in _held)
        {
            var room = GarrisonSystem.Capacity(building) - building.Occupants.Count - Walking(building.Id);
            var batch = ImmutableArray.CreateBuilder<EntityId>();
            while (room > 0 && soldiers < MaxSoldiers && Nearest(context, building) is { } soldier)
            {
                batch.Add(soldier.Id);
                _walking[soldier.Id.Value] = building.Id;
                context.Plan.Errands.Add(soldier.Id.Value);
                (room, soldiers) = (room - 1, soldiers + 1);
            }

            if (batch.Count > 0)
            {
                context.Issue(new EnterCommand(view.Me, context.ExecuteTick, context.NextSequence(), batch.ToImmutable(), building.Id));
                context.Log?.Add(context.Tick, context.Module, $"sends {batch.Count} into {building.Kind} at {building.Cell.X},{building.Cell.Y}");
            }
        }
    }

    /// <summary>The soldiers who went in, fell or stopped on the way are off their errand.</summary>
    private void Forget(AiContext context)
    {
        _gone.Clear();
        foreach (var (id, building) in _walking)
        {
            var going = false;
            foreach (var unit in context.View.OwnUnits)
            {
                going |= unit.Id.Value == id && unit.EnterTarget == building;
            }

            if (!going)
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _walking.Remove(id);
            context.Plan.Errands.Remove(id);
        }
    }

    /// <summary>The civilian buildings to hold, into <see cref="_held"/>.</summary>
    private void Buildings(AiContext context)
    {
        var view = context.View;
        _held.Clear();
        if (view.Start is not { } start)
        {
            return;
        }

        foreach (var building in view.MapBuildingsInSight)
        {
            if (GarrisonSystem.Civilian(building) && GarrisonSystem.Holds(building, view.Me) && ByTheBase(context, building))
            {
                _held.Add(building);
            }
        }

        _held.Sort((a, b) => Distance(a.Cell, start) != Distance(b.Cell, start) ? Distance(a.Cell, start).CompareTo(Distance(b.Cell, start))
            : a.Id.Value.CompareTo(b.Id.Value));
        if (_held.Count > MaxBuildings)
        {
            _held.RemoveRange(MaxBuildings, _held.Count - MaxBuildings);
        }
    }

    private static bool ByTheBase(AiContext context, Building civilian)
    {
        foreach (var own in context.View.OwnBuildings)
        {
            if (own.Def is null)
            {
                continue;
            }

            var gapX = IntMath.Max(0, IntMath.Max(own.Cell.X - civilian.FarCell.X, civilian.Cell.X - own.FarCell.X) - 1);
            var gapY = IntMath.Max(0, IntMath.Max(own.Cell.Y - civilian.FarCell.Y, civilian.Cell.Y - own.FarCell.Y) - 1);
            if (IntMath.Max(gapX, gapY) <= ReachCells)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The soldier at home nearest <paramref name="building"/> who may sit in it and is on no errand; null for none.</summary>
    private Unit? Nearest(AiContext context, Building building)
    {
        Unit? best = null;
        var bestDistance = int.MaxValue;
        foreach (var unit in context.View.OwnUnits)
        {
            var distance = Distance(unit.Position.ToCell(), building.Cell);
            if (distance < bestDistance && context.Units.InArmy(unit) && GarrisonSystem.CanEnter(unit) && unit.Id != context.Plan.Scout
                && !context.Plan.Errands.Contains(unit.Id.Value) && !_away.Contains(unit.Id.Value))
            {
                (best, bestDistance) = (unit, distance);
            }
        }

        return best;
    }

    private int Walking(EntityId building)
    {
        var count = 0;
        foreach (var target in _walking.Values)
        {
            count += target == building ? 1 : 0;
        }

        return count;
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
