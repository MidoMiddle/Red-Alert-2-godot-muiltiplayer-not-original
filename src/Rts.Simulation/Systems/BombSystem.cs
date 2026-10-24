using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Movement;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Bombs and blasts (stage 3b of filling the game), after the movement and the soldiers going into buildings. A shot whose warhead plants
/// a bomb (<see cref="WarheadDef.Bomb"/>: Ivan's bomb, the commando's C4) hurts nothing: it puts the bomb on a unit or building that has
/// none — the planter forgets its target and moves on — and the bomb goes off after <see cref="WarheadDef.BombDelay"/> ticks, or at once
/// when what it is on falls first: a shot of the bomb's weapon lands on its place the next tick, at it while it stands, as the planter's.
/// The weapon of a unit that dies (<see cref="UnitDef.DeathWeapon"/>: the demolition trooper) goes off the same way where it died; those
/// deaths are gathered in <see cref="GameWorld.DeathBlasts"/> during the tick, as a death may come while the entities are walked.
/// </summary>
public static class BombSystem
{
    /// <summary>A bomb shot lands on its target: the bomb is planted, unless one is there already.</summary>
    internal static void Plant(GameWorld world, Hit hit, WarheadDef warhead, List<SimEvent>? events)
    {
        if (hit.Target is not { } id || !world.TryGet(id, out var target) || target is not { IsAlive: true, BombTicks: 0 } and (Unit or Building)
            || target is Building { Def.Wall: true })
        {
            return;
        }

        target.BombTicks = warhead.BombDelay;
        target.BombWeapon = world.Rules.Database.IndexOfWeapon(warhead.Bomb);
        target.BombShooter = hit.Shooter;
        target.BombOwner = hit.Owner;
        events?.Add(new BombPlanted(world.CurrentTick, target.Id, hit.Shooter, warhead.BombDelay));
        if (world.TryGet(hit.Shooter, out var shooter) && shooter is Unit planter && planter.Target == target.Id
            && planter.TargetOrder != TargetOrder.Force)
        {
            UnitOrders.ForgetTarget(planter);
        }
    }

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var (owner, shooter, weapon, point) in world.DeathBlasts)
        {
            GoOff(world, owner, shooter, weapon, point, null);
        }

        world.DeathBlasts.Clear();
        List<Entity>? due = null;
        foreach (var entity in world.Entities)
        {
            if (entity.BombTicks > 0 && (!entity.IsAlive || --entity.BombTicks == 0))
            {
                (due ??= []).Add(entity);
            }
        }

        foreach (var entity in due ?? [])
        {
            var point = entity is Building building ? Aim.CentreOf(building) : ((Unit)entity).Position;
            GoOff(world, entity.BombOwner, entity.BombShooter, entity.BombWeapon, point, entity.IsAlive ? entity.Id : null);
            entity.BombTicks = 0;
            entity.BombWeapon = -1;
            entity.BombShooter = EntityId.None;
            entity.BombOwner = PlayerId.Neutral;
        }
    }

    /// <summary>Whether every weapon among <paramref name="weapons"/> plants a bomb: such a unit picks no target that has one.</summary>
    internal static bool PlantsOnly(GameRules rules, System.Collections.Immutable.ImmutableArray<int> weapons)
    {
        var any = false;
        foreach (var weapon in weapons)
        {
            if (weapon < 0)
            {
                continue;
            }

            if (rules.Database.Warheads[rules.Database.IndexOfWarhead(rules.Database.Weapons[weapon].Warhead)].Bomb.Length == 0)
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    private static void GoOff(GameWorld world, PlayerId owner, EntityId shooter, int weapon, WorldPos point, EntityId? target)
    {
        if (weapon < 0)
        {
            return;
        }

        var shot = world.Launch(owner, shooter, weapon, target, point, point);
        shot.FlightTicks = 1;
        shot.TicksLeft = 1;
    }
}
