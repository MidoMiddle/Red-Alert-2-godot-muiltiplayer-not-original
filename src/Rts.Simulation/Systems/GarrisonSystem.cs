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
/// <para>
/// The civilian buildings of the map (RA2 garrisons): a house, an apartment block, a school… that holds soldiers
/// (<see cref="BuildingType.Garrison"/>) takes the soldiers of any player while it stands empty and neutral — the first one in makes it
/// his — and more of his while there is room; an enemy's is closed until it is empty again. Empty — its soldiers out on an order, or
/// their owner beaten — it is the neutral side's again. The tech buildings are taken by engineers, not soldiers.
/// The soldiers inside are never hurt: the building is. Once it is critically damaged (<see cref="DamageState.Critical"/>, under a
/// quarter of its hit points) they all come out after the damage of the tick, as they went in and with their hit points, and it takes
/// none again until an engineer of any player mends it whole (<see cref="EntrySystem"/>).
/// </para>
/// </summary>
public static class GarrisonSystem
{
    /// <summary>
    /// Whether the unit may sit in a building: soldiers only — none with a jet pack (stage 3a), no engineer nor infiltrator (stage 3b,
    /// RA2 <c>Occupier</c>).
    /// </summary>
    /// <summary>How far a civilian building with soldiers inside sees, in cells (RA2: 4).</summary>
    public const int Sight = 4;

    public static bool CanEnter(Unit unit) =>
        // RA2 Occupier: the GI and the conscript; not one held by a mind control.
        unit.Archetype.Has(UnitTalents.Occupier) && !unit.Archetype.Flies && unit.ControlledBy is null;

    /// <summary>
    /// Whether <paramref name="building"/> is a ready building that takes the soldiers of <paramref name="player"/>: one of his that holds
    /// soldiers, or a civilian building of the map that is his or nobody's.
    /// </summary>
    public static bool Holds(Building building, PlayerId player) =>
        building is { IsAlive: true, IsReady: true } && Capacity(building) > 0
        && (building.Owner == player || (Civilian(building) && building.Owner.IsNeutral))
        && !(Civilian(building) && Critical(building));

    /// <summary>Whether the building is critically damaged: a civilian one lets its soldiers out and takes none.</summary>
    public static bool Critical(Building building) => DamageStates.Of(building.Hp, building.MaxHp) == DamageState.Critical;

    /// <summary>
    /// After the damage of the tick: the soldiers in a civilian building critically damaged come out, in id order of the buildings.
    /// </summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        List<Building>? emptying = null;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Occupants.Count: > 0 } building && Civilian(building) && Critical(building))
            {
                (emptying ??= []).Add(building);
            }
        }

        // The soldiers come out after the walk: the list of entities must not change while walked.
        foreach (var building in emptying ?? [])
        {
            Evacuate(world, building, events);
        }
    }

    /// <summary>Whether the building has room for one more soldier.</summary>
    public static bool HasRoom(Building building) => building.Occupants.Count < Capacity(building);

    /// <summary>How many soldiers the building holds: its data's, or a civilian building's of the map; a tech building none.</summary>
    public static int Capacity(Building building) =>
        building.Def is { } def ? def.GarrisonCapacity
        : BuildingTypes.IsKnown(building.Kind) && !building.Type.Capturable ? building.Type.Garrison
        : 0;

    /// <summary>A civilian building of the map that holds soldiers: it changes hands with them.</summary>
    public static bool Civilian(Building building) => building.Def is null && Capacity(building) > 0;

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
            weapons.AddRange(rules.GarrisonWeaponsOf(occupant.Type));
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
            if (building.IsAlive && Civilian(building) && !building.Owner.IsNeutral)
            {
                // Empty, a civilian building is nobody's again.
                SetOwner(world, building, PlayerId.Neutral);
            }
        }
    }

    /// <summary>
    /// The player is beaten: his soldiers in a civilian building die with him, and the building is the neutral side's again (it does not
    /// fall as his own buildings do).
    /// </summary>
    internal static void Abandon(GameWorld world, Building building)
    {
        building.Occupants.Clear();
        building.Target = null;
        SetOwner(world, building, PlayerId.Neutral);
    }

    // The building changes hands: the old owner's sight of it goes (the new owner's comes with VisibilitySystem); its target goes.
    private static void SetOwner(GameWorld world, Building building, PlayerId to)
    {
        if (building.SightCell is { } cell)
        {
            world.VisionOf(building.Owner).Hide(cell, building.SightRadius);
            building.SightCell = null;
            building.SightRadius = 0;
        }

        building.Owner = to;
        building.Target = null;
        building.TargetOrder = TargetOrder.None;
        world.BuildingsChanged();
    }

    /// <summary>The soldier goes in: it leaves the map without a death and becomes an occupant with its hit points.</summary>
    internal static void Enter(GameWorld world, Unit unit, Building building, List<SimEvent>? events)
    {
        if (Civilian(building) && building.Owner.IsNeutral)
        {
            // The first one in makes a civilian building his.
            SetOwner(world, building, unit.Owner);
        }

        building.Occupants.Add(new Occupant(unit.Archetype, unit.Hp));
        unit.EnterTarget = null;
        unit.MarkDead();
        events?.Add(new UnitGarrisoned(world.CurrentTick, unit.Id, building.Id));
    }
}
