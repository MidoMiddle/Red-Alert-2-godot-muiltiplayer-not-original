using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Pathfinding;

/// <summary>A unit's wait for a path: who asked, when, from where to where, and the search in progress.</summary>
public sealed class PathRequest
{
    internal PathRequest(
        EntityId unit, int requestTick, CellPos start, CellPos goal, Locomotor locomotor, ImmutableArray<CellPos> avoid, PathSearch search,
        bool startOnDeck = false, bool groundGoal = false)
    {
        Avoid = avoid.IsDefault ? [] : avoid;
        StartOnDeck = startOnDeck;
        GroundGoal = groundGoal;
        Unit = unit;
        RequestTick = requestTick;
        Start = start;
        Goal = goal;
        Locomotor = locomotor;
        Search = search;
    }

    public EntityId Unit { get; }

    public int RequestTick { get; }

    public CellPos Start { get; }

    /// <summary>The unit asked from the deck of a high bridge over <see cref="Start"/> (<see cref="Decks"/>).</summary>
    public bool StartOnDeck { get; }

    public CellPos Goal { get; }

    /// <summary>The goal counts on the ground only, not on a deck over it: a harvester going for ore (<see cref="Decks"/>).</summary>
    public bool GroundGoal { get; }

    public Locomotor Locomotor { get; }

    /// <summary>Cells this search treats as impassable (taken by other units when the unit asked for a way around them).</summary>
    public ImmutableArray<CellPos> Avoid { get; }

    /// <summary>Nodes expanded so far. Together with the fields above and the map it fully describes the search state.</summary>
    public int Expanded => Search.Expanded;

    internal PathSearch Search { get; private set; }

    /// <summary>Drops the search worked out so far and starts it again with the same request, after the map changed.</summary>
    internal void Restart(Pathfinder pathfinder)
    {
        Search.Cancel();
        Search = pathfinder.StartSearch(Start, Goal, Locomotor, AStar.DefaultMaxExpansions, Avoid, StartOnDeck, GroundGoal);
    }
}

/// <summary>
/// Path requests of all units (docs/implementation/M2_unit_control.md, 4.4). Units never search themselves: an order puts a
/// request here, and every tick the queue spends at most <see cref="Budget"/> expanded nodes on the requests in
/// (request tick, unit id) order. A search that runs out of budget continues on the next tick from where it stopped,
/// so a big order spreads over several ticks instead of stalling one. One request per unit: a new order replaces the old one.
/// A search around taken cells that cannot reach the goal is asked again without them: units move, terrain does not.
/// </summary>
public sealed class PathRequestQueue
{
    /// <summary>Nodes all searches may expand in one tick together.</summary>
    public const int DefaultBudget = 6_000;

    private readonly Pathfinder _pathfinder;
    private readonly List<PathRequest> _requests = new();

    internal PathRequestQueue(MapGrid map) => _pathfinder = new Pathfinder(map);

    /// <summary>The searches of this world; others (group places) read its step costs.</summary>
    internal Pathfinder Pathfinder => _pathfinder;

    /// <summary>Node budget per tick. A rule of the simulation; tests change it to check that results do not depend on it.</summary>
    public int Budget { get; internal set; } = DefaultBudget;

    /// <summary>Requests still waiting, in processing order.</summary>
    public IReadOnlyList<PathRequest> Requests => _requests;

    /// <summary>Nodes expanded during the last <see cref="Process"/>, for the debug panel.</summary>
    public int ExpandedLastTick { get; private set; }

    /// <summary>
    /// Asks for a path for <paramref name="unit"/> from its current cell; replaces its previous request.
    /// <paramref name="avoid"/> are cells to keep out of, for a way around other units.
    /// </summary>
    internal void Request(Unit unit, int tick, CellPos goal, ImmutableArray<CellPos> avoid = default)
    {
        Cancel(unit.Id);
        var start = unit.Position.ToCell();
        var locomotor = unit.Archetype.Locomotor;
        // Ore lies on the ground: a harvester sent to it never stops on a deck over it.
        var groundGoal = unit.Archetype.Harvests && _pathfinder.Map.DeckCount > 0 && !_pathfinder.Map.OreAt(goal).IsEmpty;
        var search = _pathfinder.StartSearch(start, goal, locomotor, AStar.DefaultMaxExpansions, avoid, unit.OnDeck, groundGoal);
        var request = new PathRequest(unit.Id, tick, start, goal, locomotor, avoid, search, unit.OnDeck, groundGoal);

        var index = _requests.Count;
        while (index > 0 && Before(request, _requests[index - 1]))
        {
            index--;
        }

        _requests.Insert(index, request);
    }

    /// <summary>
    /// The map changed: searches already under way worked on the old map and start again, keeping their place in the queue.
    /// Searches not started yet read the map when they start.
    /// </summary>
    internal void MapChanged()
    {
        foreach (var request in _requests)
        {
            if (request.Search.IsStarted && !request.Search.IsDone)
            {
                request.Restart(_pathfinder);
            }
        }
    }

    internal void Cancel(EntityId unit)
    {
        for (var i = 0; i < _requests.Count; i++)
        {
            if (_requests[i].Unit == unit)
            {
                _requests[i].Search.Cancel();
                _requests.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>Spends this tick's budget on the requests in order and hands finished paths to their units.</summary>
    internal void Process(GameWorld world, List<SimEvent> events)
    {
        ExpandedLastTick = 0;
        var remaining = Budget;
        var i = 0;
        while (i < _requests.Count && remaining > 0)
        {
            var request = _requests[i];
            if (!world.TryGet(request.Unit, out var entity) || entity is not Unit { IsAlive: true, PathPending: true } unit)
            {
                request.Search.Cancel();
                _requests.RemoveAt(i);
                continue;
            }

            var before = request.Search.Expanded;
            var done = request.Search.Advance(remaining);
            var spent = request.Search.Expanded - before;
            remaining -= spent;
            ExpandedLastTick += spent;
            if (!done)
            {
                // Advance stops early only when the budget is used up.
                break;
            }

            if (request.Avoid.Length > 0 && request.Search.Result!.Status != PathStatus.Found)
            {
                // No way around the taken cells: they are only units, so ask for the plain path and queue up behind them.
                _requests.RemoveAt(i);
                Request(unit, world.CurrentTick, request.Goal);
                continue;
            }

            var path = PathSmoother.Smooth(world.Map, request.Search.Result!, request.Locomotor, request.Avoid, request.StartOnDeck);
            unit.Path = path;
            // Segments are checked between cell centres: a unit that stopped off the centre of its cell (say, at the edge,
            // in front of another unit) first goes back to the centre, so its first segment cannot clip a neighbouring cell.
            unit.PathIndex = unit.Position == path.Cells[0].Center ? 1 : 0;
            unit.PathPending = false;
            events.Add(new UnitPathFound(world.CurrentTick, unit.Id, path.Status, path.Cells.Length));
            _requests.RemoveAt(i);
        }
    }

    private static bool Before(PathRequest a, PathRequest b) =>
        a.RequestTick < b.RequestTick || (a.RequestTick == b.RequestTick && a.Unit.CompareTo(b.Unit) < 0);
}
