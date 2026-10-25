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
    private const byte ProjectileTag = 4;

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

        // Rules v56 (stage 2d): the storms of the Storm Generator.
        hash.Add(world.Storms.Count);
        foreach (var storm in world.Storms)
        {
            hash.Add(storm.Owner.Value);
            hash.Add(storm.Source.Value);
            hash.Add(storm.Weapon);
            hash.Add(storm.Centre.X);
            hash.Add(storm.Centre.Y);
            hash.Add(storm.Start);
            hash.Add(storm.End);
        }

        // Rules v65: the C4 on bridges (v66: shells hurt them too, the segments are in the map's digest).
        hash.Add(world.BridgeBombs.Count);
        foreach (var bomb in world.BridgeBombs)
        {
            hash.Add(bomb.Owner.Value);
            hash.Add(bomb.Shooter.Value);
            hash.Add(bomb.Bridge);
            hash.Add(bomb.Segment);
            hash.Add(bomb.GoesOff);
        }

        // Rules v74 (M11 a6): the crates, in a match that has them only — the others keep their hashes.
        if (Systems.CrateSystem.Enabled(world))
        {
            hash.Add(world.Crates.Count);
            foreach (var crate in world.Crates)
            {
                hash.Add(crate.Id);
                hash.Add(crate.Cell.X);
                hash.Add(crate.Cell.Y);
            }

            hash.Add(world.CrateTimer);
            hash.Add(world.NextCrateId);
        }

        // Rules v50 (stage 3b): the toxin on the ground, cell by cell where there is some.
        var radiation = world.Radiation;
        for (var i = 0; i < radiation.Length; i++)
        {
            if (radiation[i] != 0)
            {
                hash.Add(i);
                hash.Add((int)radiation[i]);
            }
        }

        // Rules v19 (M3 step 5): the credits of every player. Power and technologies follow from the buildings, not hashed.
        // Rules v21 (step 8): the production queues.
        foreach (var player in world.Players)
        {
            hash.Add(player.Credits);
            // Rules v36 (M4 step 13): the tick of the last notice of an attack.
            hash.Add(player.LastUnderAttack.HasValue);
            hash.Add(player.LastUnderAttack ?? 0);
            // Rules v38 (M5 step 10): the tick the player lost the match on.
            hash.Add(player.DefeatedTick.HasValue);
            hash.Add(player.DefeatedTick ?? 0);
            // Rules v50 (stage 3b): the blackout an infiltrator brought.
            hash.Add(player.BlackoutUntil);

            // Rules v60 (loose ends, stage 3): veterans from a spy.
            hash.Add(player.VeteranInfantry);
            hash.Add(player.VeteranVehicles);
            // Rules v39 (M8 step 2): the bonuses of the player and the hundredths of income it is owed.
            hash.Add(player.Modifiers.IncomeBonusPercent);
            hash.Add(player.Modifiers.BuildSpeedBonusPercent);
            hash.Add(player.Modifiers.RevealMap);
            hash.Add(player.IncomeCarry);
            for (var category = Rules.ProductionCategory.Infantry; category <= Rules.ProductionCategories.Last; category++)
            {
                var queue = player.Queue(category);
                hash.Add(queue.Count);
                foreach (var item in queue)
                {
                    hash.Add((byte)item.Kind);
                    hash.Add(item.DefIndex);
                    hash.Add(item.Progress);
                    hash.Add(item.Paid);
                    hash.Add(item.Paused);
                    hash.Add(item.WaitingForFunds);
                    hash.Add(item.Ready);
                }
            }
        }

        // Rules v35 (M4 step 11): what every player sees — the cells seen now with their counts, and the explored marks.
        for (var player = 1; player <= PlayerId.MaxPlayers; player++)
        {
            AddVision(hash, world.VisionOf(new PlayerId(player)));
        }

        hash.Add(world.Entities.Count);
        foreach (var entity in world.Entities)
        {
            AddEntity(hash, entity);
        }

        return hash.Value;
    }

    private static void AddVision(Fnv1a64 hash, Visibility.VisibilityGrid grid)
    {
        var counts = grid.Counts;
        var seen = 0;
        foreach (var count in counts)
        {
            seen += count != 0 ? 1 : 0;
        }

        hash.Add(seen);
        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] != 0)
            {
                hash.Add(i);
                hash.Add((int)counts[i]);
            }
        }

        foreach (var word in grid.Explored)
        {
            hash.Add(word);
        }

        // The veils of the veil generators (stage 2d): nothing while no cell is veiled, so a match without them hashes as before.
        var veils = grid.Veils;
        for (var i = 0; i < veils.Length; i++)
        {
            if (veils[i] != 0)
            {
                hash.Add(-1 - i);
                hash.Add((int)veils[i]);
            }
        }
    }

    private static void AddWeapons(Fnv1a64 hash, EntityId? target, Combat.WeaponInstance[] weapons)
    {
        hash.Add(target.HasValue);
        hash.Add(target?.Value ?? 0);
        hash.Add(weapons.Length);
        foreach (var weapon in weapons)
        {
            hash.Add(weapon.ReloadLeft);
            hash.Add(weapon.BurstLeft);
            hash.Add(weapon.BurstDelayLeft);
        }
    }

    private static void AddHarvester(Fnv1a64 hash, Harvester harvester)
    {
        hash.Add((byte)harvester.Mode);
        AddCell(hash, harvester.Field);
        AddCell(hash, harvester.Target);
        hash.Add(harvester.Refinery.Value);
        hash.Add(harvester.Preferred.Value);
        hash.Add(harvester.Load);
        hash.Add(harvester.Timer);
        hash.Add(harvester.WaitTicks);
        hash.Add(harvester.WaitingForRefinery);
        hash.Add(harvester.QueueTick);
    }

    private static void AddCell(Fnv1a64 hash, Geometry.CellPos? cell)
    {
        hash.Add(cell.HasValue);
        if (cell is { } value)
        {
            hash.Add(value.X);
            hash.Add(value.Y);
        }
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

                // Rules v26 (M4 step 2): hit points; v27 (step 3): the target and the weapons.
                hash.Add(unit.Hp);
                AddWeapons(hash, unit.Target, unit.Weapons);

                // Rules v31 (M4 step 7): the stance, the orders to attack and the pursuit.
                hash.Add((byte)unit.Stance);
                hash.Add(unit.Anchor.X);
                hash.Add(unit.Anchor.Y);
                hash.Add((byte)unit.TargetOrder);
                hash.Add(unit.TargetPoint.HasValue);
                if (unit.TargetPoint is { } point)
                {
                    hash.Add(point.X);
                    hash.Add(point.Y);
                }

                hash.Add(unit.Engaged);
                AddCell(hash, unit.ChaseCell);

                // Rules v48 (stage 2e): the building the soldier goes into.
                hash.Add(unit.EnterTarget.HasValue);
                hash.Add(unit.EnterTarget?.Value ?? 0);

                // Rules v50 (stage 3b): deployed, phasing in after a jump.
                hash.Add(unit.Deployed);
                hash.Add(unit.WarpTicks);

                // Rules v52 (stage 3d): the passengers, the parasite inside.
                hash.Add(unit.Passengers.Count);
                foreach (var passenger in unit.Passengers)
                {
                    hash.Add((byte)passenger.Type);
                    hash.Add(passenger.Hp);
                    AddWeapons(hash, null, passenger.Weapons);
                }

                hash.Add((byte)unit.Parasite);
                hash.Add(unit.ParasiteOwner.Value);
                hash.Add(unit.ParasiteHp);
                hash.Add(unit.ParasiteWeapon);
                hash.Add(unit.ParasiteTicks);

                // Rules v54 (stage 3f): the ammo of a strike jet.
                hash.Add(unit.Ammo);
                hash.Add(unit.RearmTicks);

                // Rules v55: a carrier and its Hornets.
                hash.Add(unit.Mother.HasValue);
                hash.Add(unit.Mother?.Value ?? 0);
                hash.Add(unit.SpawnsDocked);
                hash.Add(unit.SpawnRegenTicks);

                // Rules v60 (loose ends, stage 3): experience.
                hash.Add(unit.Experience);
                if (unit.Upgrades != CrateUpgrade.None)
                {
                    // M11 a6: only a unit a crate upgraded, so the hashes of the matches without crates stay.
                    hash.Add((byte)unit.Upgrades);
                }

                // Yuri of RA2: only a unit in a mind control, or one that will stand up, so the hashes of the matches without him stay.
                if (unit.ControlledBy is { } holder)
                {
                    hash.Add(1);
                    hash.Add(holder.Value);
                    hash.Add(unit.ControlledFrom.Value);
                }

                if (unit.Controlling is { } held)
                {
                    hash.Add(2);
                    hash.Add(held.Value);
                }

                if (unit.UndeployIn != 0)
                {
                    hash.Add(3);
                    hash.Add(unit.UndeployIn);
                }

                // Rules v57 (loose ends, stage 1): the load of a paradrop plane.
                hash.Add(unit.Drop is not null);
                if (unit.Drop is { } drop)
                {
                    hash.Add((byte)drop.Type);
                    hash.Add(drop.Count);
                    hash.Add(drop.Point.X);
                    hash.Add(drop.Point.Y);
                    hash.Add(drop.Exit.X);
                    hash.Add(drop.Exit.Y);
                }

                // Rules v23 (M3 step 10): the work of a harvester.
                hash.Add(unit.Harvester is not null);
                if (unit.Harvester is { } harvester)
                {
                    AddHarvester(hash, harvester);
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

                // Rules v19 (M3 step 5): the definition of a building of a player; v20 (step 6): the ticks it still grows.
                hash.Add(building.DefIndex);
                hash.Add(building.BuildUpLeft);

                // Rules v24 (step 11): the repair.
                hash.Add(building.Repairing);
                hash.Add(building.RepairCarry);

                // Rules v21 (step 8): the rally point of a factory.
                hash.Add(building.RallyPoint.HasValue);
                if (building.RallyPoint is { } rally)
                {
                    hash.Add(rally.X);
                    hash.Add(rally.Y);
                }

                // Rules v27 (M4 step 3): the target and the weapon of a defense.
                AddWeapons(hash, building.Target, building.Weapons);

                // Rules v56 (stage 2d): the charge of a superweapon.
                hash.Add(building.ChargeLeft);

                // Rules v57 (loose ends, stage 1): the income of a taken oil derrick.
                hash.Add(building.CashTimer);

                // Rules v48 (stage 2e): the soldiers inside.
                hash.Add(building.Occupants.Count);
                foreach (var occupant in building.Occupants)
                {
                    hash.Add((byte)occupant.Type);
                    hash.Add(occupant.Hp);
                    AddWeapons(hash, null, occupant.Weapons);
                }

                break;

            case Projectile projectile:
                // Rules v28 (M4 step 4): a shot in flight.
                hash.Add(ProjectileTag);
                AddCommon(hash, projectile);
                hash.Add(projectile.Shooter.Value);
                hash.Add(projectile.Weapon);
                // Rules v51 (stage 3c): the hit points of a shot anti-air can bring down.
                hash.Add(projectile.Hp);
                hash.Add(projectile.Target.HasValue);
                hash.Add(projectile.Target?.Value ?? 0);
                hash.Add(projectile.Origin.X);
                hash.Add(projectile.Origin.Y);
                hash.Add(projectile.Position.X);
                hash.Add(projectile.Position.Y);
                hash.Add(projectile.AimPoint.X);
                hash.Add(projectile.AimPoint.Y);
                hash.Add(projectile.LastTargetPoint.X);
                hash.Add(projectile.LastTargetPoint.Y);
                hash.Add((int)projectile.Facing.Value);
                hash.Add(projectile.FlightTicks);
                hash.Add(projectile.TicksLeft);
                hash.Add(projectile.LaunchTick);
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

        // Rules v49 (stage 3a): the temporal beam that holds it.
        hash.Add(entity.ErasedBy.HasValue);
        hash.Add(entity.ErasedBy?.Value ?? 0);
        hash.Add(entity.EraseWeapon);
        hash.Add(entity.ErasePoints);

        // Rules v56 (stage 2d): the Bastion Field Projector's cover.
        hash.Add(entity.InvulnerableUntil);

        // Rules v50 (stage 3b): the bomb planted on it.
        hash.Add(entity.BombTicks);
        hash.Add(entity.BombWeapon);
        hash.Add(entity.BombShooter.Value);
        hash.Add(entity.BombOwner.Value);
    }
}
