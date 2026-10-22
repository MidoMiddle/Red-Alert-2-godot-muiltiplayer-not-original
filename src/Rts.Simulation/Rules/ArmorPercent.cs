using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>A share of damage against one type of armor, as a fraction: 0.25 for 25 percent (M4, 4.2).</summary>
public readonly record struct ArmorPercent(string Armor, Fixed Percent);
