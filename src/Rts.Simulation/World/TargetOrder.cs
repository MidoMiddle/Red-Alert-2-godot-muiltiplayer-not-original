namespace Rts.Simulation.World;

/// <summary>Whose choice a unit's target is (docs/implementation/M4_combat.md, 4.12).</summary>
public enum TargetOrder : byte
{
    /// <summary>The unit chose it itself, or has none.</summary>
    None = 0,

    /// <summary>The player ordered it to attack this enemy: it keeps to it and follows it until it falls.</summary>
    Attack = 1,

    /// <summary>The player forced the attack: on anything, or on a point of the ground (<see cref="Unit.TargetPoint"/>).</summary>
    Force = 2,
}
