using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Units carrying units (stage 3d of filling the game: the hovercraft, the landing craft, the IFV; RA2 <c>Passengers</c>). A unit boards
/// its owner's transport through an <see cref="Commands.EnterCommand"/>, at the door as into a building (<see cref="EntrySystem"/>), while
/// the passengers' slots (<see cref="UnitArchetype.Size"/>: infantry 1, a vehicle 3, the largest 6) leave room for it and it is no
/// larger than the transport takes; a flier, a transport with passengers of its own and the transport itself do not. It leaves the map
/// and rides as an <see cref="Occupant"/> with its hit points. A <see cref="Commands.DeployCommand"/> on the transport lets them out on
/// the free cells round it, ring by ring up to 4 out, each where it can stand; those with no room stay in. Passengers fall with their
/// transport. An IFV fires the weapon its first passenger gives it (<see cref="Rules.GameRules.WeaponsOf(Unit)"/>).
/// </summary>
public static class TransportSystem
{
    /// <summary>The slots the passengers of <paramref name="transport"/> take.</summary>
    public static int SlotsTaken(GameWorld world, Unit transport)
    {
        var taken = 0;
        foreach (var passenger in transport.Passengers)
        {
            taken += world.Rules.Unit(passenger.Type).Size;
        }

        return taken;
    }

    /// <summary>Whether <paramref name="unit"/> may board <paramref name="transport"/> now.</summary>
    public static bool CanBoard(GameWorld world, Unit unit, Unit transport) =>
        transport is { IsAlive: true, Archetype.PassengerSlots: > 0 } && transport != unit && transport.Owner == unit.Owner
        && !unit.Archetype.Flies && unit.Passengers.Count == 0 && unit.Archetype.Size <= transport.Archetype.PassengerSizeLimit
        && SlotsTaken(world, transport) + unit.Archetype.Size <= transport.Archetype.PassengerSlots;

    /// <summary>The unit boards: it leaves the map without a death and rides with its hit points.</summary>
    internal static void Board(GameWorld world, Unit unit, Unit transport, List<SimEvent>? events)
    {
        transport.Passengers.Add(new Occupant(unit.Archetype, unit.Hp));
        unit.MarkDead();
        events?.Add(new UnitBoarded(world.CurrentTick, unit.Id, transport.Id));
    }

    /// <summary>The passengers come out round the transport, in the order they boarded, each on a free cell it can stand on.</summary>
    internal static bool Unload(GameWorld world, Unit transport, List<SimEvent>? events)
    {
        var centre = transport.Position.ToCell();
        var left = 0;
        var any = false;
        while (left < transport.Passengers.Count)
        {
            var passenger = transport.Passengers[left];
            var archetype = world.Rules.Unit(passenger.Type);
            if (FreeCellAround(world, centre, archetype) is not { } cell)
            {
                left++;
                continue;
            }

            transport.Passengers.RemoveAt(left);
            var unit = world.SpawnUnit(transport.Owner, passenger.Type, cell.Center, snapToSubcell: true);
            world.SetHp(unit, passenger.Hp, events);
            events?.Add(new UnitUnloaded(world.CurrentTick, unit.Id, transport.Id));
            any = true;
        }

        return any;
    }

    /// <summary>The nearest free cell round <paramref name="centre"/>, ring by ring up to 4 out, row by row in a ring; null without one.</summary>
    internal static CellPos? FreeCellAround(GameWorld world, CellPos centre, UnitArchetype archetype)
    {
        for (var ring = 1; ring <= 4; ring++)
        {
            for (var y = centre.Y - ring; y <= centre.Y + ring; y++)
            {
                for (var x = centre.X - ring; x <= centre.X + ring; x++)
                {
                    var cell = new CellPos(x, y);
                    if (IntMath.Max(IntMath.Abs(x - centre.X), IntMath.Abs(y - centre.Y)) == ring && world.Map.Contains(cell)
                        && world.Map.CanEnter(cell, archetype.Locomotor) && world.Reservations.HasRoomFor(cell, archetype.Placement, EntityId.None))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }
}
