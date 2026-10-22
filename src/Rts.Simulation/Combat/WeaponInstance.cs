namespace Rts.Simulation.Combat;

/// <summary>
/// The state of one weapon of a unit or a defense (docs/implementation/M4_combat.md, 4.7): how long until the next volley and where a
/// volley stands. The weapon itself is in the rules. State: hashed.
/// </summary>
public sealed class WeaponInstance
{
    /// <summary>Ticks until the weapon may start a volley; it counts down with or without a target, so a shot is never stored up.</summary>
    public int ReloadLeft { get; internal set; }

    /// <summary>Shots of the volley still to come.</summary>
    public int BurstLeft { get; internal set; }

    /// <summary>Ticks until the next shot of the volley.</summary>
    public int BurstDelayLeft { get; internal set; }
}
