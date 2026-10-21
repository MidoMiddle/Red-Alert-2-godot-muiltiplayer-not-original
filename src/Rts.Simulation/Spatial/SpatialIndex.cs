using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Spatial;

/// <summary>
/// Units by area (docs/implementation/M2_unit_control.md, 4.9): the map is split into buckets of
/// <see cref="BucketCells"/> × <see cref="BucketCells"/> cells, each holding the ids of its units in increasing order.
/// A query looks only at the buckets its circle or rectangle touches and checks each unit's exact position, so it costs
/// what is near, not what is on the map. Results are always sorted by <see cref="EntityId"/>.
/// A derived structure: it can be rebuilt from unit positions at any time, so it is not part of the state hash.
/// </summary>
public sealed class SpatialIndex
{
    public const int BucketCells = 4;

    private const int BucketLeptons = BucketCells * WorldPos.LeptonsPerCell;

    private readonly List<EntityId>[] _buckets;
    private readonly Dictionary<EntityId, int> _bucketOf = new();

    internal SpatialIndex(int widthCells, int heightCells)
    {
        Columns = (widthCells + BucketCells - 1) / BucketCells;
        Rows = (heightCells + BucketCells - 1) / BucketCells;
        _buckets = new List<EntityId>[Columns * Rows];
        for (var i = 0; i < _buckets.Length; i++)
        {
            _buckets[i] = new List<EntityId>();
        }
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Number of units in the index.</summary>
    public int Count => _bucketOf.Count;

    /// <summary>Ids of the units within <paramref name="radius"/> leptons of <paramref name="center"/> (inclusive), sorted.</summary>
    public void QueryRadius(GameWorld world, WorldPos center, long radius, List<EntityId> results)
    {
        results.Clear();
        if (radius < 0)
        {
            return;
        }

        var radiusSquared = radius * radius;
        Collect(world, center.X - radius, center.Y - radius, center.X + radius, center.Y + radius, results,
            position => (position - center).LengthSquared <= radiusSquared);
    }

    /// <summary>Ids of the units inside the rectangle spanned by two corners (inclusive, in any order), sorted.</summary>
    public void QueryRect(GameWorld world, WorldPos corner, WorldPos opposite, List<EntityId> results)
    {
        results.Clear();
        var minX = IntMath.Min(corner.X, opposite.X);
        var minY = IntMath.Min(corner.Y, opposite.Y);
        var maxX = IntMath.Max(corner.X, opposite.X);
        var maxY = IntMath.Max(corner.Y, opposite.Y);
        Collect(world, minX, minY, maxX, maxY, results,
            position => position.X >= minX && position.X <= maxX && position.Y >= minY && position.Y <= maxY);
    }

    internal void Add(Unit unit)
    {
        var bucket = BucketOf(unit.Position);
        _bucketOf[unit.Id] = bucket;
        Insert(_buckets[bucket], unit.Id);
    }

    internal void Remove(EntityId unit)
    {
        if (_bucketOf.Remove(unit, out var bucket))
        {
            _buckets[bucket].Remove(unit);
        }
    }

    /// <summary>Moves every unit that crossed into another bucket since the last call; the rest stay untouched.</summary>
    internal void Sync(GameWorld world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit || !_bucketOf.TryGetValue(unit.Id, out var old))
            {
                continue;
            }

            var bucket = BucketOf(unit.Position);
            if (bucket != old)
            {
                _buckets[old].Remove(unit.Id);
                _bucketOf[unit.Id] = bucket;
                Insert(_buckets[bucket], unit.Id);
            }
        }
    }

    /// <summary>Forgets everything and indexes every living unit again.</summary>
    internal void Rebuild(GameWorld world)
    {
        foreach (var bucket in _buckets)
        {
            bucket.Clear();
        }

        _bucketOf.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true } unit)
            {
                Add(unit);
            }
        }
    }

    private void Collect(GameWorld world, long minX, long minY, long maxX, long maxY, List<EntityId> results, Func<WorldPos, bool> inside)
    {
        var firstColumn = (int)IntMath.Clamp(IntMath.DivFloor(minX, BucketLeptons), 0, Columns - 1);
        var lastColumn = (int)IntMath.Clamp(IntMath.DivFloor(maxX, BucketLeptons), 0, Columns - 1);
        var firstRow = (int)IntMath.Clamp(IntMath.DivFloor(minY, BucketLeptons), 0, Rows - 1);
        var lastRow = (int)IntMath.Clamp(IntMath.DivFloor(maxY, BucketLeptons), 0, Rows - 1);
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                foreach (var id in _buckets[row * Columns + column])
                {
                    if (world.TryGet(id, out var entity) && entity is Unit unit && inside(unit.Position))
                    {
                        results.Add(id);
                    }
                }
            }
        }

        // Buckets are sorted each, but several buckets interleave.
        results.Sort();
    }

    /// <summary>Bucket of a position; positions off the map go to the nearest edge bucket.</summary>
    private int BucketOf(WorldPos position)
    {
        var column = (int)IntMath.Clamp(IntMath.DivFloor(position.X, BucketLeptons), 0, Columns - 1);
        var row = (int)IntMath.Clamp(IntMath.DivFloor(position.Y, BucketLeptons), 0, Rows - 1);
        return row * Columns + column;
    }

    private static void Insert(List<EntityId> bucket, EntityId id)
    {
        var index = bucket.BinarySearch(id);
        bucket.Insert(index < 0 ? ~index : index, id);
    }
}
