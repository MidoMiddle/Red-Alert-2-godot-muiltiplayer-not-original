using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The ammo of aircraft and the carriers' jets (stage 3f of filling the game: the strike jets, RA2 <c>Ammo</c>; the aircraft carrier,
/// RA2 <c>Spawns</c>), after the movement. A unit whose type carries ammo spends one with every volley it starts
/// (<see cref="WeaponSystem"/>) and starts none without. Out of ammo, once its last volley is over, it forgets its target and flies home:
/// a jet to the nearest ready building of its owner that rearms aircraft, where — within a cell of the footprint — it loads a volley every
/// <see cref="RearmTicks"/> ticks up to its type's ammo; a Hornet to its carrier, or the Osprey to its destroyer (loose ends, stage 4),
/// which it boards beside, as it does when its target is gone. A Hornet whose carrier fell falls too. A player builds no more jets than its pads (<see cref="BuildingDef.AircraftDocks"/>). A
/// carrier's weapon launches every Hornet aboard at its target (<see cref="Launch"/>); one lost is replaced aboard after the carrier's
/// <see cref="UnitDef.SpawnRegen"/> ticks.
/// </summary>
public static class AmmoSystem
{
    /// <summary>The ticks to load a volley (RA2 <c>ReloadRate=.3</c> minutes).</summary>
    public const int RearmTicks = 270;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            if (unit.Archetype.SpawnCount > 0)
            {
                Replace(world, unit);
            }

            if (unit.Mother is { } mother)
            {
                Home(world, unit, mother, events);
                continue;
            }

            if (unit.Archetype.Ammo <= 0 || unit.Weapons is [{ BurstLeft: > 0 }, ..] || PadOf(world, unit) is not { } airfield)
            {
                continue;
            }

            if (OnPad(world, unit))
            {
                // On its pad (RA2: four to an air force command), it loads a volley now and then up to its type's ammo.
                if (unit.Ammo < unit.Archetype.Ammo && ++unit.RearmTicks >= RearmTicks)
                {
                    unit.Ammo++;
                    unit.RearmTicks = 0;
                }

                continue;
            }

