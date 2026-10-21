using System;
using Rts.Simulation.Map;
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

        AddMap(hash, world.Map);

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
                break;

            default:
                // A new entity type must be added here explicitly, never silently skipped.
                throw new InvalidOperationException($"StateHasher does not know entity type {entity.GetType().Name}.");
        }
    }

    private static void AddMap(Fnv1a64 hash, MapGrid map)
    {
        // Width and height are already in the world part; the layers follow in cell index order.
        foreach (var land in map.LandCells)
        {
            hash.Add((byte)land);
        }

        foreach (var blocked in map.BlockedCells)
        {
            hash.Add(blocked);
        }
    }

    private static void AddCommon(Fnv1a64 hash, Entity entity)
    {
        hash.Add(entity.Id.Value);
        hash.Add(entity.Owner.Value);
        hash.Add(entity.IsAlive);
    }
}
