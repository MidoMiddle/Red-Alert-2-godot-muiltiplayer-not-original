using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The ferry of a bot (M11 step 3, the bot that knows every weapon): on a map where no tank drives to the enemy
/// (<see cref="Knowledge.MapReading.LandRoute"/>) but a hovercraft comes ashore at its land (<see cref="Knowledge.MapReading.Landings"/>), its
/// transports (<see cref="Knowledge.UnitUse.Transport"/>: the hovercraft, the landing craft, the Night Hawk) carry the squad that is ready
/// (<see cref="AiPlan.FerryReady"/>) over the water.
/// <list type="bullet">
/// <item>Up to <see cref="MaxTransports"/> transports, alive and queued: of those the bot can build now, the most slots for the money.</item>
/// <item>An idle transport takes the units of the ready squad it has slots for — the largest first, no larger than it takes — and they
/// board it (<see cref="EnterCommand"/>); on the way they are on an errand (<see cref="AiPlan.Errands"/>).</item>
/// <item>Loaded, it goes to the landing nearest the enemy the squad was to attack and lets them out there (<see cref="DeployCommand"/>);
/// the units of the army by it then are put ashore (<see cref="AiPlan.Landed"/>): the squads send them on as a squad that is out.</item>
/// <item>Empty, it goes home to the start and waits for the next.</item>
/// </list>
/// </summary>
public sealed class FerryManager : IAiModule
{
    /// <summary>The transports the bot keeps.</summary>
    public const int MaxTransports = 2;

    /// <summary>A transport this near the landing, in cells, is there.</summary>
    public const int ArrivedCells = 2;

    /// <summary>The units this near a transport that let them out, in cells, came out of it.</summary>
    public const int AshoreCells = 5;

    private enum Stage
    {
        Loading,
        Sailing,
        Unloading,
    }

    private sealed class Trip
    {
        public readonly List<int> Riders = new();
        public CellPos Landing;
        public Stage Stage;
    }

    private readonly SortedDictionary<int, Trip> _trips = new();
    private readonly List<int> _gone = new();
    private readonly List<Unit> _transports = new();

    public AiModuleKind Kind => AiModuleKind.Ferry;

    public void Update(AiContext context)
    {
        var view = context.View;
        var map = context.Map;
        _transports.Clear();
        foreach (var unit in view.OwnUnits)
        {
            if (context.Units.Of(unit).Use == Knowledge.UnitUse.Transport)
            {
                _transports.Add(unit);
            }
        }

        Forget(context);
        if (map.LandRoute || map.Landings.Length == 0)
        {
            return;
        }

        Order(context);
        foreach (var transport in _transports)
        {
            if (!_trips.TryGetValue(transport.Id.Value, out var trip))
            {
                Load(context, transport);
                continue;
            }

            switch (trip.Stage)
            {
                case Stage.Loading:
                    Loading(context, transport, trip);
                    break;
                case Stage.Sailing:
                    if (Distance(transport.Position.ToCell(), trip.Landing) <= ArrivedCells)
                    {
                        trip.Stage = Stage.Unloading;
                        context.Issue(new DeployCommand(view.Me, context.ExecuteTick, context.NextSequence(), transport.Id));
                        context.Log?.Add(context.Tick, context.Module, $"{transport.Archetype.Id} lets {transport.Passengers.Count} out at {trip.Landing.X},{trip.Landing.Y}");
                    }
                    else if (transport.MoveTarget is null)
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [transport.Id], trip.Landing.Center));
                    }

