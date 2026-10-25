using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Repairs (docs/implementation/M3_base_economy.md, 4.8): a building whose repair is on gains <c>repair_rate</c> hit points a tick and
/// costs <c>cost × repair_cost × hit points / max hit points</c>, with the remainder of the rounding carried on, so a whole repair costs
/// exactly its share of the cost. Without the credits for a tick it stands, still on, and goes on when they come; at full hit points it
/// turns off. After the harvesters, before movement (4.15). A workshop (<see cref="Rules.BuildingDef.RepairsVehicles"/>, stage 2c)
/// repairs the vehicles and ships of its owner that stand still beside it the same way, at the same rate and share of their cost.
/// A tech building of the map its owner took (an oil derrick, a hospital, an airfield; not the bridge repair hut, which nothing hurts)
/// is repaired the same way, as if it cost its hit points in credits (ours: the map's buildings have no price).
/// </summary>
public static class RepairSystem
{
    /// <summary>Whether a building of the map can be repaired by its owner: a tech building that can be taken and hurt.</summary>
    public static bool Repairable(Building building) =>
        building.Def is null && BuildingTypes.IsKnown(building.Kind) && building.Type.Capturable;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        RepairBuildings(world, events);
        RepairVehicles(world, events);
        HealThemselves(world, events);
    }

    /// <summary>
    /// A unit that heals itself (the Super Heavy Tank, RA2 <c>SelfHealing</c>) gains its <see cref="Rules.UnitDef.SelfHeal"/> hit points once a
    /// second, up to whole, for nothing; one held by a temporal beam does not. An elite unit heals so too, and a vehicle of a player with
    /// a machine shop (M11 a5) — the most of what it has, they do not add up.
    /// </summary>
    private static void HealThemselves(GameWorld world, List<SimEvent>? events)
    {
        if (world.CurrentTick % SimulationSettings.DefaultTicksPerSecond != 0)
        {
            return;
        }

        var shops = TechBuildingSystem.MachineShopOwners(world);
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, ErasedBy: null } unit && unit.Hp < unit.MaxHp
                && IntMath.Max(
                    unit.Archetype.SelfHeal + (Veterancy.RankOf(unit) == Rank.Elite ? Veterancy.EliteHeal : 0),
                    unit.Archetype.Category == UnitCategory.Vehicle && !unit.Owner.IsNeutral && (shops & (1 << (unit.Owner.Value - 1))) != 0
                        ? TechBuildingSystem.MachineShopHeal
                        : 0) is > 0 and var heal)
            {
                // Self-repair, and an elite unit's (loose ends, stage 3).
                world.SetHp(unit, IntMath.Min(unit.MaxHp, unit.Hp + heal), events);
            }
        }
    }

    private static void RepairBuildings(GameWorld world, List<SimEvent>? events)
    {
        var economy = world.Rules.Economy;
        var share = IntMath.DivRound(economy.RepairCost.Raw * 1000, Fixed.OneRaw);
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, Repairing: true } building || (building.Def is null && !Repairable(building)))
            {
                continue;
            }

            var cost = building.Def?.Cost ?? building.MaxHp;
            var gain = IntMath.Min(economy.RepairRate, building.MaxHp - building.Hp);
            if (gain > 0)
            {
                var due = (long)cost * share * gain + building.RepairCarry;
                var scale = 1000L * building.MaxHp;
                var price = (int)(due / scale);
                var player = world.Player(building.Owner);
                if (price > player.Credits)
                {
                    continue;
                }

                player.Credits -= price;
                world.Stats.Spent(building.Owner, price);
                building.RepairCarry = due % scale;
                world.SetHp(building, building.Hp + gain, events);
            }

            if (building.Hp >= building.MaxHp)
            {
                building.Repairing = false;
                building.RepairCarry = 0;
                events?.Add(new RepairDone(world.CurrentTick, building.Id));
            }
        }
    }

    /// <summary>
    /// Every ready workshop repairs the owner's vehicles and ships beside it — a cell of theirs next to its footprint — that stand
    /// without an order to move: <c>repair_rate</c> hit points a tick, for their share of the cost, rounded up; without the credits
    /// they wait. Infantry and aircraft are not repaired here. A unit beside two workshops is repaired once.
    /// </summary>
    private static void RepairVehicles(GameWorld world, List<SimEvent>? events)
    {
        var economy = world.Rules.Economy;
        var share = IntMath.DivRound(economy.RepairCost.Raw * 1000, Fixed.OneRaw);
        List<EntityId>? near = null;
        HashSet<EntityId>? repaired = null;
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, IsReady: true, Def.RepairsVehicles: true } workshop)
            {
                continue;
            }

            near ??= [];
            repaired ??= [];
            near.Clear();
            var reach = (IntMath.Max(workshop.Width, workshop.Height) / 2 + 2) * WorldPos.LeptonsPerCell;
            world.Spatial.QueryRadius(world, Combat.Aim.CentreOf(workshop), reach, near);
            near.Sort();
            foreach (var id in near)
            {
                if (!world.TryGet(id, out var found) || found is not Unit { IsAlive: true } unit || unit.Owner != workshop.Owner
                    || unit.Archetype.Category is not (UnitCategory.Vehicle or UnitCategory.Naval) || unit.Hp >= unit.MaxHp
                    || unit.MoveTarget is not null || unit.PathPending || !Beside(workshop, unit.Position.ToCell()) || !repaired.Add(unit.Id)
                    || !world.Rules.Database.TryGetUnit(unit.Archetype.Id, out var def))
                {
                    continue;
                }

                var gain = IntMath.Min(economy.RepairRate, unit.MaxHp - unit.Hp);
                var scale = 1000L * unit.MaxHp;
                var price = (int)(((long)def.Cost * share * gain + scale - 1) / scale);
                var player = world.Player(unit.Owner);
                if (price > player.Credits)
                {
                    continue;
                }

                player.Credits -= price;
                world.Stats.Spent(unit.Owner, price);
                world.SetHp(unit, unit.Hp + gain, events);
                if (unit.Parasite != 0)
                {
                    // The workshop pulls the parasite out (stage 3d).
                    ParasiteSystem.Clear(unit);
                    events?.Add(new ParasiteKilled(world.CurrentTick, unit.Id));
                }
            }
        }
    }

    // A cell next to the footprint of the workshop, or under it.
    private static bool Beside(Building workshop, CellPos cell) =>
        cell.X >= workshop.Cell.X - 1 && cell.X <= workshop.FarCell.X + 1 && cell.Y >= workshop.Cell.Y - 1 && cell.Y <= workshop.FarCell.Y + 1;
}
