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
/// <c>burst_delay</c> apart, the next volley <c>reload</c> after the first shot. A shot is a <see cref="WeaponFired"/> event; the
/// projectile and the damage come in steps 4 and 5.
/// </summary>
public static class WeaponSystem
{
    /// <summary>How far off the target a weapon may point and still fire: 8 of 256 parts of a turn (RA2).</summary>
    public const int AimTolerance = BAngle.FullTurn * 8 / 256;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var rules = world.Rules;
        foreach (var entity in world.Entities)
        {
            switch (entity)
            {
                case Unit { IsAlive: true } unit when unit.Weapons.Length > 0:
                    Unit(world, rules, unit, events);
                    break;
                case Building { IsAlive: true, IsReady: true } building when building.Weapons.Length > 0:
                    Defense(world, rules, building, events);
                    break;
            }
        }
    }

    private static void Unit(GameWorld world, GameRules rules, Unit unit, List<SimEvent>? events)
    {
        CountDown(unit.Weapons);
        var archetype = unit.Archetype;
        if (!TargetOf(world, unit.Target, out var target))
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

        var aimAt = NearestPoint(target, unit.Position);
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
        Fire(world, rules, unit.Id, target, unit.Weapons, slots, distance, aimed, events);
    }

    private static void Defense(GameWorld world, GameRules rules, Building building, List<SimEvent>? events)
    {
        CountDown(building.Weapons);
        if (!TargetOf(world, building.Target, out var target))
        {
            building.Target = null;
            StopVolleys(building.Weapons);
            return;
        }

        // A defense aims at once.
        var centre = CentreOf(building);
        var distance = DamageCalculator.Distance(centre, NearestPoint(target, centre));
        int[] slots = [rules.WeaponOfBuilding(building.DefIndex)];
        Fire(world, rules, building.Id, target, building.Weapons, slots, distance, aimed: true, events);
    }

    /// <summary>
    /// Fires the first weapon that hits ground and reaches <paramref name="distance"/>, when it is aimed and ready; the volley of a
    /// weapon that cannot fire now is broken off.
    /// </summary>
    private static void Fire(
        GameWorld world, GameRules rules, EntityId shooter, Entity target, WeaponInstance[] weapons, int[] slots, int distance, bool aimed,
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
            return;
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
            return;
        }

        events?.Add(new WeaponFired(world.CurrentTick, shooter, target.Id, chosen));
    }

    /// <summary>Whether the weapon hits ground targets and <paramref name="distance"/> leptons is within its range.</summary>
    private static bool Reaches(WeaponDef weapon, int distance) =>
        (weapon.Targets & WeaponTargets.Ground) != 0 && distance <= Leptons(weapon.Range) && distance >= Leptons(weapon.MinRange);

    private static long Leptons(Fixed cells) => (cells.Raw * WorldPos.LeptonsPerCell) >> Fixed.FractionalBits;

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

    /// <summary>The point of the target nearest to <paramref name="from"/>: a unit's position, the nearest point of a building's footprint.</summary>
    private static WorldPos NearestPoint(Entity target, WorldPos from)
    {
        if (target is not Building building)
        {
            return ((World.Unit)target).Position;
        }

        var left = building.Cell.X * WorldPos.LeptonsPerCell;
        var top = building.Cell.Y * WorldPos.LeptonsPerCell;
        var right = ((building.Cell.X + building.Width) * WorldPos.LeptonsPerCell) - 1;
        var bottom = ((building.Cell.Y + building.Height) * WorldPos.LeptonsPerCell) - 1;
        return new WorldPos(IntMath.Clamp(from.X, left, right), IntMath.Clamp(from.Y, top, bottom));
    }

    private static WorldPos CentreOf(Building building) =>
        new(
            (building.Cell.X * WorldPos.LeptonsPerCell) + (building.Width * WorldPos.LeptonsPerCell / 2),
            (building.Cell.Y * WorldPos.LeptonsPerCell) + (building.Height * WorldPos.LeptonsPerCell / 2));

    private static BAngle Turn(BAngle current, BAngle wanted, int rate) => rate == 0 ? wanted : BAngle.RotateTowards(current, wanted, rate);

    private static bool PointsAt(BAngle facing, BAngle wanted) => IntMath.Abs(BAngle.Difference(facing, wanted)) <= AimTolerance;
}
