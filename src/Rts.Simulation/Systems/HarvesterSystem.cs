using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The harvesters (docs/implementation/M3_base_economy.md, 4.13): every tick each one, in id order, takes a step of its work
/// (<see cref="HarvesterMode"/>). It finds ore by a breadth-first search over the cells it can enter — from its last field within
/// <see cref="FieldRadius"/>, then over the whole map — skipping the cells other harvesters have claimed; takes one stage every
/// <c>harvest_time</c>; goes to the refinery of the lowest <c>distance + 10 × queue</c> when the next stage would not fit; waits near a
/// busy dock, the earliest first; unloads <c>unload_rate</c> credits a tick into the player's purse; and goes back to its field. When
/// there is nothing to find, or it got stuck, it waits and tries again later, so no search runs every tick. One system, before
/// movement, makes the decisions and does the harvesting and unloading (4.15 splits them; one place is simpler and the result the same).
/// </summary>
public static class HarvesterSystem
{
    /// <summary>How far from its last field a harvester looks first.</summary>
    public const int FieldRadius = 24;

    /// <summary>How far from an emptied cell it looks for the next one before it goes to unload.</summary>
    public const int NextCellRadius = 4;

    /// <summary>Ticks between two searches when there is nothing to find (5 s).</summary>
    public const int RetryTicks = 75;

    /// <summary>Ticks to wait after it could not reach its place.</summary>
    public const int StuckTicks = 15;

    /// <summary>Ticks a player's order keeps it from its work (30 s, decision 3 of the M3 plan).</summary>
    public const int ManualTicks = 450;

    /// <summary>How near the dock it stops to queue when the dock is busy.</summary>
    public const int QueueDistance = 3;

