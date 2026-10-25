using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Yuri's mind control of RA2 (docs/ra2_units/soviets/yuri): a hit of a <see cref="Rules.WarheadDef.MindControl"/> warhead takes the
/// unit for the shooter's owner at once, with its hit points, its orders forgotten. A Yuri holds one unit at a time: taking another lets
/// the first go. Neither a building, nor a flier, nor a unit immune to it (<see cref="Rules.UnitDef.PsionicImmune"/>: dogs, the terror
/// drone, harvesters, the squid, Yuri), nor one already held is taken. When the Yuri dies the unit goes back to its player, or to
/// nobody when that player is out. A Yuri looks for a unit by himself only while he holds none; ordered, he takes another.
/// </summary>
public static class MindControlSystem
{
    /// <summary>Whether <paramref name="me"/> could take <paramref name="candidate"/>; <paramref name="byItself"/>: looking for one unordered.</summary>
    public static bool CanTake(GameWorld world, Entity me, Entity candidate, bool byItself) =>
        candidate is Unit { IsAlive: true, ControlledBy: null } unit && !unit.Archetype.Flies && !unit.Archetype.Has(UnitTalents.PsionicImmune)
        && !unit.Owner.IsNeutral && !world.AreAllies(unit.Owner, me.Owner)
        && !(byItself && me is Unit { Controlling: not null });

    /// <summary>The hit of a mind control: the shooter, a Yuri still standing, takes the unit it was fired at, if it can.</summary>
    internal static void Take(GameWorld world, Hit hit, List<SimEvent>? events)
    {
        if (hit.Target is not { } targetId || !world.TryGet(targetId, out var found) || found is not Unit target
            || !world.TryGet(hit.Shooter, out var shooter) || shooter is not Unit { IsAlive: true } yuri || !CanTake(world, yuri, target, byItself: false))
        {
            return;
        }

        // One at a time: the unit held before goes back.
        if (yuri.Controlling is { } heldId && world.TryGet(heldId, out var before) && before is Unit { IsAlive: true } held && held.ControlledBy == yuri.Id)
        {
            Release(world, held, events);
        }

        var from = target.Owner;
        target.ControlledBy = yuri.Id;
        target.ControlledFrom = from;
        SetOwner(world, target, yuri.Owner);
        yuri.Controlling = target.Id;
        UnitOrders.ForgetTarget(yuri);
        events?.Add(new UnitControlled(world.CurrentTick, yuri.Id, target.Id, from, yuri.Owner));
    }

    /// <summary>After the deaths of the tick: a hold whose Yuri fell breaks; a Yuri whose unit fell holds nothing.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            if (unit.ControlledBy is { } holderId
                && !(world.TryGet(holderId, out var holder) && holder is Unit { IsAlive: true } yuri && yuri.Controlling == unit.Id))
            {
                Release(world, unit, events);
            }

            if (unit.Controlling is { } heldId
                && !(world.TryGet(heldId, out var found) && found is Unit { IsAlive: true } held && held.ControlledBy == unit.Id))
            {
                unit.Controlling = null;
            }
        }
    }

    /// <summary>The unit goes back to its player, or to nobody when that player is out of the match.</summary>
    private static void Release(GameWorld world, Unit unit, List<SimEvent>? events)
    {
        if (unit.ControlledBy is { } holderId && world.TryGet(holderId, out var holder) && holder is Unit yuri && yuri.Controlling == unit.Id)
        {
            yuri.Controlling = null;
        }

        var to = unit.ControlledFrom.IsNeutral || world.IsDefeated(unit.ControlledFrom) ? PlayerId.Neutral : unit.ControlledFrom;
        unit.ControlledBy = null;
        unit.ControlledFrom = default;
        SetOwner(world, unit, to);
        events?.Add(new UnitReleased(world.CurrentTick, unit.Id, to));
    }

    /// <summary>
    /// The unit changes hands: its sight leaves the old owner's grid (the new owner's comes with <see cref="VisibilitySystem"/>), its
    /// orders and target go. Its passengers go with it.
    /// </summary>
    private static void SetOwner(GameWorld world, Unit unit, PlayerId to)
    {
        if (unit.SightCell is { } cell && !unit.Owner.IsNeutral)
        {
            world.VisionOf(unit.Owner).Hide(cell, unit.SightRadius);
        }

        unit.SightCell = null;
        unit.SightRadius = 0;
        UnitOrders.Stop(world, unit);
        UnitOrders.ForgetTarget(unit);
        unit.Owner = to;
    }
}
