using Rts.Simulation.World;

namespace Rts.Client.Debug;

/// <summary>Debug panel line of the selected unit's movement state (M2 step 8). Pure text, tested in Rts.Simulation.Tests.</summary>
public static class UnitStateText
{
    public static string Describe(Unit? unit)
    {
        if (unit is null)
        {
            return "unit -";
        }

        if (unit.PathPending)
        {
            return "unit waiting for path";
        }

        if (unit.ResumeTarget is not null)
        {
            return unit.HoldTicks > 0 ? $"unit giving way · hold {unit.HoldTicks}" : "unit giving way";
        }

        return unit.MoveTarget is null ? "unit idle" : $"unit moving · wait {unit.WaitTicks} · replans {unit.Replans}";
    }
}
