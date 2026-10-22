using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The growing of new buildings (docs/implementation/M3_base_economy.md, 4.8): a placed building stands and blocks its cells for the
/// <c>build_up</c> of its definition, then it works. Runs after power (4.15), so a building that finishes gives power from the next
/// tick on; the technologies, worked out at the end of the tick, open the same tick.
/// </summary>
public static class BuildUpSystem
{
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, BuildUpLeft: > 0 } building)
            {
                building.BuildUpLeft--;
                if (building.BuildUpLeft == 0)
                {
                    world.CompleteBuildUp(building);
                    events?.Add(new BuildUpComplete(world.CurrentTick, building.Id));
                }
            }
        }
    }
}
