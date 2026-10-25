using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The production of every player (docs/implementation/M3_base_economy.md, 4.10). The first item of each queue goes on by
/// <c>1000 × power × factories</c> per mille of a tick: power is 100 %, or <c>output / drain</c> on low power, kept between
/// <c>low_power_min_rate</c> and <c>low_power_max_rate</c> (RA2: 50 % and 80 %); factories are <c>1 / multiple_factory^(N − 1)</c> for N
/// ready factories of its category (RA2: each more takes the time × 0.8), and nothing goes on
/// without one. It is paid as it goes: the cost times the share of progress, rounded down, so the payments add up to the cost exactly.
/// When the player cannot pay a tick, the item waits (<see cref="InsufficientFunds"/> once). A finished building waits to be placed; a
/// finished unit comes out at the exit of the main factory — the last one built — as soon as the exit is free, and drives to a free place
/// at the rally point or one cell on. Runs after power and before build-up (4.15).
/// </summary>
public static class ProductionSystem
{
    private const int Categories = ProductionCategories.Count;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var (counts, mains) = Factories(world);
        var rules = world.Rules.Database;
        foreach (var player in world.Players)
        {
            for (var category = ProductionCategory.Infantry; category <= ProductionCategories.Last; category++)
            {
                var queue = player.QueueList(category);
                var factories = counts[player.Id.Value - 1, (int)category - 1];
                if (queue.Count == 0 || factories == 0 || queue[0].Paused)
                {
                    continue;
                }

                var item = queue[0];
                var total = ProductionRules.TotalProgress(rules, item.Kind, item.DefIndex);
                if (item.Progress < total && !Advance(world, player, item, total, category, factories, events))
                {
                    continue;
                }

                if (item.Progress < total)
                {
                    continue;
                }

                if (item.Kind == ProductionKind.Building)
                {
                    if (!item.Ready)
                    {
                        item.Ready = true;
                        events?.Add(new ProductionReady(world.CurrentTick, player.Id, item.DefIndex));
                    }
                }
                else if (mains[player.Id.Value - 1, (int)category - 1] is { } factory && TryRelease(world, player, item, factory, events))
                {
                    queue.RemoveAt(0);
                }
                else
                {
                    item.Ready = true;
                }
            }
        }
    }

    /// <summary>The speed of production in per mille: <c>1000 × power × factories</c>, and the build speed bonus of the player on top (M8 step 2).</summary>
    public static int RatePermille(GameWorld world, PlayerState player, int factories)
    {
        var economy = world.Rules.Economy;
        var power = 1000;
        if (player.LowPower)
        {
            var share = player.PowerDrain > 0 ? (int)(player.PowerOutput * 1000L / player.PowerDrain) : 0;
            power = IntMath.Clamp(share, Permille(economy.LowPowerMinRate), Permille(economy.LowPowerMaxRate));
        }

        // Each factory beyond the first shortens the time by multiple_factory, compounded.
        var factory = 1000;
        var step = Permille(economy.MultipleFactory);
        for (var more = 1; more < factories; more++)
        {
            factory = (int)(factory * 1000L / step);
        }
        return (int)((long)power * factory * (100 + player.Modifiers.BuildSpeedBonusPercent) / 100_000);
    }

    /// <summary>One tick of progress and its payment; false when the player cannot pay it, and the item waits.</summary>
    private static bool Advance(
        GameWorld world, PlayerState player, ProductionItem item, int total, ProductionCategory category, int factories, List<SimEvent>? events)
    {
        var progress = IntMath.Min(total, item.Progress + RatePermille(world, player, factories));
        var due = (int)((long)ProductionRules.CostOf(world.Rules.Database, item.Kind, item.DefIndex) * progress / total);
        var payment = IntMath.Max(0, due - item.Paid);
        if (payment > player.Credits)
        {
            if (!item.WaitingForFunds)
            {
                item.WaitingForFunds = true;
                events?.Add(new InsufficientFunds(world.CurrentTick, player.Id, category));
            }

            return false;
        }

        player.Credits -= payment;
        world.Stats.Spent(player.Id, payment);
        item.Paid = due;
        item.Progress = progress;
        item.WaitingForFunds = false;
        return true;
    }

    /// <summary>
    /// A finished unit comes out at the exit of <paramref name="factory"/> — its <c>exit_cell</c>, or the cell under the middle of its
    /// footprint — when that cell takes it, and drives to the rally point, or one cell further on. An exit the unit cannot enter at all
    /// (a ship at a shipyard whose exit is ashore, stage 2a) gives way to the nearest cell around the footprint it can: the queue never
    /// stands still for good.
    /// </summary>
    private static bool TryRelease(GameWorld world, PlayerState player, ProductionItem item, Building factory, List<SimEvent>? events)
    {
        var def = world.Rules.Database.Units[item.DefIndex];
        if (!UnitTypes.TryGetById(def.Id, out var type))
        {
            return false;
        }

        var archetype = world.Rules.Unit(type);
        if (archetype.Flies)
        {
            // A flier takes off from the middle of its factory, in no cell, and flies to the rally point or a little ahead (stage 2b).
            var middle = new CellPos(factory.Cell.X + factory.Width / 2, factory.Cell.Y + factory.Height / 2);
            var flier = world.SpawnUnit(player.Id, type, middle.Center);
            events?.Add(new UnitProduced(world.CurrentTick, player.Id, flier.Id, factory.Id));
            Veterancy.Produced(world, flier, events);
            var rally = factory.RallyPoint ?? new CellPos(middle.X, factory.Cell.Y + factory.Height + 1).Center;
            UnitOrders.MoveTo(world, flier, world.Contains(rally) ? rally : middle.Center);
            return true;
        }

        var exit = factory.Def!.ExitCell is { } offset
            ? new CellPos(factory.Cell.X + offset.X, factory.Cell.Y + offset.Y)
            : new CellPos(factory.Cell.X + factory.Width / 2, factory.Cell.Y + factory.Height);
        if (!world.Map.CanEnter(exit, archetype.Locomotor) && ExitAround(world, factory, archetype) is { } around)
        {
            exit = around;
        }

        if (!world.Map.CanEnter(exit, archetype.Locomotor) || !world.Reservations.HasRoomFor(exit, archetype.Placement, EntityId.None))
        {
            return false;
        }

        var unit = world.SpawnUnit(player.Id, type, exit.Center, snapToSubcell: true);
        events?.Add(new UnitProduced(world.CurrentTick, player.Id, unit.Id, factory.Id));
        Veterancy.Produced(world, unit, events);
        var point = factory.RallyPoint?.ToCell() ?? new CellPos(exit.X, exit.Y + 1);
        if (FreeCellNear(world, point, exit, archetype) is { } place)
        {
            UnitOrders.MoveTo(world, unit, place.Center);
        }

        if (archetype.Category == UnitCategory.Infantry)
        {
            Clone(world, player, type, archetype, events);
        }

        return true;
    }

    /// <summary>
    /// RA2 cloning vats (<see cref="BuildingDef.ClonesInfantry"/>): every ready vats of the player puts out a free copy of the soldier
    /// just trained, on the nearest free cell round it, in id order, with or without power; a vats with no free cell round it gives none.
    /// </summary>
    private static void Clone(GameWorld world, PlayerState player, UnitType type, UnitArchetype archetype, List<SimEvent>? events)
    {
        List<Building>? vats = null;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def.ClonesInfantry: true } building && building.Owner == player.Id)
            {
                (vats ??= []).Add(building);
            }
        }

        foreach (var building in vats ?? [])
        {
            if (ExitAround(world, building, archetype) is { } cell)
            {
                var clone = world.SpawnUnit(player.Id, type, cell.Center, snapToSubcell: true);
                events?.Add(new UnitCloned(world.CurrentTick, player.Id, clone.Id, building.Id));
                Veterancy.Produced(world, clone, events);
            }
        }
    }

    /// <summary>
    /// The nearest free cell around the footprint of <paramref name="factory"/>, ring by ring up to 4 cells out and row by row inside a
    /// ring, that the unit can enter; null when there is none.
    /// </summary>
    internal static CellPos? ExitAround(GameWorld world, Building factory, UnitArchetype archetype)
    {
        for (var ring = 1; ring <= 4; ring++)
        {
            for (var y = factory.Cell.Y - ring; y <= factory.FarCell.Y + ring; y++)
            {
                for (var x = factory.Cell.X - ring; x <= factory.FarCell.X + ring; x++)
                {
                    var outside = IntMath.Max(
                        IntMath.Max(factory.Cell.X - x, x - factory.FarCell.X), IntMath.Max(factory.Cell.Y - y, y - factory.FarCell.Y));
                    var cell = new CellPos(x, y);
                    if (outside == ring && world.Map.CanEnter(cell, archetype.Locomotor)
                        && world.Reservations.HasRoomFor(cell, archetype.Placement, EntityId.None))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The nearest cell to <paramref name="point"/>, ring by ring up to 8 cells and row by row inside a ring, that the unit can enter and
    /// that nobody stands in, other than the exit: units that came out before may stand at the point, and a unit sent to its own cell
    /// would stay in the exit and close the factory. Null when there is none.
    /// </summary>
    internal static CellPos? FreeCellNear(GameWorld world, CellPos point, CellPos exit, UnitArchetype archetype)
    {
        for (var ring = 0; ring <= 8; ring++)
        {
            for (var y = point.Y - ring; y <= point.Y + ring; y++)
            {
                for (var x = point.X - ring; x <= point.X + ring; x++)
                {
                    var cell = new CellPos(x, y);
                    if (IntMath.Max(IntMath.Abs(x - point.X), IntMath.Abs(y - point.Y)) == ring && cell != exit
                        && world.Map.CanEnter(cell, archetype.Locomotor) && world.Reservations.HasRoomFor(cell, archetype.Placement, EntityId.None))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// For every player and category: how many ready factories it has, and the main one — the last built; in the world's reused
    /// tables (M4 step 15).
    /// </summary>
    private static (int[,] Counts, Building?[,] Mains) Factories(GameWorld world)
    {
        var counts = world.FactoryCounts;
        var mains = world.MainFactories;
        System.Array.Clear(counts);
        System.Array.Clear(mains);
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && !building.Owner.IsNeutral)
            {
                foreach (var category in def.Produces)
                {
                    counts[building.Owner.Value - 1, (int)category - 1]++;
                    // Entities go in id order: the last one seen is the last built.
                    mains[building.Owner.Value - 1, (int)category - 1] = building;
                }
            }
        }

        return (counts, mains);
    }

    private static int Permille(Fixed fraction) => (int)IntMath.DivRound(fraction.Raw * 1000, Fixed.OneRaw);
}
