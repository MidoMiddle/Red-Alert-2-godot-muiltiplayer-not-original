using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Movement;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Random;
using Rts.Simulation.Spatial;

namespace Rts.Simulation.World;

/// <summary>
/// Complete simulation state: tick number, entities, id counter, random generator and the map.
/// Entities are kept sorted by id; iterating <see cref="Entities"/> is always in id order,
/// which keeps every system deterministic.
/// </summary>
public sealed class GameWorld
{
    private readonly List<Entity> _entities = new();
    private int _nextEntityId = 1;

    /// <summary>A world on a map of clear land.</summary>
    public GameWorld(ulong seed, int widthCells, int heightCells)
        : this(seed, new MapGrid(widthCells, heightCells))
    {
    }

    /// <summary>A world on the given map. Maps from settings and replays arrive in M2 step 4; tests use this now.</summary>
    internal GameWorld(ulong seed, MapGrid map)
    {
        Map = map;
        Random = new DeterministicRandom(seed);
        PathRequests = new PathRequestQueue(map);
        Reservations = new Reservations(map.Width, map.Height);
        Spatial = new SpatialIndex(map.Width, map.Height);
    }

    public int CurrentTick { get; internal set; }

    public DeterministicRandom Random { get; }

    public MapGrid Map { get; }

    /// <summary>Path searches waiting for their share of the per-tick node budget.</summary>
    public PathRequestQueue PathRequests { get; }

    /// <summary>Which unit stands in which cell: one unit per cell.</summary>
    public Reservations Reservations { get; }

    /// <summary>Units by area, for "who is near" queries. Derived from positions, not part of the state hash.</summary>
    public SpatialIndex Spatial { get; }

    public int WidthCells => Map.Width;

    public int HeightCells => Map.Height;

    /// <summary>Id the next spawned entity will get. Part of the state hash.</summary>
    public EntityId NextEntityId => new(_nextEntityId);

    /// <summary>All entities in increasing id order, including ones marked dead during the current tick.</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>True when the position is inside the map.</summary>
    public bool Contains(WorldPos position) =>
        position.X >= 0 && position.Y >= 0
        && position.X < WidthCells * WorldPos.LeptonsPerCell
        && position.Y < HeightCells * WorldPos.LeptonsPerCell;

    /// <summary>
    /// Finds an entity by id (binary search). An entity marked dead this tick is still found,
    /// with <see cref="Entity.IsAlive"/> == false, until <see cref="RemoveDead"/> runs.
    /// </summary>
    public bool TryGet(EntityId id, out Entity entity)
    {
        var low = 0;
        var high = _entities.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var candidate = _entities[middle];
            var comparison = candidate.Id.CompareTo(id);
            if (comparison == 0)
            {
                entity = candidate;
                return true;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        entity = null!;
        return false;
    }

    /// <param name="snapToSubcell">Put a soldier on the spot it takes (commands); tests keep the exact position.</param>
    /// <summary>
    /// Changes the map during a match (docs/implementation/M7_maps.md, 4.4): applies the edits, patches the A* step costs in the
    /// changed area only, restarts path searches already under way, and sends every unit whose remaining path now crosses a
    /// cell it cannot enter to look for a new one (not counted as a replan). A cell that opens up does not disturb units on
    /// their way, as in RA2: the next search finds it. Reports <see cref="Events.MapChanged"/> with the changed area.
    /// </summary>
    internal void ChangeMap(IReadOnlyList<MapEdit> edits, List<Events.SimEvent>? events)
    {
        if (edits.Count == 0)
        {
            return;
        }

        var versionBefore = Map.Version;
        var (from, to) = edits[0].Area(Map);
        foreach (var edit in edits)
        {
            var (editFrom, editTo) = edit.Area(Map);
            from = new CellPos(Math.IntMath.Min(from.X, editFrom.X), Math.IntMath.Min(from.Y, editFrom.Y));
            to = new CellPos(Math.IntMath.Max(to.X, editTo.X), Math.IntMath.Max(to.Y, editTo.Y));
            edit.Apply(Map);
        }

        PathRequests.Pathfinder.MapChanged(versionBefore, from, to);
        PathRequests.MapChanged();
        foreach (var entity in _entities)
        {
            if (entity is Unit { IsAlive: true, Path: { } path, MoveTarget: { } target } unit && CrossesClosedCell(unit, path, from, to))
            {
                Movement.UnitOrders.RequestPath(this, unit, target, default);
            }
        }

        events?.Add(new Events.MapChanged(CurrentTick, from, to));
    }

    /// <summary>True when a cell of the path still ahead of the unit lies in the area and is closed to it now.</summary>
    private bool CrossesClosedCell(Unit unit, Pathfinding.CellPath path, CellPos from, CellPos to)
    {
        var here = unit.Position.ToCell();
        var start = path.Cells.IndexOf(here);
        for (var i = start < 0 ? 0 : start; i < path.Cells.Length; i++)
        {
            var cell = path.Cells[i];
            if (cell.X >= from.X && cell.X <= to.X && cell.Y >= from.Y && cell.Y <= to.Y && !Map.CanEnter(cell, unit.Archetype.Locomotor))
            {
                return true;
            }
        }

        return false;
    }

    internal Unit SpawnUnit(PlayerId owner, UnitType type, WorldPos position, bool snapToSubcell = false)
    {
        var unit = new Unit(AllocateId(), owner, UnitArchetypes.Get(type), position);
        _entities.Add(unit);
        Spatial.Add(unit);
        // Commands never spawn where there is no room; a test that places two units in one cell gets an unregistered second one.
        if (Contains(position))
        {
            var cell = position.ToCell();
            var subcell = Reservations.TryEnter(cell, unit, position);
            if (subcell >= 0)
            {
                unit.Subcell = subcell;
                if (snapToSubcell && unit.Archetype.Placement == UnitPlacement.Subcell)
                {
                    unit.Position = Reservations.SubcellPoint(cell, subcell);
                }
            }
        }

        return unit;
    }

    /// <summary>Physically removes entities marked dead and frees their cells. Called once at the end of a tick.</summary>
    internal void RemoveDead()
    {
        foreach (var entity in _entities)
        {
            if (entity is Unit { IsAlive: false } unit)
            {
                Spatial.Remove(unit.Id);
                if (Contains(unit.Position))
                {
                    Reservations.Leave(unit.Position.ToCell(), unit);
                }
            }
        }

        _entities.RemoveAll(entity => !entity.IsAlive);
    }

    private EntityId AllocateId() => new(_nextEntityId++);
}
