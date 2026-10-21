using System;
using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Validates and applies commands to the world. Every peer runs exactly the same checks, so an invalid
/// command is ignored silently and identically everywhere. Never throws for a bad command: a malicious
/// or stale command must not be able to crash the game.
/// </summary>
public static class CommandProcessor
{
    /// <summary>Applies the command. Returns true if the world changed.</summary>
    /// <param name="events">Receives events about what changed; null when the caller does not need them.</param>
    public static bool Apply(GameWorld world, Command command, List<SimEvent>? events = null)
    {
        if (!IsPlayablePlayer(command.Player))
        {
            return false;
        }

        return command switch
        {
            SpawnTestUnitCommand spawn => ApplySpawn(world, spawn, events),
            MoveUnitsCommand move => ApplyMove(world, move, events),
            _ => throw new ArgumentException($"Unknown command type {command.GetType().Name}.", nameof(command)),
        };
    }

    private static bool IsPlayablePlayer(PlayerId player) => player.Value >= 1 && player.Value <= PlayerId.MaxPlayers;

    private static bool ApplySpawn(GameWorld world, SpawnTestUnitCommand command, List<SimEvent>? events)
    {
        if (!world.Contains(command.Position)
            || !UnitArchetypes.IsKnown(command.UnitType)
            || !world.Map.CanEnter(command.Position.ToCell(), UnitArchetypes.Get(command.UnitType).Locomotor)
            || !world.Reservations.IsFree(command.Position.ToCell()))
        {
            return false;
        }

        var unit = world.SpawnUnit(command.Player, command.UnitType, command.Position);
        events?.Add(new UnitSpawned(world.CurrentTick, unit.Id, unit.Owner, unit.Type, unit.Position));
        return true;
    }

    // Invalid target rejects the whole command; foreign, dead, unknown or repeated units are skipped one by one.
    private static bool ApplyMove(GameWorld world, MoveUnitsCommand command, List<SimEvent>? events)
    {
        if (!world.Contains(command.Target))
        {
            return false;
        }

        var units = new List<Unit>();
        foreach (var id in command.Units)
        {
            if (world.TryGet(id, out var entity)
                && entity is Unit unit
                && unit.IsAlive
                && unit.Owner == command.Player
                && !units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        // Each unit of a group gets a cell of its own near the target (docs/implementation/M2_unit_control.md, 4.6).
        foreach (var (unit, destination) in GroupDestinations.Assign(world, units, command.Target))
        {
            UnitOrders.MoveTo(world, unit, destination);
            events?.Add(new UnitMoveStarted(world.CurrentTick, unit.Id, destination));
        }

        return units.Count > 0;
    }
}
