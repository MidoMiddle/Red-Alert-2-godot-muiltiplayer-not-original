using System;
using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Pathfinding;

/// <summary>A unit's wait for a path: who asked, when, from where to where, and the search in progress.</summary>
public sealed class PathRequest
{
    internal PathRequest(EntityId unit, int requestTick, CellPos start, CellPos goal, Locomotor locomotor, PathSearch search)
    {
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

    public CellPos Goal { get; }

    public Locomotor Locomotor { get; }

    /// <summary>Nodes expanded so far. Together with the fields above and the map it fully describes the search state.</summary>
    public int Expanded => Search.Expanded;

    internal PathSearch Search { get; }
}

/// <summary>
/// Path requests of all units (docs/implementation/M2_unit_control.md, 4.4). Units never search themselves: an order puts a
/// request here, and every tick the queue spends at most <see cref="Budget"/> expanded nodes on the requests in
/// (request tick, unit id) order. A search that runs out of budget continues on the next tick from where it stopped,
/// so a big order spreads over several ticks instead of stalling one. One request per unit: a new order replaces the old one.
/// </summary>
public sealed class PathRequestQueue
{
    /// <summary>Nodes all searches may expand in one tick together.</summary>
    public const int DefaultBudget = 6_000;

    private readonly Pathfinder _pathfinder;
    private readonly List<PathRequest> _requests = new();

    internal PathRequestQueue(MapGrid map) => _pathfinder = new Pathfinder(map);

    /// <summary>Node budget per tick. A rule of the simulation; tests change it to check that results do not depend on it.</summary>
    public int Budget { get; internal set; } = DefaultBudget;

    /// <summary>Requests still waiting, in processing order.</summary>
    public IReadOnlyList<PathRequest> Requests => _requests;

    /// <summary>Nodes expanded during the last <see cref="Process"/>, for the debug panel.</summary>
    public int ExpandedLastTick { get; private set; }

    /// <summary>Asks for a path for <paramref name="unit"/> from its current cell; replaces its previous request.</summary>
    internal void Request(Unit unit, int tick, CellPos goal)
    {
        Cancel(unit.Id);
        var start = unit.Position.ToCell();
        var locomotor = unit.Archetype.Locomotor;
        var request = new PathRequest(unit.Id, tick, start, goal, locomotor, _pathfinder.StartSearch(start, goal, locomotor));

        var index = _requests.Count;
        while (index > 0 && Before(request, _requests[index - 1]))
        {
            index--;
        }

        _requests.Insert(index, request);
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

            var path = request.Search.Result!;
            unit.Path = path;
            unit.PathIndex = 1;
            unit.PathPending = false;
            events.Add(new UnitPathFound(world.CurrentTick, unit.Id, path.Status, path.Cells.Length));
            _requests.RemoveAt(i);
        }
    }

    private static bool Before(PathRequest a, PathRequest b) =>
        a.RequestTick < b.RequestTick || (a.RequestTick == b.RequestTick && a.Unit.CompareTo(b.Unit) < 0);
}
