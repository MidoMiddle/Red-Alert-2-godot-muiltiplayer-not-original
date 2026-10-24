using System;
using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>What a weapon may fire at (RA2 <c>AG</c> and <c>AA</c>). Values go into the data hash: never renumber.</summary>
[Flags]
public enum WeaponTargets : byte
{
    None = 0,
    Ground = 1,
    Air = 2,
}

/// <summary>
/// A weapon (docs/implementation/M4_combat.md, 4.1, 4.3): how much damage and how often, how far, with which projectile and warhead.
/// Units and defenses carry them as <c>primary</c> and <c>secondary</c>.
/// </summary>
public sealed record WeaponDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Damage { get; init; }

    /// <summary>Ticks between two shots (RA2 <c>ROF</c>).</summary>
    public int Reload { get; init; }

    /// <summary>Range in cells; fractions are allowed.</summary>
    public Fixed Range { get; init; }

    /// <summary>The nearest a target may be, in cells.</summary>
    public Fixed MinRange { get; init; }

    /// <summary>Id of the projectile.</summary>
    public string Projectile { get; init; } = "";

    /// <summary>Id of the warhead.</summary>
    public string Warhead { get; init; } = "";

    /// <summary>Shots in one volley.</summary>
    public int Burst { get; init; } = 1;

    /// <summary>Ticks between the shots of a volley.</summary>
    public int BurstDelay { get; init; }

    public WeaponTargets Targets { get; init; } = WeaponTargets.Ground;

    /// <summary>The sound of a shot, for the client; the simulation does not use it.</summary>
    public string Report { get; init; } = "";

    /// <summary>Firing it is its shooter's end (stage 3b, RA2 <c>Suicide</c>: the demolition trooper): the shooter dies at once.</summary>
    public bool Suicide { get; init; }

    /// <summary>
    /// How many splinters a hit of it sends to the nearest other enemies within <see cref="FragmentRange"/> of where it hit (stage 3c: the
    /// beam tank's splitting beam, RA2 <c>ShrapnelCount</c>); 0 for none.
    /// </summary>
    public int Fragments { get; init; }

    /// <summary>The weapon of every splinter.</summary>
    public string FragmentWeapon { get; init; } = "";

    /// <summary>How far the splinters reach, in cells.</summary>
    public Fixed FragmentRange { get; init; }
}
