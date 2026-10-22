namespace Rts.Simulation;

/// <summary>
/// Version of the simulation rules. Must be bumped by any change that alters simulation results,
/// so replays and network peers can detect incompatible rules.
/// </summary>
public static class SimulationVersion
{
    public const uint Current = 29;
}
