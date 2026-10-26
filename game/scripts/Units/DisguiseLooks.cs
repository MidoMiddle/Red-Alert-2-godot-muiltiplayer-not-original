using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// What a disguise of stage 3b and 3c looks like to a player, without Godot (for the tests): <see cref="Disguises"/> and the unit renderer
/// ask it for the local player.
/// </summary>
public static class DisguiseLooks
{
    /// <summary>An enemy holo tank standing still: <paramref name="viewer"/> sees a tree there, no unit (stage 3c).</summary>
    public static bool LooksLikeTree(GameWorld world, PlayerId viewer, Unit unit) =>
        TargetingSystem.IsTree(unit) && !world.AreAllies(viewer, unit.Owner);

    /// <summary>
    /// The soldier a disguised enemy spy passes for to <paramref name="viewer"/> — the first soldier of the viewer's faction (stage 3b) —
    /// or null when <paramref name="viewer"/> sees the unit as it is: no disguise, one of its team, or a sensor in reach unmasks it.
    /// </summary>
    public static UnitArchetype? PassesFor(GameWorld world, PlayerId viewer, Unit unit)
    {
        if (!unit.Archetype.Has(UnitTalents.Disguised) || world.AreAllies(viewer, unit.Owner) || viewer.IsNeutral
            || Vision.Unmasked(world, viewer, unit)
            || !UnitTypes.TryGetById(world.Player(viewer).Faction.StartingInfantry, out var soldier))
        {
            return null;
        }

        return world.Rules.Unit(soldier);
    }
}
