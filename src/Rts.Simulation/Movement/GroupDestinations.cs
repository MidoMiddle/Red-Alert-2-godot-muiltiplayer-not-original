using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Movement;

/// <summary>
/// A cell of its own near the target for every unit of a group order (docs/implementation/M2_unit_control.md, 4.6), so the
/// group arrives as a blob instead of fighting over one cell, and keeps its shape: who was on the left stands on the left.
/// <list type="number">
/// <item>Anchor: the clicked cell, or the nearest cell the group can actually reach (its connected region) when the click is on
/// water, rock or an island.</item>
/// <item>Candidates: cells reached by a breadth-first search from the anchor over enterable cells (no corner cutting), so a
/// cell across a river is never offered; cells where units outside the group stand still are skipped.</item>
/// <item>Shape: each unit's offset from the group centre in cells, squeezed toward the centre when the group is wider than
/// ⌈√n⌉ cells, so a long column arrives as a compact blob.</item>
/// <item>Assignment: units by distance to the target, then id; each takes the free candidate its locomotor can enter that is
/// nearest to anchor + offset, ties going to the earlier candidate.</item>
/// </list>
/// Integer math and fixed orders only. A single unit drives to the clicked point itself, and so does the unit given the clicked cell.
/// </summary>
internal static class GroupDestinations
{
    /// <summary>How far from the anchor destinations may be, in BFS steps.</summary>
    public const int MaxRadius = 12;

    /// <summary>Destination of every unit, in <see cref="EntityId"/> order.</summary>
    public static List<(Unit Unit, WorldPos Destination)> Assign(GameWorld world, IReadOnlyList<Unit> units, WorldPos target)
    {
        var result = new List<(Unit Unit, WorldPos Destination)>();
        if (units.Count == 0)
        {
            return result;
        }

        if (units.Count == 1)
        {
            result.Add((units[0], target));
            return result;
        }

        var map = world.Map;
        var locomotors = new List<Locomotor>();
        var members = new HashSet<EntityId>();
        foreach (var unit in units)
        {
            members.Add(unit.Id);
            if (!locomotors.Contains(unit.Archetype.Locomotor))
            {
                locomotors.Add(unit.Archetype.Locomotor);
            }
        }

        var targetCell = target.ToCell();
        var region = Region(map, units[0].Position.ToCell(), locomotors);
        if (FindAnchor(map, targetCell, region) is not { } anchor)
        {
            foreach (var unit in units)
            {
                result.Add((unit, target));
            }

            return result;
        }

        var candidates = Candidates(world, anchor, locomotors, members, units.Count);
        var radius = (int)IntMath.Sqrt(units.Count);
        radius += radius * radius < units.Count ? 1 : 0;

        // Group centre and the widest offset, in cells.
        long sumX = 0, sumY = 0;
        foreach (var unit in units)
        {
            var cell = unit.Position.ToCell();
            sumX += cell.X;
            sumY += cell.Y;
        }

        var centerX = (int)IntMath.DivRound(sumX, units.Count);
        var centerY = (int)IntMath.DivRound(sumY, units.Count);
        var widest = 0;
        foreach (var unit in units)
        {
            var cell = unit.Position.ToCell();
            widest = IntMath.Max(widest, IntMath.Max(IntMath.Abs(cell.X - centerX), IntMath.Abs(cell.Y - centerY)));
        }

        var ordered = new List<Unit>(units);
        ordered.Sort((a, b) =>
        {
            var byDistance = (a.Position - target).LengthSquared.CompareTo((b.Position - target).LengthSquared);
            return byDistance != 0 ? byDistance : a.Id.CompareTo(b.Id);
        });

        var taken = new bool[candidates.Count];
        foreach (var unit in ordered)
        {
            var cell = unit.Position.ToCell();
            long offsetX = cell.X - centerX;
            long offsetY = cell.Y - centerY;
            if (widest > radius)
            {
                offsetX = IntMath.DivRound(offsetX * radius, widest);
                offsetY = IntMath.DivRound(offsetY * radius, widest);
            }

            var wantedX = anchor.X + offsetX;
            var wantedY = anchor.Y + offsetY;
            var best = -1;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (taken[i] || !map.CanEnter(candidates[i], unit.Archetype.Locomotor))
                {
                    continue;
                }

                var dx = candidates[i].X - wantedX;
                var dy = candidates[i].Y - wantedY;
                var distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            if (best < 0)
            {
                result.Add((unit, target));
                continue;
            }

            taken[best] = true;
            result.Add((unit, candidates[best] == targetCell ? target : candidates[best].Center));
        }

        result.Sort((a, b) => a.Unit.Id.CompareTo(b.Unit.Id));
        return result;
    }

