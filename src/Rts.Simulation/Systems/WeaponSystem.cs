using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Aiming and firing (docs/implementation/M4_combat.md, 4.6, 4.7), after the repairs and before the movement. For every unit and defense
/// with a weapon, in id order: the reloads count down, with or without a target; then, with a living target, the first weapon that hits
/// ground and reaches it is aimed — the turret turns at its rate, a unit without one turns its hull while it stands, infantry and
/// defenses at once — and fires when it points within <see cref="AimTolerance"/> and is ready: a volley of <c>burst</c> shots
/// <c>burst_delay</c> apart, the next volley <c>reload</c> after the first shot (<see cref="WeaponFired"/>). The shots of the tick are
/// launched after the walk over the entities (step 4): an instant one lands at once (<see cref="ProjectileImpact"/>), a ballistic or
/// homing one becomes a <see cref="Projectile"/> that <see cref="ProjectileSystem"/> flies. A unit forced to attack the ground (step 7)
/// aims and fires at <see cref="World.Unit.TargetPoint"/> the same way; its shots land there with no target.
/// </summary>
public static class WeaponSystem
{
    /// <summary>How far off the target a weapon may point and still fire: 8 of 256 parts of a turn (RA2).</summary>
    public const int AimTolerance = BAngle.FullTurn * 8 / 256;

    /// <summary>A shot of this tick, launched after the walk: the list of entities must not change while it is walked.</summary>
    private readonly record struct Shot(PlayerId Owner, EntityId Shooter, int Weapon, EntityId? Target, WorldPos Origin, WorldPos AimAt);

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var rules = world.Rules;
        var shots = new List<Shot>();
        foreach (var entity in world.Entities)
        {
            switch (entity)
            {
                case Unit { IsAlive: true } unit when unit.Weapons.Length > 0:
                    Unit(world, rules, unit, shots, events);
                    break;
                case Building { IsAlive: true, IsReady: true } building when building.Weapons.Length > 0:
                    Defense(world, rules, building, shots, events);
                    break;
            }
        }

