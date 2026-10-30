using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The bridge repair huts (loose ends, stage 2; RA2 bridge repair hut): a hut belongs to the bridge nearest it within
/// <see cref="HutReach"/> cells, the first of the map on a tie. An engineer going in (<see cref="EntrySystem"/>) mends every segment of
/// it whole; a fallen segment comes back unless a unit stands on its water — a ship — and stays down then. A hut whose bridge is whole
/// takes no engineer.
/// </summary>
public static class BridgeRepair
{
    /// <summary>How far from its bridge a hut may stand, in cells.</summary>
    public const int HutReach = 4;

    /// <summary>The bridge of a hut; null when none is near.</summary>
    public static Bridge? BridgeOf(MapGrid map, Building hut) => BridgeOf(map, hut.Cell);

    /// <summary>The bridge a hut on <paramref name="hut"/> would belong to; null when none is near (the map checks, a hut by each end of a bridge).</summary>
    public static Bridge? BridgeOf(MapGrid map, CellPos hut)
    {
        Bridge? best = null;
        var bestDistance = HutReach + 1;
        foreach (var bridge in map.Bridges)
        {
            foreach (var segment in bridge.Segments)
            {
                foreach (var cell in segment.Cells)
                {
                    var distance = IntMath.Max(IntMath.Abs(cell.X - hut.X), IntMath.Abs(cell.Y - hut.Y));
                    if (distance < bestDistance)
                    {
                        (best, bestDistance) = (bridge, distance);
                    }
                }
            }
        }

        return best;
    }

    /// <summary>Whether the bridge of the hut has a segment to mend.</summary>
    public static bool Needed(GameWorld world, Building hut)
    {
        if (BridgeOf(world.Map, hut) is not { } bridge)
        {
            return false;
        }

        foreach (var segment in bridge.Segments)
        {
            if (segment.Hp < bridge.Type.SegmentHp && (segment.IsIntact || !Taken(world, segment)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Mends the bridge of the hut.</summary>
    internal static void Mend(GameWorld world, Building hut, List<SimEvent>? events)
    {
        if (BridgeOf(world.Map, hut) is not { } bridge)
        {
            return;
        }

        var edits = new List<MapEdit>();
        foreach (var segment in bridge.Segments)
        {
            if (segment.IsIntact)
            {
                world.Map.SetSegmentHp(segment, bridge.Type.SegmentHp);
            }
            else if (!Taken(world, segment))
            {
                edits.Add(new RepairBridgeSegmentEdit(bridge.Id, segment.Index));
            }
        }

        if (edits.Count > 0)
        {
            world.ChangeMap(edits, events);
        }

        events?.Add(new BridgeRepaired(world.CurrentTick, bridge.Id));
    }

    // A unit on the water of a fallen segment: a ship, or one that floats; the segment waits for it to go. Under a high bridge
    // nobody is in the way: its deck goes up over them.
    private static bool Taken(GameWorld world, BridgeSegment segment)
    {
        if (segment.Bridge.Type.High)
        {
            return false;
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true } unit && !unit.Archetype.Flies && segment.Cells.Contains(unit.Position.ToCell()))
            {
                return true;
            }
        }

        return false;
    }
}
