using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Repairs (docs/implementation/M3_base_economy.md, 4.8): a building whose repair is on gains <c>repair_rate</c> hit points a tick and
/// costs <c>cost × repair_cost × hit points / max hit points</c>, with the remainder of the rounding carried on, so a whole repair costs
/// exactly its share of the cost. Without the credits for a tick it stands, still on, and goes on when they come; at full hit points it
/// turns off. After the harvesters, before movement (4.15).
/// </summary>
public static class RepairSystem
{
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var economy = world.Rules.Economy;
        var share = IntMath.DivRound(economy.RepairCost.Raw * 1000, Fixed.OneRaw);
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, Repairing: true, Def: { } def } building)
            {
                continue;
            }

            var gain = IntMath.Min(economy.RepairRate, building.MaxHp - building.Hp);
            if (gain > 0)
            {
                var due = (long)def.Cost * share * gain + building.RepairCarry;
                var scale = 1000L * building.MaxHp;
                var price = (int)(due / scale);
                var player = world.Player(building.Owner);
                if (price > player.Credits)
                {
                    continue;
                }

                player.Credits -= price;
                building.RepairCarry = due % scale;
                building.Hp += gain;
            }

            if (building.Hp >= building.MaxHp)
            {
                building.Repairing = false;
                building.RepairCarry = 0;
                events?.Add(new RepairDone(world.CurrentTick, building.Id));
            }
        }
    }
}
