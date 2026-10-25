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

    /// <summary>
    /// A bomb it plants on its target instead of hurting it (stage 3b: Ivan's bomb, the commando's C4): the weapon that goes off on the
    /// target after <see cref="BombDelay"/> ticks, or at once when the target falls first; empty for none.
    /// </summary>
    public string Bomb { get; init; } = "";

    /// <summary>The ticks the bomb waits.</summary>
    public int BombDelay { get; init; }

    /// <summary>
    /// It hurts a bridge (<see cref="Systems.BridgeDemolition"/>): its bomb goes on a standing bridge segment too and brings it down
    /// (RA2: the commando's C4), and a shot of it forced on a point of a bridge takes hit points off the segment there by its share
    /// against concrete (RA2: tanks and artillery force-firing at a bridge).
    /// </summary>
    public bool Bridges { get; init; }

    /// <summary>The level of toxin it leaves on the ground of its area (stage 3b: the toxin trooper's eruption, RA2 <c>RadLevel</c>).</summary>
    public int Radiation { get; init; }

    /// <summary>
    /// The shooter climbs into the vehicle it hits and eats it from inside (stage 3d: the combat drone, RA2 <c>Parasite</c>): it does
    /// <see cref="ParasiteDamage"/> every <see cref="ParasiteDelay"/> ticks; anything else it hits takes the weapon's damage.
    /// </summary>
    public bool Parasite { get; init; }

    /// <summary>The damage a parasite does from inside.</summary>
    public int ParasiteDamage { get; init; }

    /// <summary>The ticks between two bites from inside.</summary>
    public int ParasiteDelay { get; init; }

    /// <summary>The parasite takes a ship, not a vehicle (stage 3e: the squid's grab).</summary>
    public bool ParasiteNaval { get; init; }

    /// <summary>The host of the parasite cannot move (stage 3e: a ship in the squid's grip).</summary>
    public bool Paralyzes { get; init; }

    /// <summary>It mends the vehicles of its owner's team by its damage instead of hurting (the IFV's repair arm, RA2 <c>RepairBullet</c>).</summary>
    public bool Repairs { get; init; }

    /// <summary>A hit on a unit held by an enemy parasite frees it, and the parasite comes out (the dolphin and the squid).</summary>
    public bool FreesParasite { get; init; }

    /// <summary>It hurts nothing: the unit hit changes hands, held by the shooter (RA2 <c>MindControl</c>, <see cref="Systems.MindControlSystem"/>).</summary>
    public bool MindControl { get; init; }

    /// <summary>Units of the shooter's own type and owner in its area are spared (RA2 <c>TypeImmune</c>: Yuri's wave).</summary>
    public bool SparesOwnKind { get; init; }

    /// <summary>How infantry dies of it, for the client: an id such as <c>explode</c>; the simulation does not use it.</summary>
    public string InfantryDeath { get; init; } = "";

    // The verses compare by value, not as the same array.
    public bool Equals(WarheadDef? other) =>
        other is not null && Id == other.Id && Name == other.Name && Verses.SequenceEqual(other.Verses) && Spread == other.Spread
        && PercentAtMax == other.PercentAtMax && WallDamage == other.WallDamage && AffectsAir == other.AffectsAir
        && Temporal == other.Temporal && Bomb == other.Bomb && BombDelay == other.BombDelay && Bridges == other.Bridges
        && Radiation == other.Radiation
        && Parasite == other.Parasite && ParasiteDamage == other.ParasiteDamage && ParasiteDelay == other.ParasiteDelay
        && ParasiteNaval == other.ParasiteNaval && Paralyzes == other.Paralyzes && Repairs == other.Repairs
        && FreesParasite == other.FreesParasite && MindControl == other.MindControl && SparesOwnKind == other.SparesOwnKind
        && InfantryDeath == other.InfantryDeath;

    public override int GetHashCode() => unchecked((Id.Length * 31) + Verses.Length);
}