    /// <summary>What a harvester already queued at a refinery adds to its cost, in cells of distance.</summary>
    public const int QueuePenalty = 10;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Harvester: { } harvester } unit)
            {
                Step(world, unit, harvester, events);
            }
        }
    }

    private static void Step(GameWorld world, Unit unit, Harvester harvester, List<SimEvent>? events)
    {
        switch (harvester.Mode)
        {
            case HarvesterMode.Manual:
            case HarvesterMode.Waiting:
                if (--harvester.WaitTicks <= 0)
                {
                    harvester.Mode = harvester.Mode == HarvesterMode.Waiting && harvester.WaitingForRefinery
                        ? HarvesterMode.SeekRefinery
                        : HarvesterMode.SeekOre;
                }

                break;
            case HarvesterMode.SeekOre:
                SeekOre(world, unit, harvester);
                break;
            case HarvesterMode.MovingToOre:
                MoveToOre(world, unit, harvester);
                break;
            case HarvesterMode.Harvesting:
                Harvest(world, unit, harvester, events);
                break;
            case HarvesterMode.SeekRefinery:
                SeekRefinery(world, unit, harvester);
                break;
            case HarvesterMode.MovingToRefinery:
            case HarvesterMode.Queued:
            case HarvesterMode.Docking:
            case HarvesterMode.Unloading:
                AtRefinery(world, unit, harvester, events);
                break;
        }
    }

    /// <summary>The nearest unclaimed ore from the field, then from anywhere; none: unload what it has, or wait.</summary>
    private static void SeekOre(GameWorld world, Unit unit, Harvester harvester)
    {
        harvester.Searches++;
        var here = unit.Position.ToCell();
        var found = FindOre(world, unit, harvester.Field ?? here, FieldRadius) ?? FindOre(world, unit, here, int.MaxValue);
        if (found is { } cell)
        {
            GoToOre(world, unit, harvester, cell);
            harvester.Field = cell;
        }
        else if (harvester.Load > 0)
        {
            harvester.Mode = HarvesterMode.SeekRefinery;
        }
        else
        {
            LeaveDock(world, unit);
            Wait(harvester, RetryTicks, forRefinery: false);
        }
    }

    /// <summary>A harvester that waits does not wait in a dock: it drives to the nearest free cell beside it.</summary>
    private static void LeaveDock(GameWorld world, Unit unit)
    {
        var here = unit.Position.ToCell();
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def.DockCell: not null } refinery && DockOf(refinery) == here
                && ProductionSystem.FreeCellNear(world, here, here, unit.Archetype) is { } place)
            {
                UnitOrders.MoveTo(world, unit, place.Center);
                return;
            }
        }
    }

    private static void MoveToOre(GameWorld world, Unit unit, Harvester harvester)
    {
        var target = harvester.Target!.Value;
        if (world.Map.OreAt(target).IsEmpty)
        {
            harvester.Mode = HarvesterMode.SeekOre;
            return;
        }

        if (!Idle(unit))
        {
            return;
        }

        if (unit.Position.ToCell() == target)
        {
            harvester.Mode = HarvesterMode.Harvesting;
            harvester.Timer = 0;
        }
        else
        {
            Wait(harvester, StuckTicks, forRefinery: false);
        }
    }

    /// <summary>One stage of ore every harvest_time; on to the next cell when this one is empty, to the refinery when full.</summary>
    private static void Harvest(GameWorld world, Unit unit, Harvester harvester, List<SimEvent>? events)
    {
        var cell = unit.Position.ToCell();
        var ore = world.Map.OreAt(cell);
        if (ore.IsEmpty)
        {
            if (FindOre(world, unit, cell, NextCellRadius) is { } next)
            {
                GoToOre(world, unit, harvester, next);
            }
            else
            {
                harvester.Target = null;
                harvester.Mode = harvester.Load > 0 ? HarvesterMode.SeekRefinery : HarvesterMode.SeekOre;
            }

            return;
        }

        var stage = world.Rules.CreditsOf(ore with { Stage = 0 });
        if (harvester.Load + stage > unit.Archetype.HarvestCapacity)
        {
            harvester.Target = null;
            harvester.Mode = HarvesterMode.SeekRefinery;
            return;
        }

        if (++harvester.Timer < unit.Archetype.HarvestTime)
        {
            return;
        }

        harvester.Timer = 0;
        harvester.Load += stage;
        if (ore.Stage == 0)
        {
            world.Map.SetOre(cell, OreCell.Empty);
            events?.Add(new OreDepleted(world.CurrentTick, cell));
        }
        else
        {
            world.Map.SetOre(cell, ore with { Stage = ore.Stage - 1 });
        }
    }

    /// <summary>The refinery of the lowest cost; none: wait with the load.</summary>
    private static void SeekRefinery(GameWorld world, Unit unit, Harvester harvester)
    {
        harvester.Target = null;
        var refinery = ChooseRefinery(world, unit, harvester);
        if (refinery is null)
        {
            harvester.Refinery = EntityId.None;
            Wait(harvester, RetryTicks, forRefinery: true);
            return;
        }

        harvester.Refinery = refinery.Id;
        harvester.Mode = HarvesterMode.MovingToRefinery;
        UnitOrders.MoveTo(world, unit, DockOf(refinery).Center);
    }

    private static void AtRefinery(GameWorld world, Unit unit, Harvester harvester, List<SimEvent>? events)
    {
        if (!world.TryGet(harvester.Refinery, out var entity) || entity is not Building { IsAlive: true } refinery)
        {
            // The refinery fell: another one, or wait; what is not unloaded stays on board.
            harvester.Mode = HarvesterMode.SeekRefinery;
            return;
        }

        var dock = DockOf(refinery);
        if (harvester.Mode is HarvesterMode.MovingToRefinery or HarvesterMode.Queued && unit.Position.ToCell() == dock)
        {
            // It got into the dock on its way: it unloads first. One still driving in gives the dock up and is next in line.
            foreach (var driver in world.Entities)
            {
                if (driver is Unit { IsAlive: true, Harvester: { Mode: HarvesterMode.Docking } other } && other.Refinery == refinery.Id)
                {
                    other.Mode = HarvesterMode.Queued;
                    other.QueueTick = int.MinValue;
                }
            }

            if (DockFree(world, refinery, unit))
            {
                harvester.Mode = HarvesterMode.Docking;
                harvester.Timer = 0;
                UnitOrders.MoveTo(world, unit, dock.Center);
                return;
            }
        }

        switch (harvester.Mode)
        {
            case HarvesterMode.MovingToRefinery:
                if (DockFree(world, refinery, unit) && !AnyQueued(world, refinery))
                {
                    harvester.Mode = HarvesterMode.Docking;
                    harvester.Timer = 0;
                    UnitOrders.MoveTo(world, unit, dock.Center);
                }
                else if (Chebyshev(unit.Position.ToCell(), dock) <= QueueDistance || Idle(unit))
                {
                    harvester.Mode = HarvesterMode.Queued;
                    harvester.QueueTick = world.CurrentTick;
                    if (ProductionSystem.FreeCellNear(world, dock, dock, unit.Archetype) is { } place)
                    {
                        UnitOrders.MoveTo(world, unit, place.Center);
                    }
                }

                break;
            case HarvesterMode.Queued:
                if (DockFree(world, refinery, unit) && FirstQueued(world, refinery) == unit)
                {
                    harvester.Mode = HarvesterMode.Docking;
                    harvester.Timer = 0;
                    UnitOrders.MoveTo(world, unit, dock.Center);
                }

                break;
            case HarvesterMode.Docking:
                if (!Idle(unit))
                {
                    break;
                }

                if (unit.Position.ToCell() == dock)
                {
                    harvester.Mode = HarvesterMode.Unloading;
                    events?.Add(new HarvesterUnloading(world.CurrentTick, unit.Id, refinery.Id));
                }
                else if (++harvester.Timer >= StuckTicks)
                {
                    // Something stands in the way: try again now and then; the dock stays this harvester's.
                    harvester.Timer = 0;
                    UnitOrders.MoveTo(world, unit, dock.Center);
                }

                break;
            case HarvesterMode.Unloading:
                var amount = IntMath.Min(unit.Archetype.UnloadRate, harvester.Load);
                harvester.Load -= amount;
                Earn(world.Player(unit.Owner), amount);
                world.Stats.Harvested(unit.Owner, amount);
                if (harvester.Load == 0)
                {
                    harvester.Refinery = EntityId.None;
                    harvester.Preferred = EntityId.None;
                    harvester.Mode = HarvesterMode.SeekOre;
                }

                break;
        }
    }

    /// <summary>
    /// The credits of <paramref name="ore"/> unloaded: as much, and the income bonus of the player on top — its level's (M8 step 2) and
    /// its buildings' (stage 2c), added — its hundredths kept until they make a credit.
    /// </summary>
    internal static void Earn(PlayerState player, int ore)
    {
        var hundredths = ore * (100 + player.Modifiers.IncomeBonusPercent + player.BuildingIncomeBonusPercent) + player.IncomeCarry;
        player.Credits += hundredths / 100;
        player.IncomeCarry = hundredths % 100;
    }

    private static void GoToOre(GameWorld world, Unit unit, Harvester harvester, CellPos cell)
    {
        harvester.Target = cell;
        harvester.Mode = HarvesterMode.MovingToOre;
        if (unit.Position.ToCell() != cell)
        {
            UnitOrders.MoveTo(world, unit, cell.Center);
        }
    }

    private static void Wait(Harvester harvester, int ticks, bool forRefinery)
    {
        harvester.Mode = HarvesterMode.Waiting;
        harvester.WaitTicks = ticks;
        harvester.WaitingForRefinery = forRefinery;
    }

    /// <summary>True when the unit stands still with nothing planned: it arrived, or gave up.</summary>
    private static bool Idle(Unit unit) => unit.MoveTarget is null && !unit.PathPending;

    /// <summary>
    /// A breadth-first search from <paramref name="origin"/> over the cells the harvester can enter, within <paramref name="radius"/>
    /// (Chebyshev), for the first cell of ore no other harvester has claimed. The neighbours go in the order of the path search.
    /// </summary>
    public static CellPos? FindOre(GameWorld world, Unit unit, CellPos origin, int radius)
    {
        var map = world.Map;
        if (!map.Contains(origin))
        {
            return null;
        }

        var claimed = Claimed(world, unit);
        var locomotor = unit.Archetype.Locomotor;
        var seen = new bool[map.Width * map.Height];
        var queue = new Queue<CellPos>();
        queue.Enqueue(origin);
        seen[origin.Y * map.Width + origin.X] = true;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (!map.OreAt(cell).IsEmpty && !claimed.Contains(cell.Y * map.Width + cell.X))
            {
                return cell;
            }

            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (!map.Contains(next) || seen[next.Y * map.Width + next.X] || Chebyshev(next, origin) > radius || !map.CanEnter(next, locomotor))
                {
                    continue;
                }

                seen[next.Y * map.Width + next.X] = true;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    /// <summary>The cells other harvesters of any player go to or work on, by index.</summary>
    private static SortedSet<int> Claimed(GameWorld world, Unit self)
    {
        var claimed = new SortedSet<int>();
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Harvester: { Target: { } target } } other && other != self)
            {
                claimed.Add(target.Y * world.Map.Width + target.X);
            }
        }

        return claimed;
    }

    /// <summary>The player's ready refinery of the lowest <c>distance + 10 × queue</c>; the one the player sent it to first.</summary>
    private static Building? ChooseRefinery(GameWorld world, Unit unit, Harvester harvester)
    {
        Building? best = null;
        var bestCost = int.MaxValue;
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, IsReady: true, Def.DockCell: not null } refinery || refinery.Owner != unit.Owner)
            {
                continue;
            }

            if (refinery.Id == harvester.Preferred)
            {
                return refinery;
            }

            var cost = Chebyshev(unit.Position.ToCell(), DockOf(refinery)) + QueuePenalty * Users(world, refinery, unit);
            if (cost < bestCost)
            {
                (best, bestCost) = (refinery, cost);
            }
        }

        return best;
    }

    /// <summary>The cell of the dock of a refinery.</summary>
    public static CellPos DockOf(Building refinery)
    {
        var offset = refinery.Def!.DockCell!.Value;
        return new CellPos(refinery.Cell.X + offset.X, refinery.Cell.Y + offset.Y);
    }

    /// <summary>True when no other harvester drives into the dock or unloads in it.</summary>
    public static bool DockFree(GameWorld world, Building refinery, Unit? self = null)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Harvester: { Mode: HarvesterMode.Docking or HarvesterMode.Unloading } harvester } other
                && other != self && harvester.Refinery == refinery.Id)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AnyQueued(GameWorld world, Building refinery) => FirstQueued(world, refinery) is not null;

    /// <summary>The harvester that queued first at the refinery (the lower id on the same tick).</summary>
    private static Unit? FirstQueued(GameWorld world, Building refinery)
    {
        Unit? first = null;
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Harvester: { Mode: HarvesterMode.Queued } harvester } unit && harvester.Refinery == refinery.Id
                && (first is null || harvester.QueueTick < first.Harvester!.QueueTick))
            {
                first = unit;
            }
        }

        return first;
    }

    /// <summary>The other harvesters bound for, queued at or in the dock of the refinery.</summary>
    private static int Users(GameWorld world, Building refinery, Unit self)
    {
        var users = 0;
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Harvester: { } harvester } other && other != self && harvester.Refinery == refinery.Id
                && harvester.Mode is HarvesterMode.MovingToRefinery or HarvesterMode.Queued or HarvesterMode.Docking or HarvesterMode.Unloading)
            {
                users++;
            }
        }

        return users;
    }

    private static int Chebyshev(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
