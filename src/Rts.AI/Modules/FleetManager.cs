using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The fleet of a bot (M11, the bot that knows every weapon): its ships of <see cref="Knowledge.UnitUse.Fleet"/>, apart from the squads,
/// which go by land. The ships wait at the exit of the shipyard — at the harbour of the map reading while there is none — and set out
/// together once there are <see cref="Size"/> of them and the plan lets the army attack (<see cref="AiPlan.AttackAllowed"/>), not before
/// the first attack of the bot may come (<see cref="AiSettings.FirstAttackFrom"/>): an attack-move to the enemy shore of the map reading
/// nearest the likely enemy (<see cref="Knowledge.MapReading.ShoreNear"/>), where they fire at what they reach. Below
/// <see cref="AiSettings.RetreatAtStrengthPercent"/> of the strength they set out with, or with no shore left to go to, they sail home
/// and gather again. A ship is not given the order it has and still carries out.
/// </summary>
public sealed class FleetManager(AiSettings settings) : IAiModule
{
    /// <summary>A ship this near its point, in cells, is there.</summary>
    public const int ArrivedCells = 4;

    /// <summary>The fewest ships that set out.</summary>
    public const int MinShips = 3;

    private readonly SortedDictionary<int, (bool Attack, CellPos Cell)> _orders = new();
    private readonly List<Unit> _ships = new();
    private readonly List<EntityId> _batch = new();
    private readonly List<int> _gone = new();
    private int _setOutWith;

    public AiModuleKind Kind => AiModuleKind.Fleet;

    /// <summary>Whether the fleet is out at the enemy.</summary>
    internal bool Out { get; private set; }

    /// <summary>The ships that set out together: half the units of the first squad, <see cref="MinShips"/> at least.</summary>
    public int Size => IntMath.Max(MinShips, settings.MinAttackArmy / 2);

    public void Update(AiContext context)
    {
        var view = context.View;
        _ships.Clear();
        var strength = 0;
        foreach (var unit in view.OwnUnits)
        {
            if (context.Units.InFleet(unit) && !context.Plan.Errands.Contains(unit.Id.Value))
            {
                _ships.Add(unit);
                strength += context.Memory.StrengthOf(unit);
            }
        }

        Forget();
        if (_ships.Count == 0)
        {
            Out = false;
            return;
        }

        if (Home(context) is not { } home)
        {
            return;
        }

        var shore = context.Map.ShoreNear(context.Memory.LikelyEnemyCell() ?? home);
        if (!Out && shore is not null && _ships.Count >= Size && context.Plan.AttackAllowed && context.Tick >= settings.FirstAttackFrom)
        {
            (Out, _setOutWith) = (true, strength);
            context.Log?.Add(context.Tick, context.Module, $"the fleet of {_ships.Count} sets out for {shore.Value.X},{shore.Value.Y}");
        }
        else if (Out && (shore is null || (long)strength * 100 < (long)_setOutWith * settings.RetreatAtStrengthPercent))
        {
            Out = false;
            context.Log?.Add(context.Tick, context.Module, $"the fleet turns home with {_ships.Count}");
        }

        var (attack, point) = Out ? (true, shore!.Value) : (false, home);
        _batch.Clear();
        foreach (var ship in _ships)
        {
            if (_orders.TryGetValue(ship.Id.Value, out var last) && last == (attack, point)
                && (ship.MoveTarget is not null || ship.Engaged || Distance(ship.Position.ToCell(), point) <= ArrivedCells))
            {
                continue;
            }

            _orders[ship.Id.Value] = (attack, point);
            _batch.Add(ship.Id);
        }

        if (_batch.Count > 0)
        {
            var (me, at, units) = (view.Me, context.ExecuteTick, ImmutableArray.CreateRange(_batch));
            context.Issue(attack
                ? new AttackMoveCommand(me, at, context.NextSequence(), units, point.Center)
                : new MoveCommand(me, at, context.NextSequence(), units, point.Center));
        }
    }

    /// <summary>Where the fleet gathers: the exit of the bot's first shipyard, else the middle of the harbour; null with neither.</summary>
    private static CellPos? Home(AiContext context)
    {
        foreach (var building in context.View.OwnBuildings)
        {
            if (building.Def is { Water: true, ExitCell: { } exit } && building.Def.Produces.Contains(ProductionCategory.Naval))
            {
                return new CellPos(building.Cell.X + exit.X, building.Cell.Y + exit.Y);
            }
        }

        return context.Map.Harbour is { } harbour ? new CellPos(harbour.X + 2, harbour.Y + 2) : null;
    }

    /// <summary>The orders of ships that are gone are forgotten.</summary>
    private void Forget()
    {
        _gone.Clear();
        foreach (var id in _orders.Keys)
        {
            var alive = false;
            foreach (var ship in _ships)
            {
                alive |= ship.Id.Value == id;
            }

            if (!alive)
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _orders.Remove(id);
        }
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
