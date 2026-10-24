using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The paradrop (loose ends, stage 1; RA2 ParaDrop): a plane comes in from the edge of the map nearest the building that sent it, flies
/// straight over the target and drops its soldiers there, each on a free cell nearest the target it can stand on — one that finds none
/// is lost — then flies on to the far edge and leaves the map. Its player cannot order it; anti-air brings it down, and the soldiers
/// with it.
/// </summary>
public static class ParadropSystem
{
    /// <summary>Sends a plane of <paramref name="owner"/> from the edge nearest <paramref name="from"/> to drop <paramref name="count"/> of <paramref name="type"/> on <paramref name="target"/>.</summary>
    internal static Unit Send(GameWorld world, PlayerId owner, CellPos from, CellPos target, UnitType type, int count)
    {
        var (entry, exit) = Edges(world.Map.Width, world.Map.Height, from, target);
        var plane = world.SpawnUnit(owner, UnitType.ParadropPlane, entry.Center);
        plane.Drop = new Paradrop(type, count, target.Center, exit.Center);
        UnitOrders.RequestPath(world, plane, target.Center, default);
        return plane;
    }

    /// <summary>After the movement: a plane over its target drops its load and turns to the exit; one at the exit leaves.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        List<Unit>? planes = null;
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Drop: not null, MoveTarget: null } plane)
            {
                (planes ??= []).Add(plane);
            }
        }

        // The soldiers join the list of entities after its walk.
        foreach (var plane in planes ?? [])
        {
            var drop = plane.Drop!;
            if (drop.Count == 0)
            {
                plane.MarkDead();
                continue;
            }

            var archetype = world.Rules.Unit(drop.Type);
            var centre = drop.Point.ToCell();
            for (var i = 0; i < drop.Count; i++)
            {
                var cell = world.Map.Contains(centre) && world.Map.CanEnter(centre, archetype.Locomotor)
                    && world.Reservations.HasRoomFor(centre, archetype.Placement, EntityId.None)
                    ? centre
                    : TransportSystem.FreeCellAround(world, centre, archetype);
                if (cell is not { } free)
                {
                    continue;
                }

                var soldier = world.SpawnUnit(plane.Owner, drop.Type, free.Center, snapToSubcell: true);
                events?.Add(new UnitSpawned(world.CurrentTick, soldier.Id, soldier.Owner, soldier.Type, soldier.Position));
            }

            plane.Drop = drop with { Count = 0 };
            UnitOrders.RequestPath(world, plane, drop.Exit, default);
        }
    }

    /// <summary>The cell on the edge nearest <paramref name="from"/> the plane comes in at, and the one on the far edge it leaves at.</summary>
    private static (CellPos Entry, CellPos Exit) Edges(int width, int height, CellPos from, CellPos target)
    {
        var west = from.X;
        var east = width - 1 - from.X;
        var north = from.Y;
        var south = height - 1 - from.Y;
        var nearest = IntMath.Min(IntMath.Min(west, east), IntMath.Min(north, south));
        var y = IntMath.Clamp(target.Y, 0, height - 1);
        var x = IntMath.Clamp(target.X, 0, width - 1);
        return nearest == west ? (new CellPos(0, y), new CellPos(width - 1, y))
            : nearest == east ? (new CellPos(width - 1, y), new CellPos(0, y))
            : nearest == north ? (new CellPos(x, 0), new CellPos(x, height - 1))
            : (new CellPos(x, height - 1), new CellPos(x, 0));
    }
}
