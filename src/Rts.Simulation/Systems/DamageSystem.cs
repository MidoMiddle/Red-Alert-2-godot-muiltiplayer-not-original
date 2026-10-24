using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The damage of the shots that landed this tick (docs/implementation/M4_combat.md, 4.9), after the projectiles and before the movement.
/// The hits wait in <see cref="GameWorld.Hits"/> and are applied in an order of their own — by the projectile or shooter, the target and
/// the point — so the order in which the systems found them changes nothing. A hit takes the whole share against the armor from the
/// target it was fired at (at least 1, <see cref="DamageCalculator.Direct"/>), and the fall-off from everything else in its area, by id,
/// friend or foe as in RA2; a wall only from a warhead that damages walls. At no hit points a unit dies and a building falls through
/// <see cref="DeathSystem"/>. What died this tick takes no more damage. A building of a player hit by another player's shot tells it
/// with an <see cref="UnderAttack"/>, once in <see cref="UnderAttackTicks"/> at most (step 13).
/// </summary>
public static class DamageSystem
{
    /// <summary>The least time between two <see cref="UnderAttack"/> of a player: 10 seconds at 15 ticks a second.</summary>
    public const int UnderAttackTicks = 150;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        if (world.Hits.Count == 0)
        {
            return;
        }

        world.Hits.Sort(Compare);
        var rules = world.Rules;
        var area = world.Area;
        List<Hit>? splinters = null;
        foreach (var hit in world.Hits)
        {
            Apply(world, rules, hit, area, events);
            if (rules.Database.Weapons[hit.Weapon].Fragments > 0)
            {
                Splinter(world, rules, hit, splinters ??= [], events);
            }
        }

        // The splinters of a splitting beam (stage 3c) land after every hit of the tick, in the order they were sent.
        foreach (var splinter in splinters ?? [])
        {
            Apply(world, rules, splinter, area, events);
        }

