using Rts.Simulation.Pathfinding;

namespace Rts.Client.Debug;

/// <summary>Debug panel line of the path preview (F3). Pure text, tested in Rts.Simulation.Tests.</summary>
public static class PathText
{
    /// <param name="following">True when <paramref name="path"/> is the unit's own path in the simulation, false for a preview to the pointer.</param>
    public static string Describe(bool enabled, int selectedUnits, CellPath? path, bool following = false)
    {
        if (!enabled)
        {
            return "path off (F3)";
        }

        if (selectedUnits != 1)
        {
            return "path: select one unit";
        }

        return path is null
            ? "path: point at the map"
            : $"{(following ? "unit path" : "path")} {path.Status} · {path.Cells.Length} cells · {path.Waypoints.Length} waypoints · cost {path.Cost} · {path.Expanded} nodes";
    }

    /// <summary><c>paths queue 2 · nodes 4120/6000 this tick</c>.</summary>
    public static string DescribeQueue(int waiting, int expandedLastTick, int budget) =>
        $"paths queue {waiting} · nodes {expandedLastTick}/{budget} this tick";
}
