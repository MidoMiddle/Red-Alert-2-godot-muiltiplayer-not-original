using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The flight of shots (docs/implementation/M4_combat.md, 4.8), after <see cref="WeaponSystem"/> and before the movement, in id order. A
/// shot starts to fly on the tick after it was fired. A ballistic shot goes straight from where it was fired to its aim point and lands
/// there when its flight time is up, whatever became of the target. A homing shot turns toward its target by at most its turn rate a
/// tick and moves its speed along its course; once the target is gone it flies to where it last saw it. It lands within
/// <see cref="HitRadius"/> of that point, or falls where it is when its lifetime is up. A landing is a <see cref="ProjectileImpact"/>;
/// the shot is gone at the end of the tick.
/// </summary>
public static class ProjectileSystem
{
    /// <summary>How near a homing shot must come to its target to hit it.</summary>
    public const int HitRadius = 64;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var database = world.Rules.Database;
        foreach (var entity in world.Entities)
        {
            if (entity is not Projectile { IsAlive: true } projectile || projectile.LaunchTick == world.CurrentTick)
            {
                continue;
            }

            database.TryGetProjectile(database.Weapons[projectile.Weapon].Projectile, out var def);
            if (def.Motion == ProjectileMotion.Homing)
            {
                Homing(world, projectile, def, events);
            }
            else
            {
                Ballistic(world, projectile, events);
            }
        }
    }

    private static void Ballistic(GameWorld world, Projectile projectile, List<SimEvent>? events)
    {
        projectile.TicksLeft--;
        var flown = projectile.FlightTicks - projectile.TicksLeft;
        var (from, to) = (projectile.Origin, projectile.AimPoint);
        projectile.Position = new WorldPos(
            from.X + (int)(((long)(to.X - from.X) * flown) / projectile.FlightTicks),
            from.Y + (int)(((long)(to.Y - from.Y) * flown) / projectile.FlightTicks));
        if (projectile.TicksLeft <= 0)
        {
            Land(world, projectile, projectile.AimPoint, LivingTarget(world, projectile), events);
        }
    }

    private static void Homing(GameWorld world, Projectile projectile, ProjectileDef def, List<SimEvent>? events)
    {
        var target = LivingTarget(world, projectile);
        if (target is not null)
        {
            projectile.LastTargetPoint = Aim.NearestPoint(target, projectile.Position);
        }

        var goal = projectile.LastTargetPoint;
        var wanted = FixedMath.Angle(goal.X - projectile.Position.X, goal.Y - projectile.Position.Y);
        projectile.Facing = BAngle.RotateTowards(projectile.Facing, wanted, def.TurnRate);
        var (x, y) = FixedMath.Direction(projectile.Facing);
        projectile.Position += new WorldPos((int)((x.Raw * def.Speed) >> Fixed.FractionalBits), (int)((y.Raw * def.Speed) >> Fixed.FractionalBits));
        projectile.TicksLeft--;
        if (DamageCalculator.Distance(projectile.Position, goal) <= HitRadius)
        {
            Land(world, projectile, goal, target, events);
        }
        else if (projectile.TicksLeft <= 0)
        {
            Land(world, projectile, projectile.Position, null, events);
        }
    }

    private static Entity? LivingTarget(GameWorld world, Projectile projectile) =>
        projectile.Target is { } id && world.TryGet(id, out var entity) && entity.IsAlive && entity is Unit or Building or Projectile { Hp: > 0 } ? entity : null;

    private static void Land(GameWorld world, Projectile projectile, WorldPos point, Entity? target, List<SimEvent>? events)
    {
        events?.Add(new ProjectileImpact(world.CurrentTick, projectile.Id, projectile.Shooter, projectile.Weapon, point, target?.Id));
        world.Hits.Add(new Hit(projectile.Id, projectile.Shooter, projectile.Owner, projectile.Weapon, point, target?.Id));
        projectile.Position = point;
        projectile.MarkDead();
    }
}