                    break;
                default:
                    Unloading(context, transport, trip);
                    break;
            }
        }
    }

    /// <summary>The trips of transports that fell are over: their riders still outside are off their errand.</summary>
    private void Forget(AiContext context)
    {
        _gone.Clear();
        foreach (var (id, trip) in _trips)
        {
            if (!_transports.Exists(transport => transport.Id.Value == id))
            {
                _gone.Add(id);
                foreach (var rider in trip.Riders)
                {
                    context.Plan.Errands.Remove(rider);
                }
            }
        }

        foreach (var id in _gone)
        {
            _trips.Remove(id);
        }
    }

    /// <summary>A transport, with fewer than <see cref="MaxTransports"/> alive and queued and the money there.</summary>
    private void Order(AiContext context)
    {
        var view = context.View;
        var have = _transports.Count;
        for (var category = ProductionCategories.First; category <= ProductionCategories.Last; category++)
        {
            foreach (var item in view.Player.Queue(category))
            {
                have += item.Kind == ProductionKind.Unit && context.Units[item.DefIndex].Use == Knowledge.UnitUse.Transport ? 1 : 0;
            }
        }

        if (have >= MaxTransports)
        {
            return;
        }

        var (best, bestSlots, bestCost) = (-1, 0, 1);
        foreach (var card in context.Units.Cards)
        {
            var def = view.Rules.Database.Units[card.Index];
            if (card.Use == Knowledge.UnitUse.Transport && view.Player.CanBuildUnit(card.Index) && view.HasFactory(def.ProducedIn)
                && (long)def.PassengerSlots * bestCost > (long)bestSlots * def.Cost)
            {
                (best, bestSlots, bestCost) = (card.Index, def.PassengerSlots, def.Cost);
            }
        }

        if (best >= 0 && bestCost <= view.SpareCredits())
        {
            context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Unit, best, 1));
            context.Log?.Add(context.Tick, context.Module, $"orders {view.Rules.Database.Units[best].Id} to carry the army over the water");
        }
    }

    /// <summary>An idle transport takes the ready squad's units it has slots for.</summary>
    private void Load(AiContext context, Unit transport)
    {
        if (context.Plan.FerryReady is not { } squad)
        {
            return;
        }

        var view = context.View;
        var riders = new List<Unit>();
        foreach (var id in squad.Units)
        {
            foreach (var unit in view.OwnUnits)
            {
                if (unit.Id == id && !context.Plan.Errands.Contains(id.Value) && !unit.Archetype.Flies
                    && unit.Archetype.Size <= transport.Archetype.PassengerSizeLimit)
                {
                    riders.Add(unit);
                }
            }
        }

        // The largest first, then by id: the tanks before the soldiers who fill the slots left.
        riders.Sort((a, b) => a.Archetype.Size != b.Archetype.Size ? b.Archetype.Size.CompareTo(a.Archetype.Size) : a.Id.Value.CompareTo(b.Id.Value));
        var trip = new Trip { Landing = context.Map.LandingNear(squad.TargetCell ?? context.Memory.LikelyEnemyCell() ?? transport.Position.ToCell())!.Value };
        var slots = transport.Archetype.PassengerSlots;
        var batch = ImmutableArray.CreateBuilder<EntityId>();
        foreach (var rider in riders)
        {
            if (rider.Archetype.Size <= slots)
            {
                slots -= rider.Archetype.Size;
                batch.Add(rider.Id);
                trip.Riders.Add(rider.Id.Value);
                context.Plan.Errands.Add(rider.Id.Value);
            }
        }

        if (batch.Count == 0)
        {
            return;
        }

        _trips[transport.Id.Value] = trip;
        context.Issue(new EnterCommand(view.Me, context.ExecuteTick, context.NextSequence(), batch.ToImmutable(), transport.Id));
        context.Log?.Add(context.Tick, context.Module, $"{batch.Count} board {transport.Archetype.Id} for {trip.Landing.X},{trip.Landing.Y}");
    }

    /// <summary>The riders still outside on their way in are waited for; those who gave up stay home; with all in, it sails.</summary>
    private void Loading(AiContext context, Unit transport, Trip trip)
    {
        var view = context.View;
        var waiting = false;
        for (var i = trip.Riders.Count - 1; i >= 0; i--)
        {
            var rider = trip.Riders[i];
            foreach (var unit in view.OwnUnits)
            {
                if (unit.Id.Value != rider)
                {
                    continue;
                }

                if (unit.EnterTarget == transport.Id)
                {
                    waiting = true;
                }
                else
                {
                    trip.Riders.RemoveAt(i);
                    context.Plan.Errands.Remove(rider);
                }
            }
        }

        if (waiting)
        {
            return;
        }

        foreach (var rider in trip.Riders)
        {
            context.Plan.Errands.Remove(rider);
        }

        trip.Riders.Clear();
        if (transport.Passengers.Count == 0)
        {
            _trips.Remove(transport.Id.Value);
            return;
        }

        trip.Stage = Stage.Sailing;
        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [transport.Id], trip.Landing.Center));
    }

    /// <summary>The units out by the transport are ashore; empty, it goes home.</summary>
    private void Unloading(AiContext context, Unit transport, Trip trip)
    {
        var view = context.View;
        var here = transport.Position.ToCell();
        foreach (var unit in view.OwnUnits)
        {
            if (context.Units.InArmy(unit) && Distance(unit.Position.ToCell(), here) <= AshoreCells && !context.Plan.Errands.Contains(unit.Id.Value))
            {
                context.Plan.Landed.Add(unit.Id.Value);
            }
        }

        if (transport.Passengers.Count > 0)
        {
            // No room round it for all: once more.
            context.Issue(new DeployCommand(view.Me, context.ExecuteTick, context.NextSequence(), transport.Id));
            return;
        }

        _trips.Remove(transport.Id.Value);
        if (view.Start is { } start)
        {
            context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [transport.Id], start.Center));
        }
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
