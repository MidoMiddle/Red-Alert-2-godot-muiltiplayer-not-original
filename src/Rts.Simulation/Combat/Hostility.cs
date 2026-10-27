using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Combat;

/// <summary>
/// The defenses a map places for nobody (2026-10-27, the owner's choice — not RA2, whose Neutral house's defenses stand still): an armed
/// building of a faction's data owned by nobody fires at every player within its reach, and every player's units fire back at it. Nobody
/// has no sight: such a tower aims by its reach alone, through the fog, but it finds no unit under the water and a disguise fools it.
/// </summary>
public static class Hostility
{
    /// <summary>Whether <paramref name="entity"/> is a defense of nobody that fires at everybody.</summary>
    public static bool IsHostileNeutral(Entity entity) =>
        entity is Building { IsAlive: true, Owner.IsNeutral: true, Def: { Wall: false }, Weapons.Length: > 0 };

    /// <summary>
    /// Whether <paramref name="me"/> can aim at <paramref name="candidate"/>: as its owner sees (<see cref="Vision.Sees"/>), or, for a
    /// defense of nobody, by its reach alone — but not under the water.
    /// </summary>
    public static bool Sees(GameWorld world, Entity me, Entity candidate) =>
        me.Owner.IsNeutral
            ? candidate is not Unit diver || !diver.Archetype.Has(UnitTalents.Submerged)
            : Vision.Sees(world, me.Owner, candidate);
}
