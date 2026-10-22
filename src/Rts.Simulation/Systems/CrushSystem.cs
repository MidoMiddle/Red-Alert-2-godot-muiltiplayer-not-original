using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Crushing infantry (docs/implementation/M4_combat.md, 4.13). <see cref="MovementSystem"/> asks before a vehicle takes a new cell: a
/// <c>crusher</c> drives into a cell without a vehicle whose soldiers are all <c>crushable</c> and none its own — another player's or
/// neutral ones, as RA2 crushes civilians — and they all die at once, by spot, freeing their spots so the vehicle takes the cell on
/// the same tick (<see cref="EntityDestroyed"/> with <see cref="DeathCause.Crushed"/>). With one of its own soldiers there, or one that
/// cannot be crushed, the cell stays taken and the vehicle drives around or asks its own to make room, as before. A vehicle does not
/// seek infantry to crush (no RA2 <c>AutoCrush</c>), and infantry does not flee from it: that is the player's micro.
/// </summary>
public static class CrushSystem
{
    /// <summary>Crushes the soldiers in <paramref name="cell"/> under <paramref name="vehicle"/> if it may; true when it did.</summary>
    public static bool TryCrush(GameWorld world, Unit vehicle, CellPos cell, List<SimEvent>? events)
    {
        var reservations = world.Reservations;
        if (!vehicle.Archetype.Crusher || reservations.VehicleAt(cell) != EntityId.None || reservations.InfantryCount(cell) == 0)
        {
            return false;
        }

        for (var spot = 0; spot < Reservations.SubcellsPerCell; spot++)
        {
            var id = reservations.InfantryAt(cell, spot);
            if (id != EntityId.None && (!world.TryGet(id, out var entity) || entity is not Unit { Archetype.Crushable: true } soldier
                || soldier.Owner == vehicle.Owner))
            {
                return false;
            }
        }

        for (var spot = 0; spot < Reservations.SubcellsPerCell; spot++)
        {
            var id = reservations.InfantryAt(cell, spot);
            if (id == EntityId.None || !world.TryGet(id, out var entity))
            {
                continue;
            }

            var soldier = (Unit)entity;
            reservations.Leave(cell, soldier);
            if (soldier.IsAlive)
            {
                DeathSystem.Kill(world, soldier, vehicle.Id, vehicle.Owner, DeathCause.Crushed, -1, events);
            }
        }

        return true;
    }
}
