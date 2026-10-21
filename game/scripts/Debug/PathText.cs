using Rts.Simulation.Pathfinding;

namespace Rts.Client.Debug;

/// <summary>Debug panel line of the path preview (F3). Pure text, tested in Rts.Simulation.Tests.</summary>
public static class PathText
{
    public static string Describe(bool enabled, int selectedUnits, CellPath? path)
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
            : $"path {path.Status} · {path.Cells.Length} cells · cost {path.Cost} · {path.Expanded} nodes";
    }
}
