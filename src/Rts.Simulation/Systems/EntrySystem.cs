using System.Collections.Generic;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>What a soldier does once it goes into a building (stage 2e, 3b of filling the game).</summary>
public enum EntryAction
{
    None,

    /// <summary>Sits in its own building that holds soldiers, or in a civilian building of the map (<see cref="GarrisonSystem"/>).</summary>
    Garrison,

    /// <summary>An engineer takes a building of another player.</summary>
    Capture,

    /// <summary>An engineer mends its own damaged building whole.</summary>
    Mend,

    /// <summary>An infiltrator goes into a building of another player (<see cref="BuildingDef.SpyEffect"/>).</summary>
    Infiltrate,

    /// <summary>A unit boards its own transport (stage 3d, <see cref="TransportSystem"/>).</summary>
    Board,

    /// <summary>A hurt soldier is healed whole at its own hospital (RA2 tech hospital).</summary>
    Heal,

    /// <summary>An engineer mends the bridge of a repair hut (loose ends, stage 2, <see cref="BridgeRepair"/>).</summary>
    RepairBridge,

    /// <summary>A soldier goes into its owner's cloning vats and leaves for half its price (RA2).</summary>
    Recycle,

    /// <summary>A tesla trooper stands by its owner's tesla coil and charges it (RA2).</summary>
    Charge,

    /// <summary>The commando puts its C4 on a bridge repair hut: the hut's bridge falls (RA2, <see cref="BridgeDemolition.PlantAtHut"/>).</summary>
    BlowBridge,
}

/// <summary>
/// Soldiers going into buildings (stage 2e, 3b of filling the game). An <see cref="EnterCommand"/> sends the player's soldiers that can
/// do something there (<see cref="ActionOf"/>) to a ready building of the data; after the movement of the tick each one that stands
/// within <see cref="DoorReach"/> cells of the footprint does it, if it still can: a soldier sits in its own bunker while there is room;
/// an engineer takes a building of a player outside its team that can be taken (RA2 <c>Capturable</c>) — the soldiers inside come out
/// for their owner, the building changes hands with its hit points — or mends its own damaged building whole; an infiltrator goes into a
/// building of a player outside its team and blacks out its owner's power for <see cref="BlackoutTicks"/> ticks, makes it forget its map,
/// or takes half its credits. The engineer and the infiltrator are spent (they leave the map without a death). The tech buildings of the
/// map (loose ends, stage 1): an engineer takes one of the neutral side or an enemy, or mends its own; a hurt soldier is healed whole
/// at its own hospital and stays outside. An engineer going into a bridge repair hut mends its bridge and is spent (stage 2). A soldier
/// going into its owner's cloning vats leaves for half its price (RA2). One that stopped farther off, or can no longer do it, forgets the order; so does one given
/// another order.
/// </summary>
public static class EntrySystem
{
    /// <summary>How far from the footprint a soldier goes in, in cells: a group of five crowds round the door.</summary>
    public const int DoorReach = 2;

    /// <summary>The ticks of a blackout (RA2 <c>SpyPowerBlackout=1000</c> frames).</summary>
    public const int BlackoutTicks = 1000;

    /// <summary>The share of its owner's credits an infiltrator takes from a refinery, in percent (RA2 <c>SpyMoneyStealPercent=.5</c>).</summary>
    public const int StealPercent = 50;

