using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>How a projectile gets to its target (docs/implementation/M4_combat.md, 4.8). Values go into the data hash: never renumber.</summary>
public enum ProjectileMotion : byte
{
    /// <summary>Hits at once: bullets, lightning.</summary>
    Instant = 1,

    /// <summary>Flies along an arc to the point it was aimed at: shells.</summary>
    Ballistic = 2,

    /// <summary>Flies after its target, turning: rockets.</summary>
    Homing = 3,
}

/// <summary>
/// A projectile (docs/implementation/M4_combat.md, 4.1, 4.3): how a shot reaches what it was fired at. The weapon says how much and how
/// often, the warhead what it does there.
/// </summary>
public sealed record ProjectileDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ProjectileMotion Motion { get; init; }

    /// <summary>Leptons per tick; a projectile that flies needs it.</summary>
    public int Speed { get; init; }

    /// <summary>Height of the arc of a ballistic projectile, in cells.</summary>
    public Fixed Arc { get; init; }

    /// <summary>How far from the aim point it may land, in cells.</summary>
    public Fixed Inaccuracy { get; init; }

    /// <summary><c>BAngle</c> units per tick a homing projectile turns.</summary>
    public int TurnRate { get; init; }

    /// <summary>Ticks a homing projectile flies before it falls.</summary>
    public int Lifetime { get; init; }

    /// <summary>What the client draws, such as <c>electric_bolt</c>; the simulation does not use it.</summary>
    public string Visual { get; init; } = "";
}