        world.Hits.Clear();
    }

    /// <summary>
    /// A splitting beam (stage 3c, RA2 <c>ShrapnelCount</c>: the beam tank) sends <see cref="WeaponDef.Fragments"/> splinters of its
    /// fragment weapon from where it hit to the nearest living enemies of its owner within <see cref="WeaponDef.FragmentRange"/> — not
    /// its target, not a wall, only what the splinter fires at — the nearest first, then by id.
    /// </summary>
    private static void Splinter(GameWorld world, GameRules rules, Hit hit, List<Hit> splinters, List<SimEvent>? events)
    {
        var weapon = rules.Database.Weapons[hit.Weapon];
        var fragment = rules.Database.IndexOfWeapon(weapon.FragmentWeapon);
        if (fragment < 0 || hit.Owner.IsNeutral)
        {
            return;
        }

        var reach = Aim.Leptons(weapon.FragmentRange);
        var layer = rules.Database.Weapons[fragment].Targets;
        var found = new List<(int Distance, EntityId Id, WorldPos Point)>();
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity.Id == hit.Target || entity is not (World.Unit or Building) || entity.Owner.IsNeutral
                || world.AreAllies(entity.Owner, hit.Owner) || entity is Building { Def.Wall: true }
                || (TargetingSystem.LayerOf(entity) & layer) == 0)
            {
                continue;
            }

            var point = Aim.NearestPoint(entity, hit.Point);
            var distance = DamageCalculator.Distance(hit.Point, point);
            if (distance <= reach)
            {
                found.Add((distance, entity.Id, point));
            }
        }

        found.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Id.CompareTo(b.Id));
        for (var i = 0; i < found.Count && i < weapon.Fragments; i++)
        {
            var (_, id, point) = found[i];
            events?.Add(new ProjectileImpact(world.CurrentTick, null, hit.Shooter, fragment, point, id));
            splinters.Add(new Hit(hit.Source, hit.Shooter, hit.Owner, fragment, point, id));
        }
    }

    /// <summary>The order of the hits: by their source, then the target they were fired at, then the point.</summary>
    private static int Compare(Hit a, Hit b)
    {
        var order = a.Source.Value.CompareTo(b.Source.Value);
        if (order == 0)
        {
            order = (a.Target?.Value ?? -1).CompareTo(b.Target?.Value ?? -1);
        }

        if (order == 0)
        {
            order = a.Point.X.CompareTo(b.Point.X);
        }

        return order != 0 ? order : a.Point.Y.CompareTo(b.Point.Y);
    }

    private static void Apply(GameWorld world, GameRules rules, Hit hit, List<EntityId> area, List<SimEvent>? events)
    {
        var weapon = rules.Database.Weapons[hit.Weapon];
        var warheadIndex = rules.Database.IndexOfWarhead(weapon.Warhead);
        var warhead = rules.Database.Warheads[warheadIndex];
        if (warhead.Bomb.Length > 0)
        {
            // A bomb (stage 3b) hurts nothing now: it is planted on the target.
            BombSystem.Plant(world, hit, warhead, events);
            return;
        }

        if (warhead.Temporal)
        {
            // A temporal beam (stage 3a) hurts nothing: it holds its target for the erasure.
            ErasureSystem.Hold(world, hit);
            return;
        }

        if (warhead.Radiation > 0)
        {
            // Toxin (stage 3b) stays on the ground of the area.
            RadiationSystem.Lay(world, hit.Point, warhead.Spread, warhead.Radiation);
        }

        var damage = hit.BonusPercent > 0 ? (int)((long)weapon.Damage * (100 + hit.BonusPercent) / 100) : weapon.Damage;

        if (hit.Target is { } targetId && world.TryGet(targetId, out var target) && target.IsAlive && Hurts(target, warhead))
        {
            var amount = DamageCalculator.Direct(damage, rules.Verses(warheadIndex, ArmorOf(rules, target)));
            Damage(world, target, amount, hit, events);
        }

        if (warhead.Spread <= Fixed.Zero)
        {
            return;
        }

        var radius = Aim.Leptons(warhead.Spread);
        world.Spatial.QueryRadius(world, hit.Point, radius, area);
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true } building
                && DamageCalculator.Distance(hit.Point, Aim.NearestPoint(building, hit.Point)) <= radius)
            {
                area.Add(building.Id);
            }
        }

        area.Sort();
        foreach (var id in area)
        {
            if (id == hit.Target || !world.TryGet(id, out var entity) || !entity.IsAlive || !Hurts(entity, warhead))
            {
                continue;
            }

            // The splinters stay in their layer (stage 2b): a warhead at the air hurts only fliers, any other only what is on the ground.
            if (entity is Unit { Archetype.Flies: true } != warhead.AffectsAir)
            {
                continue;
            }

            var distance = DamageCalculator.Distance(hit.Point, Aim.NearestPoint(entity, hit.Point));
            var verses = rules.Verses(warheadIndex, ArmorOf(rules, entity));
            Damage(world, entity, DamageCalculator.InArea(damage, verses, warhead.Spread, warhead.PercentAtMax, distance), hit, events);
        }
    }

    /// <summary>
    /// A building of a player hit by another, not an ally (M8 step 11): <see cref="UnderAttack"/>, unless the last one is less than 10
    /// seconds old.
    /// </summary>
    private static void WarnOfAttack(GameWorld world, Building building, Hit hit, List<SimEvent>? events)
    {
        if (building.Owner.IsNeutral || building.Owner.Value > PlayerId.MaxPlayers || world.AreAllies(hit.Owner, building.Owner))
        {
            return;
        }

        var player = world.Player(building.Owner);
        if (player.LastUnderAttack is { } last && world.CurrentTick - last < UnderAttackTicks)
        {
            return;
        }

        player.LastUnderAttack = world.CurrentTick;
        events?.Add(new UnderAttack(world.CurrentTick, building.Owner, Aim.CentreOf(building)));
    }

    /// <summary>A wall takes damage only from a warhead that damages walls.</summary>
    private static bool Hurts(Entity entity, WarheadDef warhead) =>
        warhead.WallDamage || entity is not Building { Def.Wall: true };

    /// <summary>
    /// The armor number of a unit or a building of a player. A building of the map has no armor in the data yet (M3 step 14a): it counts as
    /// the first armor type.
    /// </summary>
    private static int ArmorOf(GameRules rules, Entity entity) => entity switch
    {
        World.Unit unit => unit.Archetype.Armor,
        Building { DefIndex: >= 0 } building => rules.ArmorOfBuilding(building.DefIndex),
        Projectile shot => rules.ArmorOfShot(shot.Weapon),
        _ => 0,
    };

    /// <summary>Damage that comes of no shot (stage 3b: the toxin on the ground), as a shot's would.</summary>
    internal static void Hurt(GameWorld world, Entity entity, int amount, Hit hit, List<SimEvent>? events) => Damage(world, entity, amount, hit, events);

    private static void Damage(GameWorld world, Entity entity, int amount, Hit hit, List<SimEvent>? events)
    {
        // Held by a temporal beam (stage 3a), it is out of time: nothing else hurts it.
        if (amount <= 0 || entity.ErasedBy is not null)
        {
            return;
        }

        var (attacker, weapon) = (hit.Shooter, hit.Weapon);
        switch (entity)
        {
            case World.Unit unit:
                {
                    var hp = IntMath.Max(0, unit.Hp - amount);
                    world.SetHp(unit, hp, events);
                    events?.Add(new EntityDamaged(world.CurrentTick, unit.Id, attacker, amount, hp));
                    if (hp == 0)
                    {
                        DeathSystem.Kill(world, unit, attacker, hit.Owner, DeathCause.Weapon, weapon, events);
                    }

                    break;
                }

            case Projectile { Hp: > 0 } shot:
                {
                    // A shot brought down in flight (stage 3c) falls without going off.
                    shot.Hp = IntMath.Max(0, shot.Hp - amount);
                    if (shot.Hp == 0)
                    {
                        shot.MarkDead();
                        events?.Add(new EntityDestroyed(world.CurrentTick, shot.Id, attacker, DeathCause.Weapon, weapon, hit.Owner));
                    }

                    break;
                }

            case Building building:
                {
                    WarnOfAttack(world, building, hit, events);
                    var hp = IntMath.Max(0, building.Hp - amount);
                    events?.Add(new EntityDamaged(world.CurrentTick, building.Id, attacker, amount, hp));
                    if (hp == 0)
                    {
                        DeathSystem.Destroy(world, building, attacker, hit.Owner, weapon, events);
                    }
                    else
                    {
                        world.SetHp(building, hp, events);
                    }

                    break;
                }
        }
    }
}