    /// <summary>What <paramref name="unit"/> would do going into <paramref name="target"/>, a building or a transport, now.</summary>
    public static EntryAction ActionOf(GameWorld world, Unit unit, Entity target)
    {
        if (target is Unit transport)
        {
            return TransportSystem.CanBoard(world, unit, transport) ? EntryAction.Board : EntryAction.None;
        }

        if (target is not Building { IsAlive: true, IsReady: true } building || unit.Archetype.Category != UnitCategory.Infantry
            || unit.Archetype.Flies)
        {
            return EntryAction.None;
        }

        var own = building.Owner == unit.Owner;
        var enemy = !building.Owner.IsNeutral && !world.AreAllies(unit.Owner, building.Owner);
        if (building.Def is not { } def)
        {
            // A building of the map: an engineer takes a tech building of the neutral side or an enemy, or mends its own; a hurt soldier
            // is healed at its own hospital.
            // RA2: the commando's C4 on a bridge repair hut brings its bridge down.
            if (!unit.Archetype.Has(UnitTalents.Engineer) && BridgeDemolition.CanBlowHut(world, unit, building))
            {
                return EntryAction.BlowBridge;
            }

            return unit.Archetype.Has(UnitTalents.Engineer)
                ? building.Kind == BuildingKind.TechBridgeRepairHut ? BridgeRepair.Needed(world, building) ? EntryAction.RepairBridge : EntryAction.None
                    : own && building.Hp < building.MaxHp && building.Type.Capturable ? EntryAction.Mend
                    // A damaged civilian building that holds soldiers, nobody's or its own: mended whole, it takes soldiers again (RA2).
                    : (own || building.Owner.IsNeutral) && building.Hp < building.MaxHp && GarrisonSystem.Civilian(building) ? EntryAction.Mend
                    : (enemy || building.Owner.IsNeutral) && building.Type.Capturable ? EntryAction.Capture
                    : EntryAction.None
                : own && building.Kind == BuildingKind.TechHospital && unit.Hp < unit.MaxHp && !unit.Archetype.Has(UnitTalents.Infiltrator)
                    ? EntryAction.Heal
                // A civilian building takes soldiers: when it is nobody's or theirs, while there is room.
                : GarrisonSystem.CanEnter(unit) && GarrisonSystem.Holds(building, unit.Owner) && GarrisonSystem.HasRoom(building)
                    ? EntryAction.Garrison
                    : EntryAction.None;
        }

        if (unit.Archetype.Has(UnitTalents.Engineer))
        {
            return own && building.Hp < building.MaxHp && !def.Wall ? EntryAction.Mend
                : enemy && def.Capturable && def.Wall is false ? EntryAction.Capture
                : EntryAction.None;
        }

        // RA2: a tesla trooper charges its owner's tesla coil, standing by it.
        if (own && def.OverchargeWeapon.Length > 0 && unit.Archetype.Has(UnitTalents.ChargesCoils))
        {
            return unit.Charging == building.Id ? EntryAction.None : EntryAction.Charge;
        }

        // RA2 cloning vats: any other soldier of the owner's but one that teleports goes in for half its price.
        if (own && def.ClonesInfantry)
        {
            return unit.Archetype.Has(UnitTalents.Teleports) ? EntryAction.None : EntryAction.Recycle;
        }

        if (unit.Archetype.Has(UnitTalents.Infiltrator))
        {
            return enemy && def.SpyEffect != SpyEffect.None ? EntryAction.Infiltrate : EntryAction.None;
        }

        return own && GarrisonSystem.CanEnter(unit) && GarrisonSystem.Holds(building, unit.Owner) && GarrisonSystem.HasRoom(building)
            ? EntryAction.Garrison
            : EntryAction.None;
    }

    /// <summary>
    /// Sends the units among <paramref name="units"/> that can do something in the building to it, each to a place of its own round the
    /// cell beside it nearest to the first of them. False when none can.
    /// </summary>
    internal static bool Order(GameWorld world, List<Unit> units, EntityId targetId)
    {
        if (!world.TryGet(targetId, out var target) || target is not (Building or Unit))
        {
            return false;
        }

        var going = units.FindAll(unit => ActionOf(world, unit, target) != EntryAction.None);
        if (going.Count == 0)
        {
            return false;
        }

        var door = Door(world, target, going[0]);
        foreach (var (unit, destination) in GroupDestinations.Assign(world, going, door))
        {
            UnitOrders.ForgetTarget(unit);
            UnitOrders.GiveRoute(world, unit, [new UnitOrder(destination, AttackMove: false)], loop: false, append: false);
            unit.EnterTarget = target.Id;
        }

        return true;
    }

