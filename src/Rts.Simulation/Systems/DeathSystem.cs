using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Death and destruction (docs/implementation/M4_combat.md, 4.16): the one place where units die and buildings fall, for
/// <see cref="DamageSystem"/> and <see cref="CrushSystem"/>. A unit at no hit points is dead at once and leaves the world at the end of
/// the tick, which frees its place (<see cref="GameWorld.RemoveDead"/>); a harvester's load goes with it. A building is dead at once —
/// no longer found, no power, no technologies — but its cells open only at the end of the tick, in <see cref="Update"/>, so nobody
/// drives or builds into them on the tick it fell. Every death is an <see cref="EntityDestroyed"/> with its killer and the weapon, by
/// whose warhead the client picks the kind of death.
/// </summary>
public static class DeathSystem
{
    /// <summary>
    /// The unit dies: no hit points, dead, an <see cref="EntityDestroyed"/>; <paramref name="weapon"/> is -1 without one,
    /// <paramref name="killerOwner"/> neutral without a killer.
    /// </summary>
    public static void Kill(
        GameWorld world, Unit unit, EntityId killer, PlayerId killerOwner, DeathCause cause, int weapon, List<SimEvent>? events)
    {
        world.SetHp(unit, 0, events);
        unit.MarkDead();
        events?.Add(new EntityDestroyed(world.CurrentTick, unit.Id, killer, cause, weapon, killerOwner));
    }

    /// <summary>The building falls (<see cref="GameWorld.DestroyBuilding"/>) to a shot of <paramref name="killer"/>, a unit of <paramref name="killerOwner"/>.</summary>
    public static void Destroy(GameWorld world, Building building, EntityId killer, PlayerId killerOwner, int weapon, List<SimEvent>? events)
    {
        world.DestroyBuilding(building, events);
        events?.Add(new EntityDestroyed(world.CurrentTick, building.Id, killer, DeathCause.Weapon, weapon, killerOwner));
    }

    /// <summary>The building falls without a shot — <see cref="DeathCause.Defeat"/> of its owner (M5 step 10): no killer, no weapon.</summary>
    public static void Fall(GameWorld world, Building building, DeathCause cause, List<SimEvent>? events)
    {
        world.DestroyBuilding(building, events);
        events?.Add(new EntityDestroyed(world.CurrentTick, building.Id, EntityId.None, cause, -1));
    }

    /// <summary>The end of the tick, before the dead leave: the cells of the buildings that fell this tick open.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events) => world.OpenWrecks(events);
}
