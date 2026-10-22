using System;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Hashing;

/// <summary>
/// Fingerprint of the complete simulation state (docs/implementation/M0_foundation.md, 4.9).
/// Peers and replays compare it to catch a desync on the tick it happens.
/// Rule: every field of the simulation state must be fed here; a test guards this.
/// Not included: events (presentation only) and the queue of future commands
/// (identical on every peer by construction in lockstep; checked separately in M9).
/// </summary>
public static class StateHasher
{
    // Tags are only for the hash; the M0 TestUnit was 1.
    private const byte UnitTag = 2;
    private const byte BuildingTag = 3;

    public static ulong Compute(GameWorld world)
    {
        var hash = new Fnv1a64();

        hash.Add(world.CurrentTick);
        hash.Add(world.WidthCells);
        hash.Add(world.HeightCells);
        hash.Add(world.NextEntityId.Value);
        var random = world.Random.State;
        hash.Add(random.State);
        hash.Add(random.Increment);

        // The map and every path come as digests computed once; places as the taken ones only (M2 step 15, 4.15).
        hash.Add(world.Map.Digest);
        AddPathRequests(hash, world.PathRequests);
        AddTaken(hash, world.Reservations.VehicleCells);
        AddTaken(hash, world.Reservations.InfantrySpots);

        hash.Add(world.Entities.Count);
        foreach (var entity in world.Entities)
        {
            AddEntity(hash, entity);
        }

        return hash.Value;
    }

    private static void AddEntity(Fnv1a64 hash, Entity entity)
    {
        switch (entity)
        {
            case Unit unit:
                hash.Add(UnitTag);
                AddCommon(hash, unit);
                hash.Add((byte)unit.Type);
                hash.Add(unit.Position.X);
                hash.Add(unit.Position.Y);
                hash.Add(unit.MoveTarget.HasValue);
                if (unit.MoveTarget is { } target)
                {
                    hash.Add(target.X);
                    hash.Add(target.Y);
                }

                hash.Add(unit.Speed);
                hash.Add(unit.TurnRate);
                hash.Add((int)unit.Facing.Value);
                hash.Add((int)unit.TurretFacing.Value);
                hash.Add(unit.PathPending);
                hash.Add(unit.PathIndex);
                AddPath(hash, unit.Path);
                hash.Add(unit.WaitTicks);
                hash.Add(unit.Replans);
                hash.Add(unit.HoldTicks);
                hash.Add(unit.Returning);
                hash.Add(unit.Subcell);
                hash.Add(unit.Orders.Length);
                foreach (var order in unit.Orders)
                {
                    hash.Add(order.Point.X);
                    hash.Add(order.Point.Y);
                    hash.Add(order.AttackMove);
                }

                hash.Add(unit.OrderIndex);
                hash.Add(unit.LoopOrders);
                hash.Add(unit.ResumeTarget.HasValue);
                if (unit.ResumeTarget is { } resume)
                {
                    hash.Add(resume.X);
                    hash.Add(resume.Y);
                }
                break;

            case Building building:
                // Rules v18 (M7 step 7): the minimal building of the map.
                hash.Add(BuildingTag);
                AddCommon(hash, building);
                hash.Add((byte)building.Kind);
                hash.Add(building.Cell.X);
                hash.Add(building.Cell.Y);
                hash.Add(building.Facing);
                hash.Add(building.Hp);
                break;

            default:
                // A new entity type must be added here explicitly, never silently skipped.
                throw new InvalidOperationException($"StateHasher does not know entity type {entity.GetType().Name}.");
        }
    }

    /// <summary>The number of taken places, then each as (index, owner) in index order; empty places are left out.</summary>
    private static void AddTaken(Fnv1a64 hash, ReadOnlySpan<int> owners)
    {
        var taken = 0;
        foreach (var owner in owners)
        {
            taken += owner != 0 ? 1 : 0;
        }

        hash.Add(taken);
        for (var i = 0; i < owners.Length; i++)
        {
            if (owners[i] != 0)
            {
                hash.Add(i);
                hash.Add(owners[i]);
            }
        }
    }

    private static void AddPath(Fnv1a64 hash, CellPath? path)
    {
        hash.Add(path is not null);
        if (path is not null)
        {
            hash.Add(path.Digest);
        }
    }

    // The search arrays are not hashed: given the map, the request fields and the number of expanded nodes,
    // a deterministic search is always in the same state.
    private static void AddPathRequests(Fnv1a64 hash, PathRequestQueue queue)
    {
        hash.Add(queue.Budget);
        hash.Add(queue.ExpandedLastTick);
        hash.Add(queue.Requests.Count);
        foreach (var request in queue.Requests)
        {
            hash.Add(request.Unit.Value);
            hash.Add(request.RequestTick);
            hash.Add(request.Start.X);
            hash.Add(request.Start.Y);
            hash.Add(request.Goal.X);
            hash.Add(request.Goal.Y);
            hash.Add((byte)request.Locomotor);
            hash.Add(request.Expanded);
            hash.Add(request.Avoid.Length);
            foreach (var cell in request.Avoid)
            {
                hash.Add(cell.X);
                hash.Add(cell.Y);
            }
        }
    }

    private static void AddCommon(Fnv1a64 hash, Entity entity)
    {
        hash.Add(entity.Id.Value);
        hash.Add(entity.Owner.Value);
        hash.Add(entity.IsAlive);
    }
}
