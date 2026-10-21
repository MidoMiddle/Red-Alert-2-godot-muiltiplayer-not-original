using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Movement;
using Rts.Simulation.Pathfinding;
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
            MoveCommand move => ApplyRoute(world, move, [move.Target], loop: false, move.Queue, attackMove: false, events),
            AttackMoveCommand attack => ApplyRoute(world, attack, [attack.Target], loop: false, attack.Queue, attackMove: true, events),
            WaypointsCommand route => ApplyRoute(world, route, route.Points, route.Loop, append: false, attackMove: false, events),
            StopCommand stop => ApplyStop(world, stop),
            ScatterCommand scatter => ApplyScatter(world, scatter, events),
            DamageMapObjectCommand damage => damage.Amount is >= 1 and <= DamageMapObjectCommand.MaxAmount
                && world.Map.Contains(damage.Cell) && world.DamageMapObject(damage.Cell, damage.Amount, events),
            _ => throw new ArgumentException($"Unknown command type {command.GetType().Name}.", nameof(command)),
        };
    }

    private static bool IsPlayablePlayer(PlayerId player) => player.Value >= 1 && player.Value <= PlayerId.MaxPlayers;

    private static bool ApplySpawn(GameWorld world, SpawnTestUnitCommand command, List<SimEvent>? events)
    {
        if (!world.Contains(command.Position)
            || !UnitArchetypes.IsKnown(command.UnitType)
            || !world.Map.CanEnter(command.Position.ToCell(), UnitArchetypes.Get(command.UnitType).Locomotor)
            || !world.Reservations.HasRoomFor(command.Position.ToCell(), UnitArchetypes.Get(command.UnitType).Placement, EntityId.None))
        {
            return false;
        }

        var unit = world.SpawnUnit(command.Player, command.UnitType, command.Position, snapToSubcell: true);
        events?.Add(new UnitSpawned(world.CurrentTick, unit.Id, unit.Owner, unit.Type, unit.Position));
        return true;
    }

    // A point outside the map rejects the whole command; foreign, dead, unknown or repeated units are skipped one by one.
    private static bool ApplyRoute(
        GameWorld world, UnitsCommand command, ImmutableArray<WorldPos> points, bool loop, bool append, bool attackMove, List<SimEvent>? events)
    {
        foreach (var point in points)
        {
            if (!world.Contains(point))
            {
                return false;
            }
        }

        var units = OwnUnits(world, command);
        if (units.Count == 0)
        {
            return false;
        }

        // Each unit of a group gets a place of its own at every point, laid out now in the group's current shape
        // (docs/implementation/M2_unit_control.md, 4.6 and 4.10).
        var legs = new Dictionary<EntityId, ImmutableArray<UnitOrder>.Builder>();
        foreach (var unit in units)
        {
            legs[unit.Id] = ImmutableArray.CreateBuilder<UnitOrder>(points.Length);
        }

        foreach (var point in points)
        {
            foreach (var (unit, destination) in GroupDestinations.Assign(world, units, point))
            {
                legs[unit.Id].Add(new UnitOrder(destination, attackMove));
            }
        }

        foreach (var unit in units)
        {
            var route = legs[unit.Id].ToImmutable();
            UnitOrders.GiveRoute(world, unit, route, loop, append);
            events?.Add(new UnitMoveStarted(world.CurrentTick, unit.Id, route[0].Point));
        }

        return true;
    }

    private static bool ApplyStop(GameWorld world, StopCommand command)
    {
        var units = OwnUnits(world, command);
        foreach (var unit in units)
        {
            UnitOrders.Stop(world, unit);
        }

        return units.Count > 0;
    }

    // Units in id order, each drawing from the simulation's generator: a random free neighbouring cell it can step to.
    private static bool ApplyScatter(GameWorld world, ScatterCommand command, List<SimEvent>? events)
    {
        var units = OwnUnits(world, command);
        foreach (var unit in units)
        {
            var cell = unit.Position.ToCell();
            var free = new List<CellPos>();
            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (AStar.CanStep(world.Map, cell, dx, dy, unit.Archetype.Locomotor) && world.Reservations.HasRoomFor(next, unit))
                {
                    free.Add(next);
                }
            }

            if (free.Count == 0)
            {
                continue;
            }

            var target = free[world.Random.NextInt(free.Count)].Center;
            UnitOrders.GiveRoute(world, unit, [new UnitOrder(target, AttackMove: false)], loop: false, append: false);
            events?.Add(new UnitMoveStarted(world.CurrentTick, unit.Id, target));
        }

        return units.Count > 0;
    }

    /// <summary>The command's living units of its player, each once, in id order.</summary>
    private static List<Unit> OwnUnits(GameWorld world, UnitsCommand command)
    {
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

        units.Sort((a, b) => a.Id.CompareTo(b.Id));
        return units;
    }
}
