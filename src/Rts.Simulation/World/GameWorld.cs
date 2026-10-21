using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Random;

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
    }

    public int CurrentTick { get; internal set; }

    public DeterministicRandom Random { get; }

    public MapGrid Map { get; }

    /// <summary>Path searches waiting for their share of the per-tick node budget.</summary>
    public PathRequestQueue PathRequests { get; }

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

    internal Unit SpawnUnit(PlayerId owner, UnitType type, WorldPos position)
    {
        var unit = new Unit(AllocateId(), owner, UnitArchetypes.Get(type), position);
        _entities.Add(unit);
        return unit;
    }

    /// <summary>Physically removes entities marked dead. Called once at the end of a tick.</summary>
    internal void RemoveDead() => _entities.RemoveAll(entity => !entity.IsAlive);

    private EntityId AllocateId() => new(_nextEntityId++);
}
