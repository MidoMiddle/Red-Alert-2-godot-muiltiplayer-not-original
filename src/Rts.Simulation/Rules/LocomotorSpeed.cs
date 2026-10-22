using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>
/// The speed of one locomotor on one land type, written <c>foot: 90%</c> in the data (docs/implementation/M7_maps.md, 4.3).
/// <see cref="Multiplier"/> is the fraction of the speed: 0.9 for 90 percent; zero means the locomotor cannot enter the land.
/// </summary>
public readonly record struct LocomotorSpeed(string Locomotor, Fixed Multiplier);
