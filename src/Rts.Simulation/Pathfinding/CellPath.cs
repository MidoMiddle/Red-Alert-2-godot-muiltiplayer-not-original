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
/// <see cref="AStar"/> units, and how many nodes the search expanded.
/// </summary>
public sealed record CellPath(PathStatus Status, ImmutableArray<CellPos> Cells, int Cost, int Expanded)
{
    public CellPos End => Cells[^1];
}
