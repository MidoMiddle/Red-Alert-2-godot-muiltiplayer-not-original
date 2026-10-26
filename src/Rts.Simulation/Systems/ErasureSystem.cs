using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Erasure from time (stage 3a of filling the game: the time trooper, RA2 <c>Temporal</c> warheads), after <see cref="DamageSystem"/>.
/// A temporal shot hurts nothing: it lays its shooter's beam on the unit or building it hit (<see cref="Entity.ErasedBy"/>), unless
/// another beam holds it already. Every tick the beam holds, the target gathers the weapon's damage as erasure; at
/// <see cref="HitPointsTimes"/> times its full hit points it is gone, as killed by the shooter's weapon. The beam holds while the
/// shooter lives, is not held itself, keeps the target as its target and has it within its weapon's range; else it lets go and the
/// target comes back whole, its erasure forgotten. A held target stands still, does not fire, looks for no target and takes no other
/// damage; no one else picks it as a target.
/// </summary>
public static class ErasureSystem
{
    /// <summary>The erasure that removes a target, in its full hit points (RA2, by ModEnc).</summary>
    public const int HitPointsTimes = 10;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var rules = world.Rules.Database;
        List<(Entity Target, Unit Shooter)>? erased = null;
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity.ErasedBy is not { } id)
            {
                continue;
            }

            if (!world.TryGet(id, out var holder) || holder is not Unit { IsAlive: true, ErasedBy: null } shooter || shooter.Target != entity.Id
                || DamageCalculator.Distance(shooter.Position, Aim.NearestPoint(entity, shooter.Position)) > Aim.Leptons(rules.Weapons[entity.EraseWeapon].Range))
            {
                Release(entity);
                continue;
            }

            entity.ErasePoints += rules.Weapons[entity.EraseWeapon].Damage;
            if (entity.ErasePoints >= HitPointsTimes * MaxHpOf(entity))
            {
                (erased ??= []).Add((entity, shooter));
            }
        }

        // Gone after the walk: a building that goes may let its soldiers out, and the list of entities must not change while walked.
        foreach (var (target, shooter) in erased ?? [])
        {
            var weapon = target.EraseWeapon;
            Release(target);
            switch (target)
            {
                case Unit unit:
                    DeathSystem.Erase(world, unit, shooter.Id, shooter.Owner, weapon, events);
                    break;
                case Building building:
                    DeathSystem.Erase(world, building, shooter.Id, shooter.Owner, weapon, events);
                    break;
            }
        }
    }

    /// <summary>
    /// A temporal shot of <paramref name="hit"/>'s shooter lands on its target: the beam holds it, unless another beam holds it already.
    /// </summary>
    internal static void Hold(GameWorld world, Hit hit)
    {
        if (hit.Target is { } id && world.TryGet(id, out var target) && target is { IsAlive: true } and (Unit or Building)
            && target is not Building { Def.Wall: true } && (target.ErasedBy is null || target.ErasedBy == hit.Shooter)
            && !target.IsInvulnerable(world.CurrentTick))
        {
            target.ErasedBy = hit.Shooter;
            target.EraseWeapon = hit.Weapon;
        }
    }

    private static void Release(Entity entity)
    {
        entity.ErasedBy = null;
        entity.EraseWeapon = -1;
        entity.ErasePoints = 0;
    }

    private static int MaxHpOf(Entity entity) => entity switch
    {
        Unit unit => unit.MaxHp,
        Building building => building.MaxHp,
        _ => 1,
    };
}
