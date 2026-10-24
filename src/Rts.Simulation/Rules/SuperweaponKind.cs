namespace Rts.Simulation.Rules;

/// <summary>What a superweapon does when it fires (stage 2d of filling the game). Values are hashed with the data.</summary>
public enum SuperweaponKind : byte
{
    None = 0,

    /// <summary>The Bastion Field Projector (RA2 Iron Curtain): what stands in 3 x 3 cells cannot be hurt for a while; soldiers die.</summary>
    Curtain = 1,

    /// <summary>The Strategic Missile Silo (RA2 nuclear missile): a missile that falls on its target.</summary>
    Missile = 2,

    /// <summary>The Phase Gate (RA2 Chronosphere): what stands in 3 x 3 cells moves to another place; soldiers die.</summary>
    Gate = 3,

    /// <summary>The Storm Generator (RA2 Weather Control): a storm of lightning over an area after a delay.</summary>
    Storm = 4,

    /// <summary>A paradrop (RA2 ParaDrop): a plane flies over the target and drops soldiers; shot down, it drops none.</summary>
    Paradrop = 5,
}