            // Spent, or with nothing to do, it flies back and lands on a free pad (RA2): over the airfield when none is free.
            var spent = unit.Ammo == 0;
            var idle = unit.Target is null && unit.MoveTarget is null && !unit.PathPending && unit.TargetPoint is null;
            if ((spent || idle) && !HeadingHome(world, unit, airfield))
            {
                if (spent)
                {
                    UnitOrders.ForgetTarget(unit);
                }

                UnitOrders.MoveTo(world, unit, FreePad(world, airfield, unit) ?? Aim.CentreOf(airfield));
            }
        }
    }

    /// <summary>
    /// The carrier launches every unit aboard at <paramref name="target"/>: each takes off from its place as the player's order to attack
    /// it, and comes back when it has spent its ammo or the target is gone.
    /// </summary>
    internal static void Launch(GameWorld world, Unit carrier, EntityId? target, List<SimEvent>? events)
    {
        if (carrier.Archetype.Spawn == 0 || target is null)
        {
            return;
        }

        for (; carrier.SpawnsDocked > 0; carrier.SpawnsDocked--)
        {
            var jet = world.SpawnUnit(carrier.Owner, carrier.Archetype.Spawn, carrier.Position);
            jet.Mother = carrier.Id;
            jet.Target = target;
            jet.TargetOrder = TargetOrder.Attack;
            events?.Add(new SpawnLaunched(world.CurrentTick, jet.Id, carrier.Id));
        }
    }

    /// <summary>The pads of the player's ready buildings less its aircraft that rearm, flying or in a queue.</summary>
    public static int FreePads(GameWorld world, PlayerId player)
    {
        var pads = 0;
        var jets = 0;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && building.Owner == player)
            {
                pads += def.AircraftDocks;
            }
            else if (entity is Unit { IsAlive: true, Archetype.Ammo: > 0, Mother: null } jet && jet.Owner == player)
            {
                jets++;
            }
        }

        var state = world.Player(player);
        var rules = world.Rules.Database;
        for (var category = ProductionCategories.First; category <= ProductionCategories.Last; category++)
        {
            foreach (var item in state.Queue(category))
            {
                if (item.Kind == ProductionKind.Unit && rules.Units[item.DefIndex].Ammo > 0)
                {
                    jets++;
                }
            }
        }

        return IntMath.Max(0, pads - jets);
    }

    // A Hornet: with its carrier gone it falls; out of ammo or without a target it flies back and boards beside it.
    private static void Home(GameWorld world, Unit jet, EntityId motherId, List<SimEvent>? events)
    {
        if (!world.TryGet(motherId, out var entity) || entity is not Unit { IsAlive: true } mother)
        {
            DeathSystem.Kill(world, jet, EntityId.None, PlayerId.Neutral, DeathCause.Defeat, -1, events);
            return;
        }

        var spent = jet.Ammo == 0 && jet.Weapons is not [{ BurstLeft: > 0 }, ..];
        if (!spent && jet.Target is not null)
        {
            return;
        }

        var cell = mother.Position.ToCell();
        if (Beside(cell, cell, jet.Position.ToCell()))
        {
            jet.MarkDead();
            mother.SpawnsDocked = IntMath.Min(mother.Archetype.SpawnCount, mother.SpawnsDocked + 1);
        }
        else if (jet.MoveTarget != mother.Position)
        {
            UnitOrders.ForgetTarget(jet);
            UnitOrders.MoveTo(world, jet, mother.Position);
        }
    }

    // A carrier short of its count, aboard and out, works at replacing one.
    private static void Replace(GameWorld world, Unit carrier)
    {
        var count = carrier.SpawnsDocked;
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true } jet && jet.Mother == carrier.Id)
            {
                count++;
            }
        }

        if (count >= carrier.Archetype.SpawnCount)
        {
            carrier.SpawnRegenTicks = 0;
        }
        else if (++carrier.SpawnRegenTicks >= carrier.Archetype.SpawnRegen)
        {
            carrier.SpawnsDocked++;
            carrier.SpawnRegenTicks = 0;
        }
    }

    /// <summary>
    /// The cell of pad <paramref name="index"/> of a building that rearms aircraft (RA2 <c>NumberOfDocks</c>): its east columns, the
    /// eastmost first, north to south in a column; the tower stands west of them.
    /// </summary>
    public static CellPos PadCell(Building airfield, int index) =>
        new(airfield.Cell.X + airfield.Width - 1 - (index / airfield.Height), airfield.Cell.Y + (index % airfield.Height));

    /// <summary>
    /// The middle of a pad of <paramref name="airfield"/> no other jet of its owner stands on or flies to; null when they all are taken.
    /// </summary>
    public static WorldPos? FreePad(GameWorld world, Building airfield, Unit? jet)
    {
        for (var index = 0; index < (airfield.Def?.AircraftDocks ?? 0); index++)
        {
            var spot = PadCell(airfield, index).Center;
            var taken = false;
            foreach (var entity in world.Entities)
            {
                if (entity is Unit { IsAlive: true, Archetype.Ammo: > 0, Mother: null } other && other != jet && other.Owner == airfield.Owner
                    && (other.MoveTarget == spot || (other.MoveTarget is null && other.Position == spot)))
                {
                    taken = true;
                    break;
                }
            }

            if (!taken)
            {
                return spot;
            }
        }

        return null;
    }

    /// <summary>Whether the jet stands on a pad of a ready airfield of its owner, landed: no order to go anywhere.</summary>
    public static bool OnPad(GameWorld world, Unit jet)
    {
        if (jet.MoveTarget is not null || jet.PathPending || world.BuildingAt(jet.Position.ToCell()) is not { IsAlive: true, IsReady: true, Def: { RearmsAircraft: true } def } airfield
            || airfield.Owner != jet.Owner)
        {
            return false;
        }

        for (var index = 0; index < def.AircraftDocks; index++)
        {
            if (jet.Position == PadCell(airfield, index).Center)
            {
                return true;
            }
        }

        return false;
    }

    // Whether the jet already flies to a pad of the airfield, or over it.
    private static bool HeadingHome(GameWorld world, Unit jet, Building airfield)
    {
        if (jet.MoveTarget is not { } target)
        {
            return false;
        }

        if (target == Aim.CentreOf(airfield))
        {
            return true;
        }

        for (var index = 0; index < (airfield.Def?.AircraftDocks ?? 0); index++)
        {
            if (target == PadCell(airfield, index).Center)
            {
                return true;
            }
        }

        return false;
    }

    // The nearest ready building of the owner that rearms aircraft, by its centre, then by id.
    private static Building? PadOf(GameWorld world, Unit unit)
    {
        Building? best = null;
        var bestDistance = int.MaxValue;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def.RearmsAircraft: true } pad && pad.Owner == unit.Owner)
            {
                var distance = DamageCalculator.Distance(unit.Position, Aim.CentreOf(pad));
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (pad, distance);
                }
            }
        }

        return best;
    }

    private static bool Beside(CellPos near, CellPos far, CellPos cell) =>
        IntMath.Max(IntMath.Max(near.X - cell.X, cell.X - far.X), IntMath.Max(near.Y - cell.Y, cell.Y - far.Y)) <= 1;
}
