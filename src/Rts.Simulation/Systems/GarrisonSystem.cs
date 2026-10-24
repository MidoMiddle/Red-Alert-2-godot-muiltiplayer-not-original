using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Soldiers inside buildings (stage 2e of filling the game: the fortified bunker, RA2 battle bunker). A <see cref="GarrisonCommand"/>
/// sends the player's soldiers to its ready building that holds soldiers (<see cref="BuildingDef.GarrisonCapacity"/>); after the
/// movement of the tick each one that stands within <see cref="DoorReach"/> cells of the footprint goes in while there is room: it leaves
/// the map (no death, no event of one) and becomes an <see cref="Occupant"/> with its hit points. One that stopped farther off, or found
/// the building full, gone or no longer its owner's, forgets the order; so does one given another order. The soldiers inside fire with
/// their weapons from the building (<see cref="WeaponSystem"/>), their range counted from its wall (<see cref="Wall"/>), at the target
/// the building chooses for them (<see cref="TargetingSystem"/>). They come out around the footprint, ring by ring, on an
/// <see cref="EvacuateCommand"/>, when the building is sold and when a shot brings it down; one that finds no room stays inside, or
/// falls with the building.
/// </summary>
public static class GarrisonSystem
{
    /// <summary>How far from the footprint a soldier goes in, in cells: a group of five crowds round the door.</summary>
    public const int DoorReach = 2;

    /// <summary>Whether the unit may go into a building: soldiers only, and none with a jet pack (stage 3a).</summary>
    public static bool CanEnter(Unit unit) => unit.Archetype.Category == UnitCategory.Infantry && !unit.Archetype.Flies;

    /// <summary>Whether <paramref name="building"/> is a ready building of <paramref name="player"/> that holds soldiers.</summary>
    public static bool Holds(Building building, PlayerId player) =>
        building is { IsAlive: true, IsReady: true, Def.GarrisonCapacity: > 0 } && building.Owner == player;

    /// <summary>Whether the building has room for one more soldier.</summary>
    public static bool HasRoom(Building building) => building.Occupants.Count < (building.Def?.GarrisonCapacity ?? 0);

    /// <summary>Whether one of the soldiers inside carries a weapon: then the building chooses targets and fires.</summary>
    public static bool IsArmed(Building building)
    {
        foreach (var occupant in building.Occupants)
        {
            if (occupant.Weapons.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The weapons of the soldiers inside, for the choice of a target: every weapon of every one, in the order they went in.</summary>
    public static ImmutableArray<int> WeaponsOf(GameRules rules, Building building)
    {
        var weapons = ImmutableArray.CreateBuilder<int>();
        foreach (var occupant in building.Occupants)
        {
            weapons.AddRange(rules.WeaponsOf(occupant.Type));
        }

        return weapons.ToImmutable();
    }

    /// <summary>The leptons from the centre of the building to its wall, along its shorter side: the range of the soldiers starts there.</summary>
    public static int Wall(Building building) => IntMath.Min(building.Width, building.Height) * WorldPos.LeptonsPerCell / 2;

    /// <summary>
    /// Sends the soldiers among <paramref name="units"/> to the building, each to a place of its own round the cell beside it nearest
    /// to the first of them. False when the building cannot hold them or none is a soldier.
    /// </summary>
    internal static bool Order(GameWorld world, List<Unit> units, EntityId buildingId)
    {
        if (!world.TryGet(buildingId, out var entity) || entity is not Building building || units.Count == 0
            || !Holds(building, units[0].Owner) || !HasRoom(building))
        {
            return false;
        }

        var soldiers = units.FindAll(CanEnter);
        if (soldiers.Count == 0)
        {
            return false;
        }

        var door = Door(world, building, soldiers[0]);
        foreach (var (unit, destination) in GroupDestinations.Assign(world, soldiers, door))
        {
            UnitOrders.ForgetTarget(unit);
            UnitOrders.GiveRoute(world, unit, [new UnitOrder(destination, AttackMove: false)], loop: false, append: false);
            unit.EnterTarget = building.Id;
        }

        return true;
    }

    /// <summary>After the movement: the soldiers at the door go in; those that cannot get there or in forget the order.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, EnterTarget: { } id } unit)
            {
                continue;
            }

            if (!world.TryGet(id, out var target) || target is not Building building || !Holds(building, unit.Owner))
            {
                unit.EnterTarget = null;
            }
            else if (Outside(building, unit.Position.ToCell()) <= DoorReach)
            {
                if (HasRoom(building))
                {
                    Enter(world, unit, building, events);
                }
                else
                {
                    unit.EnterTarget = null;
                }
            }
            else if (unit.MoveTarget is null && !unit.PathPending)
            {
                unit.EnterTarget = null;
            }
        }
    }

    /// <summary>
    /// The soldiers inside come out around the footprint, in the order they went in, as new units with their hit points; those that
    /// find no free cell within 4 rings stay inside. The building forgets its target once it is empty.
    /// </summary>
    public static void Evacuate(GameWorld world, Building building, List<SimEvent>? events)
    {
        var rules = world.Rules;
        var left = 0;
        while (left < building.Occupants.Count)
        {
            var occupant = building.Occupants[left];
            var archetype = rules.Unit(occupant.Type);
            if (ProductionSystem.ExitAround(world, building, archetype) is not { } cell)
            {
                left++;
                continue;
            }

            building.Occupants.RemoveAt(left);
            var unit = world.SpawnUnit(building.Owner, occupant.Type, cell.Center, snapToSubcell: true);
            world.SetHp(unit, occupant.Hp, events);
            events?.Add(new UnitLeftGarrison(world.CurrentTick, unit.Id, building.Id));
        }

        if (building.Occupants.Count == 0)
        {
            building.Target = null;
        }
    }

    private static void Enter(GameWorld world, Unit unit, Building building, List<SimEvent>? events)
    {
        building.Occupants.Add(new Occupant(unit.Archetype, unit.Hp));
        unit.EnterTarget = null;
        unit.MarkDead();
        events?.Add(new UnitGarrisoned(world.CurrentTick, unit.Id, building.Id));
    }

    /// <summary>How many cells <paramref name="cell"/> lies outside the footprint: 0 inside, 1 in the ring around it, and so on.</summary>
    private static int Outside(Building building, CellPos cell) =>
        IntMath.Max(
            IntMath.Max(building.Cell.X - cell.X, cell.X - building.FarCell.X),
            IntMath.Max(IntMath.Max(building.Cell.Y - cell.Y, cell.Y - building.FarCell.Y), 0));

    /// <summary>The cell of the ring around the footprint nearest to the soldier that it can enter; the footprint's corner without one.</summary>
    private static WorldPos Door(GameWorld world, Building building, Unit soldier)
    {
        var from = soldier.Position.ToCell();
        CellPos? best = null;
        var bestDistance = int.MaxValue;
        for (var y = building.Cell.Y - 1; y <= building.FarCell.Y + 1; y++)
        {
            for (var x = building.Cell.X - 1; x <= building.FarCell.X + 1; x++)
            {
                var cell = new CellPos(x, y);
                if (Outside(building, cell) != 1 || !world.Map.CanEnter(cell, soldier.Archetype.Locomotor))
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

        return (best ?? building.Cell).Center;
    }
}
