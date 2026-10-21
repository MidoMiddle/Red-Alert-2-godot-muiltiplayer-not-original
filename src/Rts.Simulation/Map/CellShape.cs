namespace Rts.Simulation.Map;

/// <summary>The shape of a cell from the heights of its four corners (docs/implementation/M7_maps.md, 4.1).</summary>
public enum CellShape : byte
{
    /// <summary>All corners on one level.</summary>
    Flat = 1,

    /// <summary>The corners differ by one level: a ramp, passable (step 3 slows units on it).</summary>
    Slope = 2,

    /// <summary>The corners differ by two levels or more: impassable for ground units (step 3).</summary>
    Cliff = 3,
}
