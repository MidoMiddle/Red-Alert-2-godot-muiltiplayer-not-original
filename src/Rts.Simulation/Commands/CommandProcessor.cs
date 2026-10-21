using System;
using System.Collections.Generic;
using Rts.Simulation.Events;
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
        if (!world.Contains(command.Position) || !UnitArchetypes.IsKnown(command.UnitType))
        {
            return false;
        }

        var unit = world.SpawnUnit(command.Player, command.UnitType, command.Position);
        events?.Add(new UnitSpawned(world.CurrentTick, unit.Id, unit.Owner, unit.Type, unit.Position));
        return true;
    }

    // Invalid target rejects the whole command; foreign, dead or unknown units are skipped one by one.
    private static bool ApplyMove(GameWorld world, MoveUnitsCommand command, List<SimEvent>? events)
    {
        if (!world.Contains(command.Target))
        {
            return false;
        }

        var changed = false;
        foreach (var id in command.Units)
        {
            if (world.TryGet(id, out var entity)
                && entity is Unit unit
                && unit.IsAlive
                && unit.Owner == command.Player)
            {
                unit.MoveTarget = command.Target;
                events?.Add(new UnitMoveStarted(world.CurrentTick, unit.Id, command.Target));
                changed = true;
            }
        }

        return changed;
    }
}
