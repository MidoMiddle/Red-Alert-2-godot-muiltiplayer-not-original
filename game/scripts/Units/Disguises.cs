using Rts.Client.App;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>How the disguises of stage 3b and 3c look to the local player: what the screen, the pointer and the bars go by.</summary>
public static class Disguises
{
    /// <summary>An enemy holo tank standing still: the player sees a tree there, no unit (stage 3c).</summary>
    public static bool LooksLikeTree(GameWorld world, Unit unit) =>
        TargetingSystem.IsTree(unit) && !world.AreAllies(SimulationHost.LocalPlayer, unit.Owner);
}
