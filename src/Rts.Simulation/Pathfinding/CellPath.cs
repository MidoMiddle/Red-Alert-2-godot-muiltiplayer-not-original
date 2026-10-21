using System.Collections.Immutable;
using Rts.Simulation.Geometry;

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

    public CellPos End => Cells[^1];

    /// <summary>Corners of the smoothed path: a subset of <see cref="Cells"/> in order, with the same first and last cell.</summary>
    public ImmutableArray<CellPos> Waypoints
    {
        get => _waypoints.IsDefault ? Cells : _waypoints;
        init => _waypoints = value;
    }
}
