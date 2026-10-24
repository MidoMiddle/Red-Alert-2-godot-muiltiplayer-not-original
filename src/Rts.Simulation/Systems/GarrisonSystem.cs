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
/// Soldiers inside buildings (stage 2e of filling the game: the fortified bunker, RA2 battle bunker). A <see cref="EnterCommand"/>
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
    /// <summary>
    /// Whether the unit may sit in a building: soldiers only — none with a jet pack (stage 3a), no engineer nor infiltrator (stage 3b,
    /// RA2 <c>Occupier</c>).
    /// </summary>
    public static bool CanEnter(Unit unit) =>
        unit.Archetype.Category == UnitCategory.Infantry && !unit.Archetype.Flies
        && !unit.Archetype.Has(UnitTalents.Engineer) && !unit.Archetype.Has(UnitTalents.Infiltrator);

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

    /// <summary>The soldier goes in: it leaves the map without a death and becomes an occupant with its hit points.</summary>
    internal static void Enter(GameWorld world, Unit unit, Building building, List<SimEvent>? events)
    {
        building.Occupants.Add(new Occupant(unit.Archetype, unit.Hp));
        unit.EnterTarget = null;
        unit.MarkDead();
        events?.Add(new UnitGarrisoned(world.CurrentTick, unit.Id, building.Id));
    }
}
