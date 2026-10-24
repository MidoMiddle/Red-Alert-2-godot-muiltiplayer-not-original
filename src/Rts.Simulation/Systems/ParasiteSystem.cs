using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Parasites (stage 3d of filling the game: the combat drone, RA2 <c>Parasite</c>), after the toxin. A shot whose warhead is a parasite
/// at a vehicle with none inside puts its shooter into it: the shooter leaves the map without a death and bites the vehicle from inside,
/// <see cref="WarheadDef.ParasiteDamage"/> every <see cref="WarheadDef.ParasiteDelay"/> ticks, as its owner's; anything else it hits takes
/// the weapon's damage (a soldier dies at once). When the vehicle falls the parasite comes out beside it with its hit points; a workshop
/// mending the vehicle kills it (<see cref="RepairSystem"/>). A unit whose weapons are only parasites picks no vehicle with one inside,
/// nor a ship or a flier.
/// </summary>
public static class ParasiteSystem
{
    /// <summary>A parasite shot lands: the shooter goes into the vehicle, unless one is there. True when it did, and nothing else happens.</summary>
    internal static bool Enter(GameWorld world, Hit hit, WarheadDef warhead, List<SimEvent>? events)
    {
        // A vehicle for the drone; a ship on the water for the squid (stage 3e).
        var category = warhead.ParasiteNaval ? UnitCategory.Naval : UnitCategory.Vehicle;
        if (hit.Target is not { } id || !world.TryGet(id, out var target) || target is not Unit { IsAlive: true } host
            || host.Archetype.Category != category || host.Archetype.Has(UnitTalents.Submerged) || host.IsInvulnerable(world.CurrentTick))
        {
            return false;
        }

        if (host.Parasite == 0 && world.TryGet(hit.Shooter, out var shooter) && shooter is Unit { IsAlive: true } drone)
        {
            host.Parasite = drone.Type;
            host.ParasiteOwner = drone.Owner;
            host.ParasiteHp = drone.Hp;
            host.ParasiteWeapon = hit.Weapon;
            host.ParasiteTicks = warhead.ParasiteDelay;
            drone.MarkDead();
            events?.Add(new ParasiteEntered(world.CurrentTick, drone.Id, host.Id));
        }

        return true;
    }

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var rules = world.Rules.Database;
        List<Unit>? freed = null;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { Parasite: not 0 } host)
            {
                continue;
            }

            if (host.IsAlive && --host.ParasiteTicks <= 0)
            {
                var warhead = rules.Warheads[rules.IndexOfWarhead(rules.Weapons[host.ParasiteWeapon].Warhead)];
                host.ParasiteTicks = warhead.ParasiteDelay;
                DamageSystem.Hurt(
                    world, host, warhead.ParasiteDamage,
                    new Hit(host.Id, EntityId.None, host.ParasiteOwner, host.ParasiteWeapon, host.Position, host.Id), events);
            }

            if (!host.IsAlive)
            {
                (freed ??= []).Add(host);
            }
        }

        // Out after the walk: the list of entities must not change while walked.
        foreach (var host in freed ?? [])
        {
            LetOut(world, host, events);
        }
    }

    /// <summary>A hit that frees the host (the dolphin's wave on a ship the squid holds): the parasite comes out beside it, alive.</summary>
    internal static void Free(GameWorld world, Unit host, List<SimEvent>? events)
    {
        events?.Add(new ParasiteFreed(world.CurrentTick, host.Id));
        LetOut(world, host, events);
    }

    // The parasite comes out with its hit points on the host's cell or the nearest free one round it; with no room it is lost.
    private static void LetOut(GameWorld world, Unit host, List<SimEvent>? events)
    {
        var archetype = world.Rules.Unit(host.Parasite);
        var cell = host.Position.ToCell();
        if (!world.Reservations.HasRoomFor(cell, archetype.Placement, EntityId.None) || !world.Map.CanEnter(cell, archetype.Locomotor))
        {
            cell = TransportSystem.FreeCellAround(world, cell, archetype) ?? cell;
        }

        if (world.Map.CanEnter(cell, archetype.Locomotor) && world.Reservations.HasRoomFor(cell, archetype.Placement, EntityId.None))
        {
            var parasite = world.SpawnUnit(host.ParasiteOwner, host.Parasite, cell.Center, snapToSubcell: true);
            world.SetHp(parasite, host.ParasiteHp, events);
        }

        Clear(host);
    }

    /// <summary>Whether the parasite inside <paramref name="host"/> holds it still (stage 3e: a ship in the squid's grip).</summary>
    public static bool Paralyzed(GameWorld world, Unit host)
    {
        if (host.Parasite == 0 || host.ParasiteWeapon < 0)
        {
            return false;
        }

        var rules = world.Rules.Database;
        return rules.Warheads[rules.IndexOfWarhead(rules.Weapons[host.ParasiteWeapon].Warhead)].Paralyzes;
    }

    /// <summary>The parasite is gone: killed by a repair, or out of its fallen host.</summary>
    internal static void Clear(Unit host)
    {
        host.Parasite = 0;
        host.ParasiteOwner = PlayerId.Neutral;
        host.ParasiteHp = 0;
        host.ParasiteWeapon = -1;
        host.ParasiteTicks = 0;
    }

    /// <summary>Whether every weapon among <paramref name="weapons"/> is a parasite.</summary>
    internal static bool ParasitesOnly(GameRules rules, System.Collections.Immutable.ImmutableArray<int> weapons)
    {
        var any = false;
        foreach (var weapon in weapons)
        {
            if (weapon < 0)
            {
                continue;
            }

            if (!rules.Database.Warheads[rules.Database.IndexOfWarhead(rules.Database.Weapons[weapon].Warhead)].Parasite)
            {
                return false;
            }

            any = true;
        }

        return any;
    }
}
