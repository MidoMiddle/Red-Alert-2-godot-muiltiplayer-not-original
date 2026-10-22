using Rts.Simulation.World;

namespace Rts.Simulation.Visibility;

/// <summary>
/// What a player sees (docs/implementation/M4_combat.md, 4.12, 4.14). An order to attack what its player does not see is dropped, so
/// orders cannot be a map hack. The fog of war comes in M4 step 11; until then a player sees everything.
/// </summary>
public static class Vision
{
    public static bool Sees(GameWorld world, PlayerId player, Entity entity) => true;
}
