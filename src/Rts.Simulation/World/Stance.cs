namespace Rts.Simulation.World;

/// <summary>
/// How a unit fights by itself (docs/implementation/M4_combat.md, 4.11). The values are part of the command format: never renumber.
/// </summary>
public enum Stance : byte
{
    /// <summary>Looks for targets within its sight and follows them while it sees them.</summary>
    Aggressive = 0,

    /// <summary>Looks within its sight, follows up to <see cref="Systems.PursuitSystem.GuardLeash"/> cells from its anchor and comes back.</summary>
    Guard = 1,

    /// <summary>Looks only within the range of its weapons and never moves to fight.</summary>
    HoldPosition = 2,
}