    /// <summary>After the movement: the soldiers at the door go in; those that cannot get there or do it forget the order.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        List<(Unit Unit, Entity Target, EntryAction Action)>? entering = null;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, EnterTarget: { } id } unit)
            {
                continue;
            }

            var action = world.TryGet(id, out var target) ? ActionOf(world, unit, target) : EntryAction.None;
            if (action == EntryAction.None)
            {
                unit.EnterTarget = null;
            }
            else if (Outside(target!, unit.Position.ToCell()) <= DoorReach)
            {
                (entering ??= []).Add((unit, target!, action));
            }
            else if (unit.MoveTarget is null && !unit.PathPending)
            {
                unit.EnterTarget = null;
            }
        }

        // After the walk: a building taken lets its soldiers out, and the list of entities must not change while walked. One by one,
        // in id order, each asks again: the bunker may have filled, the building changed hands.
        foreach (var (unit, target, _) in entering ?? [])
        {
            unit.EnterTarget = null;
            var building = target as Building;
            switch (ActionOf(world, unit, target))
            {
                case EntryAction.Board:
                    TransportSystem.Board(world, unit, (Unit)target, events);
                    break;
                case EntryAction.Garrison:
                    GarrisonSystem.Enter(world, unit, building!, events);
                    break;
                case EntryAction.Capture:
                    Capture(world, building!, unit.Owner, events);
                    unit.MarkDead();
                    break;
                case EntryAction.Mend:
                    world.SetHp(building!, building!.MaxHp, events);
                    events?.Add(new BuildingMended(world.CurrentTick, building.Id));
                    unit.MarkDead();
                    break;
                case EntryAction.Infiltrate:
                    Infiltrate(world, building!, unit.Owner, events);
                    unit.MarkDead();
                    break;
                case EntryAction.RepairBridge:
                    BridgeRepair.Mend(world, building!, events);
                    unit.MarkDead();
                    break;
                case EntryAction.Heal:
                    // RA2: in at the door, out whole.
                    world.SetHp(unit, unit.MaxHp, events);
                    events?.Add(new UnitHealed(world.CurrentTick, unit.Id, building!.Id));
                    break;
                case EntryAction.BlowBridge:
                    BridgeDemolition.PlantAtHut(world, unit, building!, events);
                    break;
                case EntryAction.Charge:
                    UnitOrders.Stop(world, unit);
                    unit.Charging = building!.Id;
                    break;
                case EntryAction.Recycle:
                    var credits = RecycleRefund(unit);
                    world.Player(unit.Owner).Credits += credits;
                    events?.Add(new UnitRecycled(world.CurrentTick, unit.Id, building!.Id, credits));
                    unit.MarkDead();
                    break;
            }
        }
    }

    /// <summary>
    /// The soldiers charging each building (RA2 tesla troopers by a tesla coil), counted anew before the defenses look: one that stands
    /// within <see cref="DoorReach"/> of its own living building that can be charged still charges it; any other forgets it.
    /// </summary>
    public static void CountChargers(GameWorld world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building building)
            {
                building.Chargers = 0;
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, Charging: { } id } unit)
            {
                continue;
            }

            if (world.TryGet(id, out var found) && found is Building { IsAlive: true, Def.OverchargeWeapon.Length: > 0 } coil
                && coil.Owner == unit.Owner && unit.MoveTarget is null && Outside(coil, unit.Position.ToCell()) <= DoorReach)
            {
                coil.Chargers++;
            }
            else
            {
                unit.Charging = null;
            }
        }
    }

    /// <summary>What a soldier going into its owner's cloning vats leaves for: half its price, rounded down.</summary>
    public static int RecycleRefund(Unit unit) => unit.Archetype.Cost / 2;

    /// <summary>
    /// The building changes hands: its soldiers come out for the old owner, its sight and veil leave the old owner's grid (the new owner's
    /// come with <see cref="VisibilitySystem"/>), its target, repair and rally point go; power and the tree are worked out again.
    /// </summary>
    internal static void Capture(GameWorld world, Building building, PlayerId to, List<SimEvent>? events)
    {
        var from = building.Owner;
        GarrisonSystem.Evacuate(world, building, events);
        if (building.SightCell is { } cell)
        {
            world.VisionOf(from).Hide(cell, building.SightRadius);
            building.SightCell = null;
            building.SightRadius = 0;
        }

        if (building.VeilRadius > 0)
        {
            world.VisionOf(from).Veil(new CellPos(building.Cell.X + building.Width / 2, building.Cell.Y + building.Height / 2), building.VeilRadius, -1);
            building.VeilRadius = 0;
        }

        building.Owner = to;
        building.Target = null;
        building.Repairing = false;
        building.RallyPoint = null;
        if (building.Def is null)
        {
            TechBuildingSystem.Taken(world, building, from);
        }

        world.BuildingsChanged();
        events?.Add(new BuildingCaptured(world.CurrentTick, building.Id, from, to));
    }

    private static void Infiltrate(GameWorld world, Building building, PlayerId spy, List<SimEvent>? events)
    {
        var owner = world.Player(building.Owner);
        var effect = building.Def!.SpyEffect;
        var credits = 0;
        switch (effect)
        {
            case SpyEffect.Blackout:
                owner.BlackoutUntil = world.CurrentTick + BlackoutTicks;
                world.PowerDirty = true;
                break;
            case SpyEffect.ResetMap:
                world.VisionOf(building.Owner).ForgetExplored();
                break;
            case SpyEffect.ResetCharge:
                building.ChargeLeft = building.Def.Recharge;
                break;
            case SpyEffect.VeteranInfantry:
                world.Player(spy).VeteranInfantry = true;
                break;
            case SpyEffect.VeteranVehicles:
                world.Player(spy).VeteranVehicles = true;
                break;
            case SpyEffect.StealTech:
                world.Player(spy).StolenTech.Add(building.Def.Faction);
                world.TechDirty = true;
                break;
            case SpyEffect.StealCredits:
                credits = owner.Credits * StealPercent / 100;
                owner.Credits -= credits;
                world.Player(spy).Credits += credits;
                break;
        }

        events?.Add(new BuildingInfiltrated(world.CurrentTick, building.Id, spy, effect, credits));
    }

    /// <summary>
    /// How many cells <paramref name="cell"/> lies outside the footprint of a building, or off the cell of a transport: 0 inside, 1 in the
    /// ring around it, and so on.
    /// </summary>
    private static int Outside(Entity target, CellPos cell)
    {
        var (near, far) = target is Building building ? (building.Cell, building.FarCell) : (((Unit)target).Position.ToCell(), ((Unit)target).Position.ToCell());
        return IntMath.Max(
            IntMath.Max(near.X - cell.X, cell.X - far.X),
            IntMath.Max(IntMath.Max(near.Y - cell.Y, cell.Y - far.Y), 0));
    }

    /// <summary>The cell of the ring around the footprint nearest to the soldier that it can enter; the footprint's corner without one.</summary>
    private static WorldPos Door(GameWorld world, Entity target, Unit soldier)
    {
        var from = soldier.Position.ToCell();
        var (near, far) = target is Building building ? (building.Cell, building.FarCell) : (((Unit)target).Position.ToCell(), ((Unit)target).Position.ToCell());
        CellPos? best = null;
        var bestDistance = int.MaxValue;
        for (var y = near.Y - 1; y <= far.Y + 1; y++)
        {
            for (var x = near.X - 1; x <= far.X + 1; x++)
            {
                var cell = new CellPos(x, y);
                if (Outside(target, cell) != 1 || !world.Map.CanEnter(cell, soldier.Archetype.Locomotor))
                {
                    continue;
                }

                var distance = ((x - from.X) * (x - from.X)) + ((y - from.Y) * (y - from.Y));
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (cell, distance);
                }
            }
        }

        return (best ?? near).Center;
    }
}
