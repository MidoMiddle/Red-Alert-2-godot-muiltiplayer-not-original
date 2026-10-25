using System.Collections.Generic;
using System.Collections.Immutable;
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
/// aims and fires at <see cref="World.Unit.TargetPoint"/> the same way; its shots land there with no target. A defense off for want of
/// power (<see cref="Defenses.IsOnline"/>, step 10) does not fire; its reload counts down.
/// <para>
/// Stage 2e: the soldiers inside a building fire each with its weapons from the centre of the building at the building's target, their
/// range counted from its wall (<see cref="GarrisonSystem"/>). When a tower whose kind supports (<see cref="BuildingDef.ChainBonusPercent"/>:
/// the lens tower) fires, the idle towers of its kind and owner — ready, online, without a target, reloaded — that have it within their
/// range send it their charge, up to <see cref="BuildingDef.ChainMax"/> in id order: each starts its reload and adds its bonus to the shot
/// (<see cref="TowerSupported"/>).
/// </para>
/// </summary>
public static class WeaponSystem
{
    /// <summary>How far off the target a weapon may point and still fire: 8 of 256 parts of a turn (RA2).</summary>
    public const int AimTolerance = BAngle.FullTurn * 8 / 256;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var rules = world.Rules;
        var shots = world.Shots;
        shots.Clear();
        foreach (var entity in world.Entities)
        {
            switch (entity)
            {
                // Held by a temporal beam (stage 3a), nothing fires.
                case Unit { IsAlive: true, ErasedBy: null, WarpTicks: 0 } unit when unit.Weapons.Length > 0:
                    Unit(world, rules, unit, shots, events);
                    break;
                case Building { IsAlive: true, IsReady: true, ErasedBy: null } building when building.Weapons.Length > 0:
                    Defense(world, rules, building, shots, events);
                    break;
                case Building { IsAlive: true, IsReady: true, ErasedBy: null } building when building.Occupants.Count > 0:
                    Garrison(world, rules, building, shots, events);
                    break;
            }
        }

        foreach (var shot in shots)
        {
            // A suicide weapon (stage 3b) is no shot: firing it, the shooter dies, and its death weapon goes off.
            if (rules.Database.Weapons[shot.Weapon].Suicide && world.TryGet(shot.Shooter, out var shooter) && shooter is World.Unit { IsAlive: true } bomber)
            {
                DeathSystem.Kill(world, bomber, bomber.Id, bomber.Owner, Events.DeathCause.Weapon, shot.Weapon, events);
                continue;
            }

            // A carrier's weapon launches the units it carries at the target instead of a shot.
            if (rules.Database.Weapons[shot.Weapon].LaunchesSpawns && world.TryGet(shot.Shooter, out var launcher) && launcher is World.Unit carrier)
            {
                AmmoSystem.Launch(world, carrier, shot.Target, events);
                continue;
            }

            Launch(world, rules, shot, events);
        }

        shots.Clear();
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
                world.Hits.Add(new Hit(shot.Shooter, shot.Shooter, shot.Owner, shot.Weapon, shot.AimAt, shot.Target, shot.BonusPercent));
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
        var slots = hasTarget ? rules.WeaponsOf(unit) : BridgeDemolition.WeaponsAt(world, rules.WeaponsOf(unit), aimAt);

        // Out of ammo (stage 3f: a strike jet), a unit fires out the volley under way and starts no other.
        if (archetype.Ammo > 0 && unit.Ammo == 0 && !InVolley(unit.Weapons))
        {
            return;
        }

