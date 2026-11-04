using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The shot of the player inside a unit (the owner, 2026-11-04: aiming as in a shooter; the ray and its spread, a shot in the head kills
/// at once). From the unit's eyes — its place, the height of the ground there and <see cref="EyeHeight"/> — a ray goes along the
/// direction of the command, turned a little at random within the <see cref="WeaponDef.AimSpread"/> of its weapon (twice that on the
/// move), as far as the weapon reaches. Whatever it meets first takes the shot: a unit — an upright cylinder, a soldier's head the top of
/// it —, a building — a box over its footprint —, or the ground; anyone's, the shooter's own and the civilians too. A hit in the head of
/// a soldier kills it (<see cref="Hit.Lethal"/>); a shot that meets nobody falls on the ground where the ray does, or at its end.
/// The damage is the weapon's own, through <see cref="DamageSystem"/>. Only a unit under the player's hand (<see cref="Unit.Manual"/>),
/// whose first weapon is instant and loaded, fires; its weapon then reloads as after any shot. Integer leptons throughout: the same
/// on every machine.
/// </summary>
public static class AimedShot
{
    /// <summary>The eyes over the ground, in leptons: half a cell, where the camera of the client stands.</summary>
    public const int EyeHeight = 128;

    /// <summary>A soldier: the radius and the height of its cylinder and the top of it that is its head, in leptons.</summary>
    public const int SoldierRadius = 40;

    public const int SoldierHeight = 140;

    public const int HeadHeight = 28;

    /// <summary>A vehicle, a ship: the radius and the height of its cylinder, in leptons.</summary>
    public const int VehicleRadius = 110;

    public const int VehicleHeight = 100;

    /// <summary>A building stands this high over its footprint, in leptons: two cells, as it stands from the ground in the client.</summary>
    public const int BuildingHeight = 512;

    // The step along the ray at which the ground is looked at, in leptons.
    private const int GroundStep = 32;

    // A direction is kept in units of 1/65536, as Fixed is.
    private const long One = Fixed.OneRaw;

    // How far up or down a shot may go: a little short of straight.
    private const int MaxPitch = 16000;

    /// <summary>Fires the unit of the command; false when it may not fire now.</summary>
    public static bool Fire(GameWorld world, AimedShotCommand command, List<SimEvent>? events)
    {
        if (!world.TryGet(command.Unit, out var entity) || entity is not Unit { IsAlive: true, Manual: true } unit || unit.Owner != command.Player
            || unit.ErasedBy is not null || unit.WarpTicks > 0 || unit.Weapons.Length == 0)
        {
            return false;
        }

        var rules = world.Rules;
        var slots = rules.WeaponsOf(unit);
        if (slots.Length == 0 || slots[0] < 0)
        {
            return false;
        }

        var weaponIndex = slots[0];
        var weapon = rules.Database.Weapons[weaponIndex];
        var state = unit.Weapons[0];
        if (state.ReloadLeft > 0 || !rules.Database.TryGetProjectile(weapon.Projectile, out var projectile)
            || projectile.Motion != ProjectileMotion.Instant)
        {
            return false;
        }

        // The spread: a turn either way of up to the weapon's, twice that on the move; from the generator of the match, so every
        // machine draws the same.
        var spread = (int)((weapon.AimSpread.Raw * BAngle.FullTurn / 360) >> 16) * (unit.MoveTarget is null && !unit.PathPending ? 1 : 2);
        var yaw = new BAngle((ushort)(command.Yaw.Value + Offset(world, spread)));
        var pitch = new BAngle((ushort)IntMath.Clamp(command.Pitch + Offset(world, spread), -MaxPitch, MaxPitch));

        var cosPitch = FixedMath.Cos(pitch).Raw;
        var (sinYaw, minusCosYaw) = FixedMath.Direction(yaw);
        var direction = (X: sinYaw.Raw * cosPitch / One, Y: minusCosYaw.Raw * cosPitch / One, Z: FixedMath.Sin(pitch).Raw);
        var origin = (X: (long)unit.Position.X, Y: (long)unit.Position.Y, Z: Ground(world, unit.Position) + EyeHeight);
        long reach = Aim.Leptons(weapon.Range);

        var (along, hit, head) = Trace(world, unit, origin, direction, reach);
        var point = new WorldPos((int)(origin.X + (direction.X * along / One)), (int)(origin.Y + (direction.Y * along / One)));

        state.ReloadLeft = Veterancy.Reload(world, unit.Id, weapon.Reload);
        state.BurstLeft = 0;
        if (unit.Archetype.HasTurret)
        {
            unit.TurretFacing = yaw;
        }
        else
        {
            unit.Facing = yaw;
        }

        events?.Add(new WeaponFired(world.CurrentTick, unit.Id, hit, 0, weaponIndex));
        events?.Add(new ProjectileImpact(world.CurrentTick, null, unit.Id, weaponIndex, point, hit));
        events?.Add(new AimedHit(world.CurrentTick, unit.Id, hit, point, head));
        world.Hits.Add(new Hit(unit.Id, unit.Id, unit.Owner, weaponIndex, point, hit) { Lethal = head });
        return true;
    }

    // A random turn of -spread…spread, none for a spread of 0 (no number drawn).
    private static int Offset(GameWorld world, int spread) => spread > 0 ? world.Random.NextInt(-spread, spread + 1) : 0;

    // The height of the ground at the point, in whole leptons.
    private static long Ground(GameWorld world, WorldPos point) => world.Map.Heights.HeightAt(point).ToIntFloor();

    /// <summary>
    /// How far along the ray the shot ends, what it hits there (null: the ground or the end of its reach) and whether in a soldier's
    /// head: the nearest of the ground, the units and the buildings it meets.
    /// </summary>
    private static (long Along, EntityId? Hit, bool Head) Trace(
        GameWorld world, Unit shooter, (long X, long Y, long Z) origin, (long X, long Y, long Z) direction, long reach)
    {
        var best = Terrain(world, origin, direction, reach);
        EntityId? hit = null;
        var head = false;

        var end = (X: origin.X + (direction.X * best / One), Y: origin.Y + (direction.Y * best / One));
        var margin = VehicleRadius + 1;
        var near = new List<EntityId>();
        world.Spatial.QueryRect(
            world,
            new WorldPos((int)(IntMath.Min(origin.X, end.X) - margin), (int)(IntMath.Min(origin.Y, end.Y) - margin)),
            new WorldPos((int)(IntMath.Max(origin.X, end.X) + margin), (int)(IntMath.Max(origin.Y, end.Y) + margin)),
            near);
        near.Sort();
        foreach (var id in near)
        {
            if (id == shooter.Id || !world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } unit || unit.Archetype.Flies)
            {
                continue;
            }

            var soldier = unit.Archetype.Category == UnitCategory.Infantry;
            var radius = soldier ? SoldierRadius : VehicleRadius;
            var height = soldier ? SoldierHeight : VehicleHeight;
            var bottom = Ground(world, unit.Position);
            if (Cylinder(origin, direction, unit.Position, radius, bottom, bottom + height) is { } at && at < best)
            {
                best = at;
                hit = unit.Id;
                head = soldier && origin.Z + (direction.Z * at / One) >= bottom + height - HeadHeight;
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true } building)
            {
                continue;
            }

            var bottom = Ground(world, Aim.CentreOf(building));
            var low = (X: (long)building.Cell.X * WorldPos.LeptonsPerCell, Y: (long)building.Cell.Y * WorldPos.LeptonsPerCell, Z: bottom);
            var high = (X: low.X + ((long)building.Width * WorldPos.LeptonsPerCell), Y: low.Y + ((long)building.Height * WorldPos.LeptonsPerCell),
                Z: bottom + BuildingHeight);
            if (Box(origin, direction, low, high) is { } at && at < best)
            {
                best = at;
                hit = building.Id;
                head = false;
            }
        }

        return (best, hit, head);
    }

    // Where along the ray it first goes under the ground, else its reach; off the map it ends there.
    private static long Terrain(GameWorld world, (long X, long Y, long Z) origin, (long X, long Y, long Z) direction, long reach)
    {
        for (var along = (long)GroundStep; along <= reach; along += GroundStep)
        {
            var point = new WorldPos((int)(origin.X + (direction.X * along / One)), (int)(origin.Y + (direction.Y * along / One)));
            if (!world.Contains(point))
            {
                return along;
            }

            if (origin.Z + (direction.Z * along / One) <= Ground(world, point))
            {
                return along;
            }
        }

        return reach;
    }

    // Where along the ray it first meets the upright cylinder, or null.
    private static long? Cylinder((long X, long Y, long Z) origin, (long X, long Y, long Z) direction, WorldPos centre, long radius, long bottom, long top)
    {
        // Across: |origin + d·t/One − centre| ≤ radius, a quadratic in t.
        var (cx, cy) = (centre.X - origin.X, centre.Y - origin.Y);
        var across = (direction.X * direction.X) + (direction.Y * direction.Y);
        long enter;
        long leave;
        if (across == 0)
        {
            if ((cx * cx) + (cy * cy) > radius * radius)
            {
                return null;
            }

            (enter, leave) = (0L, long.MaxValue);
        }
        else
        {
            var dot = (direction.X * cx) + (direction.Y * cy);
            var squared = (System.Int128)dot * dot - ((System.Int128)across * (((cx * cx) + (cy * cy)) - (radius * radius)));
            if (squared < 0)
            {
                return null;
            }

            var root = (long)IntMath.Sqrt((System.UInt128)squared);
            enter = (long)((System.Int128)(dot - root) * One / across);
            leave = (long)((System.Int128)(dot + root) * One / across);
        }

        // Up: bottom ≤ origin.Z + d.Z·t/One ≤ top.
        var (low, high) = Span(origin.Z, direction.Z, bottom, top);
        var from = IntMath.Max(IntMath.Max(enter, low), 0L);
        var to = IntMath.Min(leave, high);
        return from <= to ? from : null;
    }

    // Where along the ray it first meets the box, or null.
    private static long? Box((long X, long Y, long Z) origin, (long X, long Y, long Z) direction, (long X, long Y, long Z) low, (long X, long Y, long Z) high)
    {
        var (x0, x1) = Span(origin.X, direction.X, low.X, high.X);
        var (y0, y1) = Span(origin.Y, direction.Y, low.Y, high.Y);
        var (z0, z1) = Span(origin.Z, direction.Z, low.Z, high.Z);
        var from = IntMath.Max(IntMath.Max(IntMath.Max(x0, y0), z0), 0L);
        var to = IntMath.Min(IntMath.Min(x1, y1), z1);
        return from <= to ? from : null;
    }

    // The stretch of t over which origin + d·t/One lies within low…high: all of it for a ray along the slab, none outside it.
    private static (long From, long To) Span(long origin, long direction, long low, long high)
    {
        if (direction == 0)
        {
            return origin >= low && origin <= high ? (long.MinValue, long.MaxValue) : (1L, 0L);
        }

        var a = (low - origin) * One / direction;
        var b = (high - origin) * One / direction;
        return a <= b ? (a, b) : (b, a);
    }
}
