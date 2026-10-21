using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Pathfinding;

public enum PathStatus : byte
{
    /// <summary>The path ends on the goal.</summary>
    Found = 1,

    /// <summary>The goal cannot be reached; the path ends on the reachable cell closest to it.</summary>
    Unreachable = 2,

    /// <summary>The search hit its node limit; the path ends on the best cell found so far.</summary>
    LimitReached = 3,
}

/// <summary>
/// Result of a path search: cells from the start to the end, both included, the cost of the whole path in
/// <see cref="AStar"/> units, and how many nodes the search expanded. <see cref="Waypoints"/> are the cells a unit
/// actually drives to after <see cref="PathSmoother"/>; before smoothing they are all the cells.
/// </summary>
public sealed record CellPath(PathStatus Status, ImmutableArray<CellPos> Cells, int Cost, int Expanded)
{
    private readonly ImmutableArray<CellPos> _waypoints;

    // The digest with what it was computed from: `with` copies this field, so it is checked against the current values.
    private (ulong Value, PathStatus Status, int Cost, int Expanded, ImmutableArray<CellPos> Cells, ImmutableArray<CellPos> Waypoints)? _digest;

    public CellPos End => Cells[^1];

    /// <summary>Corners of the smoothed path: a subset of <see cref="Cells"/> in order, with the same first and last cell.</summary>
    public ImmutableArray<CellPos> Waypoints
    {
        get => _waypoints.IsDefault ? Cells : _waypoints;
        init => _waypoints = value;
    }

    /// <summary>
    /// FNV-1a of the status, cost, expanded nodes, cells and waypoints, for the state hash. A path never changes, so it is
    /// computed once instead of on every tick (docs/implementation/M2_unit_control.md, 4.15).
    /// </summary>
    internal ulong Digest
    {
        get
        {
            if (_digest is { } cached && cached.Status == Status && cached.Cost == Cost && cached.Expanded == Expanded
                && cached.Cells == Cells && cached.Waypoints == Waypoints)
            {
                return cached.Value;
            }

            var hash = new Fnv1a64();
            hash.Add((byte)Status);
            hash.Add(Cost);
            hash.Add(Expanded);
            hash.Add(Cells.Length);
            foreach (var cell in Cells)
            {
                hash.Add(cell.X);
                hash.Add(cell.Y);
            }

            hash.Add(Waypoints.Length);
            foreach (var waypoint in Waypoints)
            {
                hash.Add(waypoint.X);
                hash.Add(waypoint.Y);
            }

            _digest = (hash.Value, Status, Cost, Expanded, Cells, Waypoints);
            return hash.Value;
        }
    }
}
