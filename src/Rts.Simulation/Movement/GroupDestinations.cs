using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Movement;

/// <summary>
/// A place of its own near the target for every unit of a group order (docs/implementation/M2_unit_control.md, 4.6 and 4.8):
/// a cell for a vehicle, a spot (subcell) for a soldier, up to five soldiers per cell and never a vehicle with them. So the
/// group arrives as a blob instead of fighting over one cell, and keeps its shape: who was on the left stands on the left.
/// <list type="number">
/// <item>Anchor: the clicked cell, or the nearest cell the group can actually reach (its connected region) when the click is on
/// water, rock or an island.</item>
/// <item>Candidates: cells reached by a breadth-first search from the anchor over enterable cells (no corner cutting), so a
/// cell across a river is never offered; cells where units outside the group stand still are skipped.</item>
/// <item>Shape: each unit's offset from the group centre, squeezed toward the centre when the group is wider than ⌈√cells⌉,
/// where cells = vehicles + ⌈soldiers / 5⌉, so a long column arrives as a compact blob; soldiers' offsets are halved more,
/// because they stand closer together.</item>
/// <item>Assignment: units by distance to the target, then id; a vehicle takes the free cell nearest to anchor + offset, a
/// soldier the free spot nearest to that point; ties go to the earlier candidate and the lower spot.</item>
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

        // One unit goes where it was sent; so does a group sent to a high bridge's deck, which the places on the ground do not know
        // (Decks): each finds a free cell near the target when it gets there.
        if (units.Count == 1 || world.Map.IsDeck(target.ToCell()))
        {
            foreach (var unit in units)
            {
                result.Add((unit, target));
            }

            result.Sort((a, b) => a.Unit.Id.CompareTo(b.Unit.Id));
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
        var steps = new GroupSteps(world.PathRequests.Pathfinder, locomotors);
        var region = Region(map, units[0].Position.ToCell(), steps);
        if (FindAnchor(map, targetCell, region) is not { } anchor)
        {
            foreach (var unit in units)
            {
                result.Add((unit, target));
            }

            return result;
        }

        // A vehicle needs a cell; five soldiers share one.
        var vehicles = 0;
        foreach (var unit in units)
        {
            vehicles += unit.Archetype.Placement == UnitPlacement.Cell ? 1 : 0;
        }

        var neededCells = vehicles + (units.Count - vehicles + Reservations.SubcellsPerCell - 1) / Reservations.SubcellsPerCell;
        var candidates = Candidates(world, anchor, steps, members, neededCells);
        var radius = (int)IntMath.Sqrt(neededCells);
        radius += radius * radius < neededCells ? 1 : 0;

        // Group centre and the widest offset, in cells.
        long sumX = 0, sumY = 0;
        foreach (var unit in units)
        {
            var cell = unit.Position.ToCell();
            sumX += cell.X;
            sumY += cell.Y;
        }

        var center = new CellPos((int)IntMath.DivRound(sumX, units.Count), (int)IntMath.DivRound(sumY, units.Count));
        var widest = 0;
        foreach (var unit in units)
        {
            var cell = unit.Position.ToCell();
            widest = IntMath.Max(widest, IntMath.Max(IntMath.Abs(cell.X - center.X), IntMath.Abs(cell.Y - center.Y)));
        }

        var ordered = new List<Unit>(units);
        ordered.Sort((a, b) =>
        {
            var byDistance = (a.Position - target).LengthSquared.CompareTo((b.Position - target).LengthSquared);
            return byDistance != 0 ? byDistance : a.Id.CompareTo(b.Id);
        });

        var places = new GroupPlaces(world, candidates, members);
        foreach (var unit in ordered)
        {
            var destination = unit.Archetype.Placement == UnitPlacement.Cell
                ? places.TakeCell(unit, Squeeze(unit.Position.ToCell().X - center.X, widest, radius) + anchor.X,
                    Squeeze(unit.Position.ToCell().Y - center.Y, widest, radius) + anchor.Y)
                : places.TakeSpot(unit, anchor.Center + new WorldPos(
                    (int)(Squeeze(unit.Position.X - center.Center.X, widest, radius) / 2),
                    (int)(Squeeze(unit.Position.Y - center.Center.Y, widest, radius) / 2)));

            // The vehicle given the clicked cell drives to the clicked point itself.
            result.Add((unit, destination is { } place ? (place == targetCell.Center ? target : place) : target));
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
    private static bool[] Region(MapGrid map, CellPos start, GroupSteps steps)
    {
        var region = new bool[map.Width * map.Height];
        var queue = new int[map.Width * map.Height];
        var tail = 0;
        region[start.Y * map.Width + start.X] = true;
        queue[tail++] = start.Y * map.Width + start.X;
        for (var head = 0; head < tail; head++)
        {
            var x = queue[head] % map.Width;
            var y = queue[head] / map.Width;
            foreach (var (dx, dy) in AStar.Directions)
            {
                if (!steps.CanStep(x, y, dx, dy))
                {
                    continue;
                }

                var index = (y + dy) * map.Width + x + dx;
                if (!region[index])
                {
                    region[index] = true;
                    queue[tail++] = index;
                }
            }
        }

        return region;
    }

    /// <summary>
    /// <see cref="AStar.CanStep"/> for any locomotor of the group, read from the step-cost layers of the world's pathfinder
    /// (M2 step 15: the region search walks the whole map, the slow per-cell lookups made a 300-unit order take milliseconds).
    /// </summary>
    private sealed class GroupSteps
    {
        private readonly int[][] _layers;
        private readonly int _width;
        private readonly int _height;

        public GroupSteps(Pathfinder pathfinder, List<Locomotor> locomotors)
        {
            _width = pathfinder.Map.Width;
            _height = pathfinder.Map.Height;
            _layers = new int[locomotors.Count][];
            for (var i = 0; i < locomotors.Count; i++)
            {
                _layers[i] = pathfinder.CostsFor(locomotors[i]).Straight;
            }
        }

        public bool CanStep(int x, int y, int dx, int dy)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= _width || ny >= _height)
            {
                return false;
            }

            foreach (var layer in _layers)
            {
                if (layer[ny * _width + nx] > 0 && (dx == 0 || dy == 0 || (layer[y * _width + nx] > 0 && layer[ny * _width + x] > 0)))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Cells reachable from the anchor in BFS order, enough for the group plus one more ring, skipping cells of standing outsiders.</summary>
    private static List<CellPos> Candidates(GameWorld world, CellPos anchor, GroupSteps steps, HashSet<EntityId> members, int needed)
    {
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
                if (steps.CanStep(cell.X, cell.Y, dx, dy) && seen.Add(next))
                {
                    queue.Add((next, depth + 1));
                }
            }
        }

        return candidates;
    }

    /// <summary>An offset squeezed toward the centre when the group is wider than <paramref name="radius"/> cells.</summary>
    private static long Squeeze(long offset, int widest, int radius) =>
        widest > radius ? IntMath.DivRound(offset * radius, widest) : offset;

    /// <summary>True when somebody outside the group stands still here: the cell is not offered to the group.</summary>
    private static bool IsFreeFor(GameWorld world, CellPos cell, HashSet<EntityId> members) =>
        !IsStandingOutsider(world, world.Reservations.VehicleAt(cell), members)
        && CountStandingOutsiders(world, cell, members) < Reservations.SubcellsPerCell;

    /// <summary>A unit that is not part of the group and does not drive away: its place stays its own.</summary>
    internal static bool IsStandingOutsider(GameWorld world, EntityId id, HashSet<EntityId> members) =>
        id != EntityId.None && !members.Contains(id) && world.TryGet(id, out var entity) && entity is Unit { MoveTarget: null };

    private static int CountStandingOutsiders(GameWorld world, CellPos cell, HashSet<EntityId> members)
    {
        var count = 0;
        for (var spot = 0; spot < Reservations.SubcellsPerCell; spot++)
        {
            count += IsStandingOutsider(world, world.Reservations.InfantryAt(cell, spot), members) ? 1 : 0;
        }

        return count;
    }

    /// <summary>
    /// Places handed out so far: a candidate cell goes either to one vehicle of the group or to up to five of its soldiers,
    /// never to both, and never over a unit outside the group that stands still.
    /// </summary>
    private sealed class GroupPlaces
    {
        private readonly GameWorld _world;
        private readonly List<CellPos> _candidates;
        private readonly bool[] _vehicle;
        private readonly bool[] _spots;
        private readonly int[] _soldiers;

        // Who of the outsiders stands where does not change while places are handed out: looked up once per candidate.
        private readonly bool[] _outsiderVehicle;
        private readonly bool[] _outsiderSpot;
        private readonly int[] _outsiderSoldiers;

        public GroupPlaces(GameWorld world, List<CellPos> candidates, HashSet<EntityId> members)
        {
            _world = world;
            _candidates = candidates;
            _vehicle = new bool[candidates.Count];
            _spots = new bool[candidates.Count * Reservations.SubcellsPerCell];
            _soldiers = new int[candidates.Count];
            _outsiderVehicle = new bool[candidates.Count];
            _outsiderSpot = new bool[candidates.Count * Reservations.SubcellsPerCell];
            _outsiderSoldiers = new int[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                _outsiderVehicle[i] = IsStandingOutsider(world, world.Reservations.VehicleAt(candidates[i]), members);
                for (var spot = 0; spot < Reservations.SubcellsPerCell; spot++)
                {
                    var outsider = IsStandingOutsider(world, world.Reservations.InfantryAt(candidates[i], spot), members);
                    _outsiderSpot[i * Reservations.SubcellsPerCell + spot] = outsider;
                    _outsiderSoldiers[i] += outsider ? 1 : 0;
                }
            }
        }

        /// <summary>The free cell nearest to (<paramref name="wantedX"/>, <paramref name="wantedY"/>) for a vehicle, as its centre.</summary>
        public WorldPos? TakeCell(Unit unit, long wantedX, long wantedY)
        {
            var best = -1;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var cell = _candidates[i];
                if (_vehicle[i] || _soldiers[i] > 0 || !_world.Map.CanEnter(cell, unit.Archetype.Locomotor)
                    || _outsiderVehicle[i] || _outsiderSoldiers[i] > 0)
                {
                    continue;
                }

                var dx = cell.X - wantedX;
                var dy = cell.Y - wantedY;
                if (dx * dx + dy * dy < bestDistance)
                {
                    best = i;
                    bestDistance = dx * dx + dy * dy;
                }
            }

            if (best < 0)
            {
                return null;
            }

            _vehicle[best] = true;
            return _candidates[best].Center;
        }

        /// <summary>The free soldier spot nearest to <paramref name="wanted"/>, as its point.</summary>
        public WorldPos? TakeSpot(Unit unit, WorldPos wanted)
        {
            var best = -1;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var cell = _candidates[i];
                if (_vehicle[i] || !_world.Map.CanEnter(cell, unit.Archetype.Locomotor) || _outsiderVehicle[i])
                {
                    continue;
                }

                for (var spot = 0; spot < Reservations.SubcellsPerCell; spot++)
                {
                    var index = i * Reservations.SubcellsPerCell + spot;
                    if (_spots[index] || _outsiderSpot[index])
                    {
                        continue;
                    }

                    var distance = (Reservations.SubcellPoint(cell, spot) - wanted).LengthSquared;
                    if (distance < bestDistance)
                    {
                        best = index;
                        bestDistance = distance;
                    }
                }
            }

            if (best < 0)
            {
                return null;
            }

            _spots[best] = true;
            _soldiers[best / Reservations.SubcellsPerCell]++;
            return Reservations.SubcellPoint(_candidates[best / Reservations.SubcellsPerCell], best % Reservations.SubcellsPerCell);
        }
    }
}