        EntityId? targetId = hasTarget ? target.Id : null;
        var layer = hasTarget ? TargetingSystem.LayerOf(target) : WeaponTargets.Ground;
        if (Fire(world, rules, unit.Id, targetId, unit.Weapons, slots, distance, aimed, layer, events, hasTarget ? target : null) is { } weapon)
        {
            if (archetype.Ammo > 0 && StartedVolley(rules, unit.Weapons, slots, weapon))
            {
                unit.Ammo--;
            }

            shots.Add(new Shot(unit.Owner, unit.Id, weapon, targetId, unit.Position, aimAt));
        }
    }

    private static void Defense(GameWorld world, GameRules rules, Building building, List<Shot> shots, List<SimEvent>? events)
    {
        CountDown(building.Weapons);
        if (!Defenses.IsOnline(world, building) || !TargetOf(world, building.Target, out var target))
        {
            building.Target = null;
            StopVolleys(building.Weapons);
            return;
        }

        // A defense aims at once.
        var centre = Aim.CentreOf(building);
        var aimAt = Aim.NearestPoint(target, centre);
        var distance = DamageCalculator.Distance(centre, aimAt);
        var slots = Defenses.WeaponsOf(world, building);
        if (Fire(world, rules, building.Id, target.Id, building.Weapons, slots, distance, aimed: true, TargetingSystem.LayerOf(target), events, target) is { } weapon)
        {
            var bonus = building.Def is { ChainBonusPercent: > 0 } def ? Support(world, building, def, events) * def.ChainBonusPercent : 0;
            shots.Add(new Shot(building.Owner, building.Id, weapon, target.Id, centre, aimAt, bonus));
        }
    }

    /// <summary>The idle towers of the kind and owner of <paramref name="tower"/> that send it their charge; how many did.</summary>
    private static int Support(GameWorld world, Building tower, BuildingDef def, List<SimEvent>? events)
    {
        var rules = world.Rules;
        var centre = Aim.CentreOf(tower);
        var supporters = 0;
        foreach (var entity in world.Entities)
        {
            if (supporters >= def.ChainMax)
            {
                break;
            }

            if (entity is not Building { IsAlive: true, IsReady: true, Target: null } other || other == tower || other.Owner != tower.Owner
                || other.DefIndex != tower.DefIndex || other.Weapons is not [{ ReloadLeft: 0, BurstLeft: 0 } state, ..]
                || !Defenses.IsOnline(world, other) || rules.WeaponsOfBuilding(other.DefIndex)[0] is not (>= 0 and var slot))
            {
                continue;
            }

            var weapon = rules.Database.Weapons[slot];
            if (DamageCalculator.Distance(Aim.CentreOf(other), centre) <= Aim.Leptons(weapon.Range))
            {
                state.ReloadLeft = weapon.Reload;
                supporters++;
                events?.Add(new TowerSupported(world.CurrentTick, other.Id, tower.Id));
            }
        }

        return supporters;
    }

    private static void Garrison(GameWorld world, GameRules rules, Building building, List<Shot> shots, List<SimEvent>? events)
    {
        foreach (var occupant in building.Occupants)
        {
            CountDown(occupant.Weapons);
        }

        if (!TargetOf(world, building.Target, out var target))
        {
            building.Target = null;
            foreach (var occupant in building.Occupants)
            {
                StopVolleys(occupant.Weapons);
            }

            return;
        }

        // The soldiers aim at once, from the centre; their range starts at the wall.
        var centre = Aim.CentreOf(building);
        var aimAt = Aim.NearestPoint(target, centre);
        var distance = IntMath.Max(0, DamageCalculator.Distance(centre, aimAt) - GarrisonSystem.Wall(building));
        var layer = TargetingSystem.LayerOf(target);
        foreach (var occupant in building.Occupants)
        {
            if (occupant.Weapons.Length > 0
                && Fire(world, rules, building.Id, target.Id, occupant.Weapons, rules.GarrisonWeaponsOf(occupant.Type), distance, aimed: true, layer, events, target) is { } weapon)
            {
                shots.Add(new Shot(building.Owner, building.Id, weapon, target.Id, centre, aimAt));
            }
        }
    }

    /// <summary>
    /// Fires the first weapon that fires at <paramref name="layer"/> — the ground, or the air at a flier (stage 2b) — reaches
    /// <paramref name="distance"/> and has a share against <paramref name="victim"/> (stage 3b: the commando's pistols leave a building
    /// to her C4), when it is aimed and ready; the volley of a weapon that cannot fire now is broken off. Gives the place of the weapon
    /// that fired in the rules, or null.
    /// </summary>
    private static int? Fire(
        GameWorld world, GameRules rules, EntityId shooter, EntityId? target, WeaponInstance[] weapons, ImmutableArray<int> slots, int distance, bool aimed,
        WeaponTargets layer, List<SimEvent>? events, Entity? victim = null)
    {
        var chosen = -1;
        for (var slot = 0; slot < weapons.Length && chosen < 0; slot++)
        {
            if (slots[slot] >= 0 && Reaches(rules.Database.Weapons[slots[slot]], distance, layer)
                && (victim is null || TargetingSystem.BestShare(rules, ImmutableArray.Create(slots[slot]), victim) > 0))
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
            state.ReloadLeft = Veterancy.Reload(world, shooter, weapon.Reload);
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

    /// <summary>Whether one of the weapons fires at <paramref name="layer"/> and reaches <paramref name="distance"/> leptons.</summary>
    internal static bool Reaches(GameRules rules, ImmutableArray<int> weapons, int distance, WeaponTargets layer, Entity? victim = null)
    {
        foreach (var weapon in weapons)
        {
            // With a victim, only a weapon that can hurt it counts (stage 3b: the commando walks up to a building for her C4).
            if (weapon >= 0 && Reaches(rules.Database.Weapons[weapon], distance, layer)
                && (victim is null || TargetingSystem.BestShare(rules, ImmutableArray.Create(weapon), victim) > 0))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The least range, in leptons, of the weapons that fire at <paramref name="layer"/>, can hurt <paramref name="victim"/> and reach
    /// <paramref name="distance"/> but for being too near; null when none is too near (the rocket artillery, the dreadnought).
    /// </summary>
    internal static int? TooNear(GameRules rules, ImmutableArray<int> weapons, int distance, WeaponTargets layer, Entity victim)
    {
        int? least = null;
        foreach (var index in weapons)
        {
            if (index < 0)
            {
                continue;
            }

            var weapon = rules.Database.Weapons[index];
            var minimum = Aim.Leptons(weapon.MinRange);
            if ((weapon.Targets & layer) != 0 && minimum > 0 && distance < minimum
                && TargetingSystem.BestShare(rules, ImmutableArray.Create(index), victim) > 0)
            {
                least = IntMath.Max(least ?? 0, minimum);
            }
        }

        return least;
    }

    /// <summary>Whether the weapon fires at <paramref name="layer"/> and <paramref name="distance"/> leptons is within its range.</summary>
    private static bool Reaches(WeaponDef weapon, int distance, WeaponTargets layer) =>
        (weapon.Targets & layer) != 0 && distance <= Aim.Leptons(weapon.Range) && distance >= Aim.Leptons(weapon.MinRange);

    // Whether a volley of one of the weapons is under way.
    private static bool InVolley(WeaponInstance[] weapons)
    {
        foreach (var weapon in weapons)
        {
            if (weapon.BurstLeft > 0)
            {
                return true;
            }
        }

        return false;
    }

    // Whether the shot of the weapon just fired started its volley: its reload and its volley are whole.
    private static bool StartedVolley(GameRules rules, WeaponInstance[] weapons, ImmutableArray<int> slots, int weapon)
    {
        var def = rules.Database.Weapons[weapon];
        for (var slot = 0; slot < weapons.Length; slot++)
        {
            if (slots[slot] == weapon)
            {
                return weapons[slot].ReloadLeft == def.Reload && weapons[slot].BurstLeft == def.Burst - 1;
            }
        }

        return false;
    }

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
        if (id is { } targetId && world.TryGet(targetId, out var entity) && entity is { IsAlive: true } and (World.Unit or Building or Projectile { Hp: > 0 }))
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
