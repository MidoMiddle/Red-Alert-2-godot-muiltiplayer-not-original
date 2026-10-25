namespace Rts.Simulation.World;

/// <summary>What a unit can do beyond moving and firing (stage 3b of filling the game), from its data.</summary>
[System.Flags]
public enum UnitTalents
{
    None = 0,

    /// <summary>Takes a building of another player, or mends its own (<see cref="Rules.UnitDef.Engineer"/>).</summary>
    Engineer = 1,

    /// <summary>Goes into a building of another player (<see cref="Rules.UnitDef.Infiltrator"/>).</summary>
    Infiltrator = 2,

    /// <summary>Others take it for one of their own (<see cref="Rules.UnitDef.Disguised"/>).</summary>
    Disguised = 4,

    /// <summary>Sees through a disguise (<see cref="Rules.UnitDef.DetectsDisguise"/>).</summary>
    DetectsDisguise = 8,

    /// <summary>Picks no target by itself (<see cref="Rules.UnitDef.Passive"/>).</summary>
    Passive = 16,

    /// <summary>Jumps instead of walking (<see cref="Rules.UnitDef.Teleports"/>).</summary>
    Teleports = 32,

    /// <summary>The toxin on the ground does not hurt it (<see cref="Rules.UnitDef.RadiationImmune"/>).</summary>
    RadiationImmune = 64,

    /// <summary>Standing still, it passes for a tree (<see cref="Rules.UnitDef.DisguiseWhenStill"/>).</summary>
    StillDisguise = 128,

    /// <summary>Fires its passenger's weapon (<see cref="Rules.UnitDef.PassengerWeapon"/>: the IFV).</summary>
    PassengerWeapon = 256,

    // 512 was the robot of Yuri's Revenge, taken out on 2026-10-25.

    /// <summary>Keeps under the water (<see cref="Rules.UnitDef.Submerged"/>).</summary>
    Submerged = 1024,

    /// <summary>Launched by another unit, never built (<see cref="Rules.UnitDef.Spawned"/>).</summary>
    Spawned = 2048,

    /// <summary>Gains no experience (<see cref="Rules.UnitDef.Trainable"/> false; RA2 <c>Trainable=no</c>).</summary>
    Untrainable = 4096,

    /// <summary>No mind control takes it (<see cref="Rules.UnitDef.PsionicImmune"/>).</summary>
    PsionicImmune = 8192,

    /// <summary>Fires its deploy weapon once and stands up again (<see cref="Rules.UnitDef.DeployOnce"/>).</summary>
    DeployOnce = 16384,
}