        foreach (var shot in shots)
        {
            Launch(world, rules, shot, events);
        }
    }

    /// <summary>
    /// Sends a shot on its way (M4, 4.8): an instant one lands now at the nearest point of the target; a ballistic one flies to that
    /// point scattered by up to <c>inaccuracy</c> (a random angle and distance, in the order of the shooters), in as many ticks as its
    /// speed needs, rounded up; a homing one heads for the target for its <c>lifetime</c>.
    /// </summary>
    private static void Launch(GameWorld world, GameRules rules, Shot shot, List<SimEvent>? events)
    {
        var weapon = rules.Database.Weapons[shot.Weapon];
        rules.Database.TryGetProjectile(weapon.Projectile, out var projectile);
        switch (projectile.Motion)
        {
            case ProjectileMotion.Instant:
                events?.Add(new ProjectileImpact(world.CurrentTick, null, shot.Shooter, shot.Weapon, shot.AimAt, shot.Target));
                world.Hits.Add(new Hit(shot.Shooter, shot.Shooter, shot.Owner, shot.Weapon, shot.AimAt, shot.Target));
                break;
            case ProjectileMotion.Ballistic:
                {
                    var aimPoint = shot.AimAt + Scatter(world, Aim.Leptons(projectile.Inaccuracy));
                    var flying = world.Launch(shot.Owner, shot.Shooter, shot.Weapon, shot.Target, shot.Origin, aimPoint);
                    var distance = DamageCalculator.Distance(shot.Origin, aimPoint);
                    flying.FlightTicks = IntMath.Max(1, (distance + projectile.Speed - 1) / projectile.Speed);
                    flying.TicksLeft = flying.FlightTicks;
                    break;
                }

            default:
                {
                    var flying = world.Launch(shot.Owner, shot.Shooter, shot.Weapon, shot.Target, shot.Origin, shot.AimAt);
                    flying.Facing = FixedMath.Angle(shot.AimAt.X - shot.Origin.X, shot.AimAt.Y - shot.Origin.Y);
                    flying.TicksLeft = projectile.Lifetime;
                    break;
                }
        }
    }

    /// <summary>An offset of a random direction and a random length up to <paramref name="radius"/> leptons.</summary>
    private static WorldPos Scatter(GameWorld world, int radius)
    {
        if (radius <= 0)
        {
            return WorldPos.Zero;
        }

        var angle = BAngle.FromUnits(world.Random.NextInt(BAngle.FullTurn));
        var length = world.Random.NextInt(radius + 1);
        var (x, y) = FixedMath.Direction(angle);
        return new WorldPos((int)((x.Raw * length) >> Fixed.FractionalBits), (int)((y.Raw * length) >> Fixed.FractionalBits));
    }

    private static void Unit(GameWorld world, GameRules rules, Unit unit, List<Shot> shots, List<SimEvent>? events)
    {
        CountDown(unit.Weapons);
        var archetype = unit.Archetype;
        var hasTarget = TargetOf(world, unit.Target, out var target);
        if (!hasTarget && unit.TargetPoint is null)
        {
            unit.Target = null;
            StopVolleys(unit.Weapons);
            // Without a target the turret comes back to the hull.
            if (archetype.HasTurret && unit.TurretFacing != unit.Facing)
            {
                unit.TurretFacing = Turn(unit.TurretFacing, unit.Facing, archetype.TurretTurnRate);
            }

            return;
        }

        var aimAt = hasTarget ? Aim.NearestPoint(target, unit.Position) : unit.TargetPoint!.Value;
        var wanted = FixedMath.Angle(aimAt.X - unit.Position.X, aimAt.Y - unit.Position.Y);
        bool aimed;
        if (archetype.HasTurret)
        {
            unit.TurretFacing = Turn(unit.TurretFacing, wanted, archetype.TurretTurnRate);
            aimed = PointsAt(unit.TurretFacing, wanted);
        }
        else
        {
            // Without a turret the hull aims, and only while the unit stands: driving it points where it goes.
            if (unit.MoveTarget is null)
            {
                unit.Facing = Turn(unit.Facing, wanted, archetype.TurnRate);
                unit.TurretFacing = unit.Facing;
            }

            aimed = PointsAt(unit.Facing, wanted);
        }

        var distance = DamageCalculator.Distance(unit.Position, aimAt);
        int[] slots = [archetype.Primary, archetype.Secondary];
        EntityId? targetId = hasTarget ? target.Id : null;
        if (Fire(world, rules, unit.Id, targetId, unit.Weapons, slots, distance, aimed, events) is { } weapon)
        {
            shots.Add(new Shot(unit.Owner, unit.Id, weapon, targetId, unit.Position, aimAt));
        }
    }

    private static void Defense(GameWorld world, GameRules rules, Building building, List<Shot> shots, List<SimEvent>? events)
    {
        CountDown(building.Weapons);
        if (!TargetOf(world, building.Target, out var target))
        {
            building.Target = null;
            StopVolleys(building.Weapons);
            return;
        }

        // A defense aims at once.
        var centre = Aim.CentreOf(building);
        var aimAt = Aim.NearestPoint(target, centre);
        var distance = DamageCalculator.Distance(centre, aimAt);
        int[] slots = [rules.WeaponOfBuilding(building.DefIndex)];
        if (Fire(world, rules, building.Id, target.Id, building.Weapons, slots, distance, aimed: true, events) is { } weapon)
        {
            shots.Add(new Shot(building.Owner, building.Id, weapon, target.Id, centre, aimAt));
        }
    }

    /// <summary>
    /// Fires the first weapon that hits ground and reaches <paramref name="distance"/>, when it is aimed and ready; the volley of a
    /// weapon that cannot fire now is broken off. Gives the place of the weapon that fired in the rules, or null.
    /// </summary>
    private static int? Fire(
        GameWorld world, GameRules rules, EntityId shooter, EntityId? target, WeaponInstance[] weapons, int[] slots, int distance, bool aimed,
        List<SimEvent>? events)
    {
        var chosen = -1;
        for (var slot = 0; slot < weapons.Length && chosen < 0; slot++)
        {
            if (slots[slot] >= 0 && Reaches(rules.Database.Weapons[slots[slot]], distance))
            {
                chosen = slot;
            }
        }

        for (var slot = 0; slot < weapons.Length; slot++)
        {
            if (slot != chosen)
            {
                weapons[slot].BurstLeft = 0;
            }
        }

        if (chosen < 0 || !aimed)
        {
            return null;
        }

        var weapon = rules.Database.Weapons[slots[chosen]];
        var state = weapons[chosen];
        if (state.BurstLeft > 0 && state.BurstDelayLeft == 0)
        {
            state.BurstLeft--;
            state.BurstDelayLeft = weapon.BurstDelay;
        }
        else if (state.BurstLeft == 0 && state.ReloadLeft == 0)
        {
            state.ReloadLeft = weapon.Reload;
            state.BurstLeft = weapon.Burst - 1;
            state.BurstDelayLeft = weapon.BurstDelay;
        }
        else
        {
            return null;
        }

        events?.Add(new WeaponFired(world.CurrentTick, shooter, target, chosen));
        return slots[chosen];
    }

    /// <summary>Whether one of the weapons hits ground targets and reaches <paramref name="distance"/> leptons.</summary>
    internal static bool Reaches(GameRules rules, int[] weapons, int distance)
    {
        foreach (var weapon in weapons)
        {
            if (weapon >= 0 && Reaches(rules.Database.Weapons[weapon], distance))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the weapon hits ground targets and <paramref name="distance"/> leptons is within its range.</summary>
    private static bool Reaches(WeaponDef weapon, int distance) =>
        (weapon.Targets & WeaponTargets.Ground) != 0 && distance <= Aim.Leptons(weapon.Range) && distance >= Aim.Leptons(weapon.MinRange);

    private static void CountDown(WeaponInstance[] weapons)
    {
        foreach (var weapon in weapons)
        {
            weapon.ReloadLeft = IntMath.Max(0, weapon.ReloadLeft - 1);
            weapon.BurstDelayLeft = IntMath.Max(0, weapon.BurstDelayLeft - 1);
        }
    }

    private static void StopVolleys(WeaponInstance[] weapons)
    {
        foreach (var weapon in weapons)
        {
            weapon.BurstLeft = 0;
        }
    }

    /// <summary>A target that still stands: a living unit or building.</summary>
    private static bool TargetOf(GameWorld world, EntityId? id, out Entity target)
    {
        if (id is { } targetId && world.TryGet(targetId, out var entity) && entity is { IsAlive: true } and (World.Unit or Building))
        {
            target = entity;
            return true;
        }

        target = null!;
        return false;
    }

    private static BAngle Turn(BAngle current, BAngle wanted, int rate) => rate == 0 ? wanted : BAngle.RotateTowards(current, wanted, rate);

    private static bool PointsAt(BAngle facing, BAngle wanted) => IntMath.Abs(BAngle.Difference(facing, wanted)) <= AimTolerance;
}
