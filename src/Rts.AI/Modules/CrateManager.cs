using System.Collections.Generic;
using Rts.AI.Squads;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The crates of a bot (M11 a6, before the tuning): a crate in sight is taken by the nearest unit of the army at home that can — on the
/// ground, not a ship nor a flier, in no squad that is out, not the scout, on no errand, not deployed — within <see cref="ReachCells"/>
/// of it, unless an armed enemy in sight stands within <see cref="GuardCells"/> of the crate. One unit a crate; on the way it is on an
/// errand (<see cref="AiPlan.Errands"/>), off it when the crate is gone, it fell, or <see cref="GiveUpTicks"/> passed.
/// </summary>
public sealed class CrateManager : IAiModule
{
    /// <summary>How far a unit goes for a crate, in cells.</summary>
    public const int ReachCells = 16;

    /// <summary>An armed enemy this near a crate, in cells, keeps the bot's units from it.</summary>
    public const int GuardCells = 6;

    /// <summary>How long a unit goes for a crate before it gives up, in ticks: 40 s.</summary>
    public const int GiveUpTicks = 600;

    private readonly SortedDictionary<int, (CellPos Crate, int Since)> _going = new();
    private readonly List<CellPos> _crates = new();
    private readonly List<int> _done = new();
    private readonly HashSet<int> _away = new();

    public AiModuleKind Kind => AiModuleKind.Crates;

    public void Update(AiContext context)
    {
        var view = context.View;
        _crates.Clear();
        _crates.AddRange(view.CratesInSight());
        Release(context);
        if (_crates.Count == 0)
        {
            return;
        }

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

        foreach (var crate in _crates)
        {
            if (Claimed(crate) || Guarded(context, crate) || Nearest(context, crate) is not { } unit)
            {
                continue;
            }

            _going[unit.Id.Value] = (crate, context.Tick);
            context.Plan.Errands.Add(unit.Id.Value);
            context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [unit.Id], crate.Center));
            context.Log?.Add(context.Tick, context.Module, $"sends {unit.Archetype.Id} for the crate at {crate.X},{crate.Y}");
        }
    }

    private void Release(AiContext context)
    {
        _done.Clear();
        foreach (var (id, (crate, since)) in _going)
        {
            var alive = false;
            foreach (var unit in context.View.OwnUnits)
            {
                alive |= unit.Id.Value == id;
            }

            if (!alive || !_crates.Contains(crate) || context.Tick - since >= GiveUpTicks)
            {
                _done.Add(id);
            }
        }

        foreach (var id in _done)
        {
            _going.Remove(id);
            context.Plan.Errands.Remove(id);
        }
    }

    private bool Claimed(CellPos crate)
    {
        foreach (var (target, _) in _going.Values)
        {
            if (target == crate)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Guarded(AiContext context, CellPos crate)
    {
        foreach (var enemy in context.View.VisibleEnemies)
        {
            if (enemy is Unit { Archetype.Armed: true, Harvester: null } unit && Distance(unit.Position.ToCell(), crate) <= GuardCells)
            {
                return true;
            }

            if (enemy is Building { Weapons.Length: > 0 } building && Distance(building.Cell, crate) <= GuardCells)
            {
                return true;
            }
        }

        return false;
    }

    private Unit? Nearest(AiContext context, CellPos crate)
    {
        Unit? best = null;
        var bestDistance = ReachCells + 1;
        foreach (var unit in context.View.OwnUnits)
        {
            var distance = Distance(unit.Position.ToCell(), crate);
            if (distance < bestDistance && context.Units.InArmy(unit) && !unit.Archetype.Flies && !unit.Deployed
                && unit.Id != context.Plan.Scout && !context.Plan.Errands.Contains(unit.Id.Value) && !_away.Contains(unit.Id.Value))
            {
                (best, bestDistance) = (unit, distance);
            }
        }

        return best;
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
