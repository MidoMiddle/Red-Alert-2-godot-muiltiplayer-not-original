namespace Rts.Simulation.World;

/// <summary>What a unit occupies: a whole cell, or one of the five infantry spots in it (M2 step 10).</summary>
public enum UnitPlacement : byte
{
    Cell = 1,
    Subcell = 2,
}
