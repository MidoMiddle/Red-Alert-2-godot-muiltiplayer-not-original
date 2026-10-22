namespace Rts.Simulation.Rules;

/// <summary>Size of a footprint in cells, written <c>[2, 2]</c> in the data files (docs/implementation/M3_base_economy.md, 4.2).</summary>
public readonly record struct CellSize(int Width, int Height);
