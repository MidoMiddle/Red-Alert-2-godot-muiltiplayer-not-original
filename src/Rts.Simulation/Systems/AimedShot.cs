using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The shot of the player inside a unit (the owner, 2026-11-04: aiming as in a shooter; the ray and its spread, a shot in the head kills
/// at once; 2026-11-05: cover, the sandbags, the aircraft). From the unit's eyes — its floor, its height (<see cref="Altitude"/>) and
/// <see cref="EyeHeight"/> — a ray goes along the direction of the command, turned a little at random within the
/// <see cref="WeaponDef.AimSpread"/> of its weapon (twice that on the move), as far as the weapon reaches. Whatever it meets first takes
/// the shot:
/// <list type="bullet">
/// <item>a unit the weapon can fire at (<see cref="TargetingSystem.LayerOf"/>: a bullet for the ground goes through an aircraft, one
/// for the air through a soldier) — an upright cylinder at its height, a soldier's head the top of it; a soldier dug in kneels lower
/// behind a ring of sandbags, which stops the shot and takes nothing;</item>
/// <item>a map object that gives cover (<see cref="MapObjectType.CoverHeight"/>: a wall, a fence, a boulder, a tree's trunk) — a box in
/// the middle of its cell; it takes the weapon's damage (<see cref="GameWorld.DamageAtCell"/>);</item>
/// <item>a building — a box over its footprint —, or the ground.</item>
/// </list>
/// Anyone's: the shooter's own and the civilians too. A hit in the head of a soldier kills it (<see cref="Hit.Lethal"/>); a shot that
/// meets nobody falls on the ground where the ray does, or at its end. Of the unit's instant weapons that are loaded, the first whose
/// ray hits something fires (a Flak Track's gun at a plane, its cannon at the rest), else the first of them. Only a unit under the
/// player's hand (<see cref="Unit.Manual"/>) fires; its weapon then reloads as after any shot. Integer leptons throughout: the same
/// on every machine.
/// </summary>
public static class AimedShot
{
    /// <summary>The eyes over what the unit stands on, in leptons: half a cell, where the camera of the client stands.</summary>
    public const int EyeHeight = 128;

    /// <summary>A soldier: the radius and the height of its cylinder and the top of it that is its head, in leptons.</summary>
    public const int SoldierRadius = 40;

    public const int SoldierHeight = 140;

    public const int HeadHeight = 28;

    /// <summary>A soldier dug in kneels: its cylinder this high.</summary>
    public const int KneelingHeight = 100;

    /// <summary>The ring of sandbags around a soldier dug in: its radius and height, in leptons.</summary>
    public const int SandbagRadius = 64;

    public const int SandbagHeight = 48;

    /// <summary>A vehicle, a ship, an aircraft: the radius and the height of its cylinder, in leptons.</summary>
    public const int VehicleRadius = 110;

    public const int VehicleHeight = 100;

    /// <summary>A building stands this high over its footprint, in leptons: two cells, as it stands from the ground in the client.</summary>
    public const int BuildingHeight = 512;

    // The step along the ray at which the ground and the cover are looked at, in leptons.
    private const int GroundStep = 32;

    // A direction is kept in units of 1/65536, as Fixed is.
    private const long One = Fixed.OneRaw;

    // How far up or down a shot may go: a little short of straight.
    private const int MaxPitch = 16000;

    // The spread is drawn once a shot, as a share of 1024 either way, and scaled to the weapon that fires.
    private const int SpreadShare = 1024;

    private readonly record struct Trace(long Along, EntityId? Target, bool Head, CellPos? Cover, bool Bags)
    {
        public bool Meets => Target is not null || Cover is not null || Bags;
    }

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
        var loaded = new List<int>();
        for (var slot = 0; slot < slots.Length && slot < unit.Weapons.Length; slot++)
        {
            if (slots[slot] >= 0 && unit.Weapons[slot].ReloadLeft == 0
                && rules.Database.TryGetProjectile(rules.Database.Weapons[slots[slot]].Projectile, out var projectile)
                && projectile.Motion == ProjectileMotion.Instant)
            {
                loaded.Add(slot);
            }
        }

        if (loaded.Count == 0)
        {
            return false;
        }

        // The spread: a share either way, from the generator of the match, so every machine draws the same; twice on the move.
        var shareYaw = world.Random.NextInt(-SpreadShare, SpreadShare + 1);
        var sharePitch = world.Random.NextInt(-SpreadShare, SpreadShare + 1);
        var moving = unit.MoveTarget is not null || unit.PathPending ? 2 : 1;
        var origin = (X: (long)unit.Position.X, Y: (long)unit.Position.Y, Z: Altitude.Of(world, unit) + EyeHeight);

        var chosen = -1;
        var trace = default(Trace);
        var yaw = command.Yaw;
        var ray = (X: 0L, Y: 0L, Z: 0L);
        foreach (var slot in loaded)
        {
            var weapon = rules.Database.Weapons[slots[slot]];
            var spread = (int)((weapon.AimSpread.Raw * BAngle.FullTurn / 360) >> 16) * moving;
            var turned = new BAngle((ushort)(command.Yaw.Value + (shareYaw * spread / SpreadShare)));
            var pitch = new BAngle((ushort)IntMath.Clamp(command.Pitch + (sharePitch * spread / SpreadShare), -MaxPitch, MaxPitch));
            var cosPitch = FixedMath.Cos(pitch).Raw;
            var (sinYaw, minusCosYaw) = FixedMath.Direction(turned);
            var direction = (X: sinYaw.Raw * cosPitch / One, Y: minusCosYaw.Raw * cosPitch / One, Z: FixedMath.Sin(pitch).Raw);
            var found = Cast(world, unit, weapon, origin, direction, Aim.Leptons(weapon.Range));
            if (chosen < 0 || found.Meets)
            {
                (chosen, trace, yaw, ray) = (slot, found, turned, direction);
                if (found.Meets)
                {
                    break;
                }
            }
        }

        var index = slots[chosen];
        var fired = rules.Database.Weapons[index];
        var point = new WorldPos((int)(origin.X + (ray.X * trace.Along / One)), (int)(origin.Y + (ray.Y * trace.Along / One)));

        var state = unit.Weapons[chosen];
        state.ReloadLeft = Veterancy.Reload(world, unit.Id, fired.Reload);
        state.BurstLeft = 0;
        if (unit.Archetype.HasTurret)
        {
            unit.TurretFacing = yaw;
        }
        else
        {
            unit.Facing = yaw;
        }

        events?.Add(new WeaponFired(world.CurrentTick, unit.Id, trace.Target, chosen, index));
        events?.Add(new ProjectileImpact(world.CurrentTick, null, unit.Id, index, point, trace.Target));
        events?.Add(new AimedHit(world.CurrentTick, unit.Id, trace.Target, point, trace.Head, trace.Cover is not null || trace.Bags));
        if (trace.Cover is { } cover)
        {
            // The cover takes the shot: a fence goes to pieces, a wall holds longer, a boulder for ever.
            world.DamageAtCell(cover, fired.Damage, events);
        }

        world.Hits.Add(new Hit(unit.Id, unit.Id, unit.Owner, index, point, trace.Target) { Lethal = trace.Head });
        return true;
    }

    /// <summary>
    /// How far along the ray of <paramref name="weapon"/> the shot ends, and what it meets there: the nearest of the ground, the cover,
    /// the units it can fire at and the buildings.
    /// </summary>
    private static Trace Cast(
        GameWorld world, Unit shooter, WeaponDef weapon, (long X, long Y, long Z) origin, (long X, long Y, long Z) direction, long reach)
    {
        var (best, cover) = Ground(world, origin, direction, reach);
        var trace = new Trace(best, null, false, cover, false);

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
            if (id == shooter.Id || !world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } unit
                || (TargetingSystem.LayerOf(unit) & weapon.Targets) == 0)
            {
                continue;
            }

            var soldier = unit.Archetype.Category == UnitCategory.Infantry && !unit.Archetype.Flies;
            var dugIn = soldier && unit.Deployed && unit.Archetype.DeployedWeapon >= 0;
            var radius = soldier ? SoldierRadius : VehicleRadius;
            var height = !soldier ? VehicleHeight : dugIn ? KneelingHeight : SoldierHeight;
            var floor = Altitude.Floor(world, unit);
            var bottom = unit.Archetype.Flies ? Altitude.Of(world, unit) - (VehicleHeight / 2) : floor + Altitude.Swim(world, unit);
            if (Cylinder(origin, direction, unit.Position, radius, bottom, bottom + height) is { } at && at < trace.Along)
            {
                trace = new Trace(at, unit.Id, soldier && origin.Z + (direction.Z * at / One) >= bottom + height - HeadHeight, null, false);
            }

            // The sandbags of one dug in: a shot that meets them before the soldier stops there.
            if (dugIn && Cylinder(origin, direction, unit.Position, SandbagRadius, floor, floor + SandbagHeight) is { } bags
                && bags < trace.Along)
            {
                trace = new Trace(bags, null, false, null, true);
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true } building)
            {
                continue;
            }

            var bottom = world.Map.Heights.HeightAt(Aim.CentreOf(building)).ToIntFloor();
            var low = (X: (long)building.Cell.X * WorldPos.LeptonsPerCell, Y: (long)building.Cell.Y * WorldPos.LeptonsPerCell, Z: bottom);
            var high = (X: low.X + ((long)building.Width * WorldPos.LeptonsPerCell), Y: low.Y + ((long)building.Height * WorldPos.LeptonsPerCell),
                Z: bottom + BuildingHeight);
            if (Box(origin, direction, low, high) is { } at && at < trace.Along)
            {
                trace = new Trace(at, building.Id, false, null, false);
            }
        }

        return trace;
    }

    // Where along the ray it first goes under the ground or into the cover of a map object, else its reach; off the map it ends there.
    private static (long Along, CellPos? Cover) Ground(GameWorld world, (long X, long Y, long Z) origin, (long X, long Y, long Z) direction, long reach)
    {
        // The nearest cover of the cells passed so far: a box in the middle of a cell is met further along than its edge.
        var looked = new CellPos(int.MinValue, int.MinValue);
        var coverAt = long.MaxValue;
        CellPos? coverCell = null;
        for (var along = (long)GroundStep; along <= reach; along += GroundStep)
        {
            var point = new WorldPos((int)(origin.X + (direction.X * along / One)), (int)(origin.Y + (direction.Y * along / One)));
            if (!world.Contains(point))
            {
                return coverAt <= along ? (coverAt, coverCell) : (along, null);
            }

            var cell = point.ToCell();
            if (cell != looked)
            {
                looked = cell;
                if (Cover(world, cell, origin, direction) is { } covered && covered < coverAt)
                {
                    (coverAt, coverCell) = (covered, cell);
                }
            }

            if (coverAt <= along)
            {
                return (coverAt, coverCell);
            }

            if (origin.Z + (direction.Z * along / One) <= world.Map.Heights.HeightAt(point).ToIntFloor())
            {
                return (along, null);
            }
        }

        return coverAt <= reach ? (coverAt, coverCell) : (reach, null);
    }

    // Where along the ray it meets the cover standing on the cell, or null.
    private static long? Cover(GameWorld world, CellPos cell, (long X, long Y, long Z) origin, (long X, long Y, long Z) direction)
    {
        long? first = null;
        foreach (var standing in world.Map.ObjectsAt(cell))
        {
            if (standing.Destroyed || standing.Type.CoverHeight <= 0)
            {
                continue;
            }

            var centre = cell.Center;
            var half = standing.Type.CoverWidth / 2;
            var bottom = world.Map.Heights.HeightAt(centre).ToIntFloor();
            var low = (X: (long)centre.X - half, Y: (long)centre.Y - half, Z: bottom);
            var high = (X: (long)centre.X + half, Y: (long)centre.Y + half, Z: bottom + standing.Type.CoverHeight);
            if (Box(origin, direction, low, high) is { } at && (first is null || at < first))
            {
                first = at;
            }
        }

        return first;
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
