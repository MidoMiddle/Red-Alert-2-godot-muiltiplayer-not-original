using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>
/// What a hit does (docs/implementation/M4_combat.md, 4.1, 4.3): how strongly against each armor, and over how wide an area. Many weapons
/// share a warhead, so the balance is set in one place.
/// </summary>
public sealed record WarheadDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The share of damage against every armor type (RA2 <c>Verses</c>), sorted by armor id; the data lists them all.</summary>
    public ImmutableArray<ArmorPercent> Verses { get; init; } = ImmutableArray<ArmorPercent>.Empty;

    /// <summary>Radius of the area in cells; zero hits the target only.</summary>
    public Fixed Spread { get; init; }

    /// <summary>The share of damage at the edge of the area (RA2 <c>PercentAtMax</c>).</summary>
    public Fixed PercentAtMax { get; init; } = Fixed.One;

    /// <summary>Whether it damages walls.</summary>
    public bool WallDamage { get; init; }

    /// <summary>Whether it hits aircraft in its area.</summary>
    public bool AffectsAir { get; init; }

    /// <summary>
    /// A beam that hurts nothing but erases its target from time (stage 3a: the time trooper, RA2 <c>Temporal</c>): its damage is the
    /// erasure a tick while the shooter holds the beam on it (<see cref="Systems.ErasureSystem"/>).
    /// </summary>
    public bool Temporal { get; init; }

    /// <summary>How infantry dies of it, for the client: an id such as <c>explode</c>; the simulation does not use it.</summary>
    public string InfantryDeath { get; init; } = "";

    // The verses compare by value, not as the same array.
    public bool Equals(WarheadDef? other) =>
        other is not null && Id == other.Id && Name == other.Name && Verses.SequenceEqual(other.Verses) && Spread == other.Spread
        && PercentAtMax == other.PercentAtMax && WallDamage == other.WallDamage && AffectsAir == other.AffectsAir
        && Temporal == other.Temporal && InfantryDeath == other.InfantryDeath;

    public override int GetHashCode() => unchecked((Id.Length * 31) + Verses.Length);
}
