using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The power of every player (docs/implementation/M3_base_economy.md, 4.11): the output is the sum of the positive <c>power</c> of its
/// ready buildings, the drain the sum of the negative ones; the player is low on power when it drains more than it produces. Worked
/// out only when the buildings changed (<see cref="GameWorld.PowerDirty"/>), at a fixed point of the tick. Reports
/// <see cref="LowPower"/> and <see cref="PowerRestored"/> when a player crosses the line. Low power slows production (step 8) and
/// switches the radar off (step 12).
/// </summary>
public static class PowerSystem
{
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        // A blackout (stage 3b) ends on its tick: the power comes back then.
        foreach (var player in world.Players)
        {
            world.PowerDirty |= player.BlackoutUntil > 0 && player.BlackoutUntil == world.CurrentTick;
        }

        if (!world.PowerDirty)
        {
            return;
        }

        world.PowerDirty = false;
        foreach (var player in world.Players)
        {
            player.BuildingList.Clear();
            var output = 0;
            var drain = 0;
            var robotControl = false;
            foreach (var entity in world.Entities)
            {
                if (entity is Building { IsAlive: true, Def: { } def } building && building.Owner == player.Id)
                {
                    player.BuildingList.Add(building.Id);
                    if (building.IsReady)
                    {
                        output += def.Power > 0 ? def.Power : 0;
                        drain += def.Power < 0 ? -def.Power : 0;
                        robotControl |= def.ControlsRobots;
                    }
                }
            }

            // An infiltrator in a power plant (stage 3b): no power at all until the blackout ends.
            if (world.CurrentTick < player.BlackoutUntil)
            {
                output = 0;
            }

            player.PowerOutput = output;
            player.PowerDrain = drain;
            var low = drain > output;
            if (low != player.LowPower)
            {
                player.LowPower = low;
                events?.Add(low ? new LowPower(world.CurrentTick, player.Id) : new PowerRestored(world.CurrentTick, player.Id));
            }

            // The robots work while a robot control stands ready and has power (stage 3d: the drone control hub is powered).
            player.RobotsOnline = robotControl && !low;
        }
    }
}
