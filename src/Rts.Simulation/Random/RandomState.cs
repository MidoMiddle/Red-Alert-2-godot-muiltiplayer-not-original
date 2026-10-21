namespace Rts.Simulation.Random;

/// <summary>
/// Full internal state of <see cref="DeterministicRandom"/>: enough to hash it and to resume it exactly.
/// </summary>
public readonly record struct RandomState(ulong State, ulong Increment);