    /// <summary>The clicked cell, or the nearest cell of the group's region.</summary>
    private static CellPos? FindAnchor(MapGrid map, CellPos target, bool[] region)
    {
        var seen = new HashSet<CellPos> { target };
        var queue = new List<(CellPos Cell, int Depth)> { (target, 0) };
        for (var head = 0; head < queue.Count; head++)
        {
            var (cell, depth) = queue[head];
            if (region[cell.Y * map.Width + cell.X])
            {
                return cell;
            }

            if (depth == MaxRadius)
            {
                continue;
            }

            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (map.Contains(next) && seen.Add(next))
                {
                    queue.Add((next, depth + 1));
                }
            }
        }

        return null;
    }

    /// <summary>Every cell reachable from <paramref name="start"/> by the group (8 neighbours, no corner cutting, units ignored), as a flag per cell index.</summary>
    private static bool[] Region(MapGrid map, CellPos start, List<Locomotor> locomotors)
    {
        var region = new bool[map.Width * map.Height];
        var queue = new int[map.Width * map.Height];
        var tail = 0;
        region[start.Y * map.Width + start.X] = true;
        queue[tail++] = start.Y * map.Width + start.X;
        for (var head = 0; head < tail; head++)
        {
            var cell = new CellPos(queue[head] % map.Width, queue[head] / map.Width);
            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (!CanStepAny(map, cell, dx, dy, locomotors))
                {
                    continue;
                }

                var index = next.Y * map.Width + next.X;
                if (!region[index])
                {
                    region[index] = true;
                    queue[tail++] = index;
                }
            }
        }

        return region;
    }

    private static bool CanStepAny(MapGrid map, CellPos from, int dx, int dy, List<Locomotor> locomotors)
    {
        foreach (var locomotor in locomotors)
        {
            if (AStar.CanStep(map, from, dx, dy, locomotor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Cells reachable from the anchor in BFS order, enough for the group plus one more ring, skipping cells of standing outsiders.</summary>
    private static List<CellPos> Candidates(GameWorld world, CellPos anchor, List<Locomotor> locomotors, HashSet<EntityId> members, int needed)
    {
        var map = world.Map;
        var candidates = new List<CellPos>();
        var seen = new HashSet<CellPos> { anchor };
        var queue = new List<(CellPos Cell, int Depth)> { (anchor, 0) };
        var stopDepth = MaxRadius;
        for (var head = 0; head < queue.Count; head++)
        {
            var (cell, depth) = queue[head];
            if (depth > stopDepth)
            {
                break;
            }

            if (IsFreeFor(world, cell, members))
            {
                candidates.Add(cell);
                if (candidates.Count >= needed && stopDepth == MaxRadius)
                {
                    // Finish this ring and take one more, so that every unit has room to keep its place in the shape.
                    stopDepth = IntMath.Min(MaxRadius, depth + 1);
                }
            }

            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (CanStepAny(map, cell, dx, dy, locomotors) && seen.Add(next))
                {
                    queue.Add((next, depth + 1));
                }
            }
        }

        return candidates;
    }

    /// <summary>Free, or held by a group member, or by a unit that is driving away.</summary>
    private static bool IsFreeFor(GameWorld world, CellPos cell, HashSet<EntityId> members)
    {
        var owner = world.Reservations.OwnerOf(cell);
        return owner == EntityId.None
            || members.Contains(owner)
            || (world.TryGet(owner, out var entity) && entity is Unit { MoveTarget: not null });
    }
}
