using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Validates and applies commands to the world. Every peer runs exactly the same checks, so an invalid
/// command is ignored silently and identically everywhere. Never throws for a bad command: a malicious
/// or stale command must not be able to crash the game.
/// </summary>
public static class CommandProcessor
{
    /// <summary>Applies the command. Returns true if the world changed.</summary>
    /// <param name="events">Receives events about what changed; null when the caller does not need them.</param>
    public static bool Apply(GameWorld world, Command command, List<SimEvent>? events = null)
    {
        if (!IsPlayablePlayer(command.Player))
        {
            return false;
        }

        var applied = command switch
        {
            SpawnTestUnitCommand spawn => ApplySpawn(world, spawn, events),
            MoveCommand move => ApplyRoute(world, move, [move.Target], loop: false, move.Queue, attackMove: false, events),
            AttackMoveCommand attack => ApplyRoute(world, attack, [attack.Target], loop: false, attack.Queue, attackMove: true, events),
            WaypointsCommand route => ApplyRoute(world, route, route.Points, route.Loop, append: false, attackMove: false, events),
            StopCommand stop => ApplyStop(world, stop),
            ScatterCommand scatter => ApplyScatter(world, scatter, events),
            DamageMapObjectCommand damage => damage.Amount is >= 1 and <= DamageMapObjectCommand.MaxAmount
                && world.Map.Contains(damage.Cell) && world.DamageAtCell(damage.Cell, damage.Amount, events),
            PlaceBuildingCommand place => ApplyPlaceBuilding(world, place, events),
            DeployCommand deploy => ApplyDeploy(world, deploy, events),
            ProduceCommand produce => ApplyProduce(world, produce),
            PauseProductionCommand pause => ApplyPause(world, pause),
            CancelProductionCommand cancel => ApplyCancel(world, cancel, events),
            SetRallyPointCommand rally => ApplyRallyPoint(world, rally),
            HarvestCommand harvest => ApplyHarvest(world, harvest),
            SellCommand sell => ApplySell(world, sell, events),
            ToggleRepairCommand repair => ApplyToggleRepair(world, repair, events),
            PlaceWallsCommand walls => ApplyPlaceWalls(world, walls, events),
            AttackCommand attack => ApplyAttack(world, attack),
            ForceAttackCommand force => ApplyForceAttack(world, force),
            SetStanceCommand stance => ApplySetStance(world, stance),
            _ => throw new ArgumentException($"Unknown command type {command.GetType().Name}.", nameof(command)),
        };
        if (applied && command is UnitsCommand { Type: not (CommandType.Harvest or CommandType.Attack or CommandType.ForceAttack or CommandType.SetStance) } order)
        {
            HarvestersFollow(world, order);
        }

        return applied;
    }

    /// <summary>The player's ready building from the data, if the id names one.</summary>
    private static Building? OwnReady(GameWorld world, PlayerId player, EntityId id) =>
        world.TryGet(id, out var entity) && entity is Building { IsAlive: true, IsReady: true, Def: not null } building && building.Owner == player
            ? building
            : null;

    /// <summary>Sells the player's ready building: <c>cost × sell_refund × hp / max hp</c> back, rounded down (4.8).</summary>
    private static bool ApplySell(GameWorld world, SellCommand command, List<SimEvent>? events)
    {
        if (OwnReady(world, command.Player, command.Building) is not { } building)
        {
            return false;
        }

        var share = IntMath.DivRound(world.Rules.Economy.SellRefund.Raw * 1000, Fixed.OneRaw);
        var refund = (int)((long)building.Def!.Cost * share * building.Hp / (1000L * building.MaxHp));
        world.SellBuilding(building, refund, events);
        return true;
    }

    /// <summary>Turns the repair of the player's ready building on or off (4.8).</summary>
    private static bool ApplyToggleRepair(GameWorld world, ToggleRepairCommand command, List<SimEvent>? events)
    {
        if (OwnReady(world, command.Player, command.Building) is not { } building)
        {
            return false;
        }

        building.Repairing = !building.Repairing;
        events?.Add(new RepairToggled(world.CurrentTick, building.Id, building.Repairing));
        return true;
    }

    /// <summary>
    /// A line of walls (4.8): the cells in order, each where the placement rules allow; the first takes the building ready in the queue,
    /// every further one is paid at once. A refused cell is skipped; the line ends when the credits do.
    /// </summary>
    private static bool ApplyPlaceWalls(GameWorld world, PlaceWallsCommand command, List<SimEvent>? events)
    {
        var buildings = world.Rules.Database.Buildings;
        if (command.DefIndex < 0 || command.DefIndex >= buildings.Length || command.Cells.IsEmpty || command.Cells.Length > PlaceWallsCommand.MaxCells)
        {
            return false;
        }

        var def = buildings[command.DefIndex];
        var player = world.Player(command.Player);
        if (def.Faction != player.Faction.Id || def.Footprint != new Rules.CellSize(1, 1))
        {
            return false;
        }

        var queue = player.QueueList(def.Category);
        var ready = queue.Count > 0 && queue[0] is { Kind: ProductionKind.Building, Ready: true } item && item.DefIndex == command.DefIndex;
        if (!ready)
        {
            var problem = player.CanBuildBuilding(command.DefIndex) ? PlacementProblem.NotReady : PlacementProblem.NotAvailable;
            events?.Add(new CannotPlace(world.CurrentTick, command.Player, command.DefIndex, command.Cells[0], problem));
            return false;
        }

        var placed = 0;
        var first = PlacementProblem.None;
        foreach (var cell in command.Cells)
        {
            var problem = PlacementRules.Check(world, command.Player, def, cell);
            if (problem != PlacementProblem.None)
            {
                first = first == PlacementProblem.None ? problem : first;
                continue;
            }

            if (placed == 0)
            {
                queue.RemoveAt(0);
            }
            else if (player.Credits < def.Cost)
            {
                break;
            }
            else
            {
                player.Credits -= def.Cost;
            }

            var wall = world.CreateBuilding(command.Player, def, cell, ready: false, events);
            events?.Add(new BuildingPlaced(world.CurrentTick, wall.Id, command.Player, command.DefIndex, cell));
            placed++;
        }

        if (placed == 0)
        {
            events?.Add(new CannotPlace(world.CurrentTick, command.Player, command.DefIndex, command.Cells[0], first));
        }

        return placed > 0;
    }

    /// <summary>
    /// An order to attack an enemy (M4, 4.12): a living unit or building of another player that the player sees, not a wall (a wall
    /// takes a forced attack, as in RA2). Every own armed unit
    /// with a ground weapon that hurts its armor takes it, drops its route and follows it whatever its stance; a harvester keeps to its
    /// work and only fires. Refused when no unit takes it.
    /// </summary>
    private static bool ApplyAttack(GameWorld world, AttackCommand command)
    {
        if (!world.TryGet(command.Target, out var target) || !target.IsAlive || target is not (Unit or Building)
            || target is Building { Def.ExtendsBase: false } || target.Owner == command.Player || target.Owner.IsNeutral || !Vision.Sees(world, command.Player, target))
        {
            return false;
        }

        var any = false;
        foreach (var unit in OwnUnits(world, command))
        {
            if (unit.Weapons.Length > 0 && TargetingSystem.BestShare(world.Rules, TargetingSystem.WeaponsOf(unit), target) > 0)
            {
                Order(world, unit, TargetOrder.Attack, target.Id, null);
                any = true;
            }
        }

        return any;
    }

    /// <summary>
    /// A forced attack (M4, 4.12): on any living unit or building the player sees, own and neutral ones and walls too, or on a point
    /// of the map. Every own unit with a ground weapon takes it, but not one ordered to fire at itself.
    /// </summary>
    private static bool ApplyForceAttack(GameWorld world, ForceAttackCommand command)
    {
        Entity? target = null;
        if (command.Target is { } id)
        {
            if (!world.TryGet(id, out target) || !target.IsAlive || target is not (Unit or Building) || !Vision.Sees(world, command.Player, target))
            {
                return false;
            }
        }
        else if (!world.Contains(command.Point))
        {
            return false;
        }

        var any = false;
        foreach (var unit in OwnUnits(world, command))
        {
            if (unit.Weapons.Length > 0 && unit != target && TargetingSystem.HitsGround(world.Rules, TargetingSystem.WeaponsOf(unit)))
            {
                Order(world, unit, TargetOrder.Force, target?.Id, target is null ? command.Point : null);
                any = true;
            }
        }

        return any;
    }

    /// <summary>Gives the unit the target of an order; a unit that is not a harvester drops its route for it.</summary>
    private static void Order(GameWorld world, Unit unit, TargetOrder order, EntityId? target, WorldPos? point)
    {
        if (unit.Harvester is null)
        {
            UnitOrders.Stop(world, unit);
        }

        UnitOrders.ForgetTarget(unit);
        unit.Target = target;
        unit.TargetOrder = order;
        unit.TargetPoint = point;
    }

    /// <summary>The stance of the own armed units but the harvesters (M4, 4.11); each guards from where it stands.</summary>
    private static bool ApplySetStance(GameWorld world, SetStanceCommand command)
    {
        if (command.Stance is not (Stance.Aggressive or Stance.Guard or Stance.HoldPosition))
        {
            return false;
        }

        var any = false;
        foreach (var unit in OwnUnits(world, command))
        {
            if (unit.Weapons.Length > 0 && unit.Harvester is null)
            {
                unit.Stance = command.Stance;
                unit.Anchor = unit.Position;
                any = true;
            }
        }

        return any;
    }

    /// <summary>The player's harvesters among the units work the field at the cell from now on (4.13).</summary>
    private static bool ApplyHarvest(GameWorld world, HarvestCommand command)
    {
        if (!world.Map.Contains(command.Cell))
        {
            return false;
        }

        var any = false;
        foreach (var unit in OwnUnits(world, command))
        {
            if (unit.Harvester is { } harvester)
            {
                harvester.Field = command.Cell;
                harvester.Target = null;
                harvester.Refinery = EntityId.None;
                harvester.Preferred = EntityId.None;
                harvester.Mode = HarvesterMode.SeekOre;
                any = true;
            }
        }

        return any;
    }

    /// <summary>
    /// A harvester given another order leaves its work: sent onto one of the player's refineries with a load, it unloads there;
    /// otherwise it follows the order and goes back to work by itself after <see cref="Systems.HarvesterSystem.ManualTicks"/>
    /// (decision 3 of the M3 plan).
    /// </summary>
    private static void HarvestersFollow(GameWorld world, UnitsCommand command)
    {
        var target = command switch
        {
            MoveCommand move => move.Target,
            AttackMoveCommand attack => attack.Target,
            _ => (WorldPos?)null,
        };
        var refinery = target is { } point && world.Contains(point) && world.BuildingAt(point.ToCell()) is { IsAlive: true, Def.DockCell: not null } building
            && building.Owner == command.Player
            ? building
            : null;
        foreach (var unit in OwnUnits(world, command))
        {
            if (unit.Harvester is not { } harvester)
            {
                continue;
            }

            harvester.Target = null;
            harvester.Refinery = EntityId.None;
            if (refinery is not null && harvester.Load > 0)
            {
                harvester.Preferred = refinery.Id;
                harvester.Mode = HarvesterMode.SeekRefinery;
            }
            else
            {
                harvester.Mode = HarvesterMode.Manual;
                harvester.WaitTicks = Systems.HarvesterSystem.ManualTicks;
            }
        }
    }

    private static bool IsPlayablePlayer(PlayerId player) => player.Value >= 1 && player.Value <= PlayerId.MaxPlayers;

    /// <summary>
    /// A building of the player's faction that is ready in its production queue, where <see cref="PlacementRules"/> allow
    /// (docs/implementation/M3_base_economy.md, 4.8); it then leaves the queue. A wrong index or another faction's building is ignored;
    /// a building that is not ready (or not even available) or not allowed there is refused with <see cref="CannotPlace"/>, so the
    /// player learns why.
    /// </summary>
    private static bool ApplyPlaceBuilding(GameWorld world, PlaceBuildingCommand command, List<SimEvent>? events)
    {
        var buildings = world.Rules.Database.Buildings;
        if (command.DefIndex < 0 || command.DefIndex >= buildings.Length)
        {
            return false;
        }

        var def = buildings[command.DefIndex];
        var player = world.Player(command.Player);
        if (def.Faction != player.Faction.Id)
        {
            return false;
        }

        var queue = player.QueueList(def.Category);
        var ready = queue.Count > 0 && queue[0] is { Kind: ProductionKind.Building, Ready: true } item && item.DefIndex == command.DefIndex;
        var problem = ready
            ? PlacementRules.Check(world, command.Player, def, command.Cell)
            : player.CanBuildBuilding(command.DefIndex) ? PlacementProblem.NotReady : PlacementProblem.NotAvailable;
        if (problem != PlacementProblem.None)
        {
            events?.Add(new CannotPlace(world.CurrentTick, command.Player, command.DefIndex, command.Cell, problem));
            return false;
        }

        queue.RemoveAt(0);
        var building = world.CreateBuilding(command.Player, def, command.Cell, ready: false, events);
        events?.Add(new BuildingPlaced(world.CurrentTick, building.Id, command.Player, command.DefIndex, command.Cell));
        return true;
    }

    /// <summary>
    /// Production of a definition of the player's faction (4.10): resumes its first item when that one is paused; otherwise, when it is
    /// available, a ready factory of its category stands and the queue has room, adds the items. A building or defense takes the queue
    /// alone; infantry and vehicles queue up to <see cref="ProductionRules.MaxUnitQueue"/>.
    /// </summary>
    private static bool ApplyProduce(GameWorld world, ProduceCommand command)
    {
        var rules = world.Rules.Database;
        if (!ProductionRules.IsKnown(rules, command.Kind, command.DefIndex) || command.Count is < 1 or > ProductionRules.MaxOrder)
        {
            return false;
        }

        var player = world.Player(command.Player);
        var faction = command.Kind == ProductionKind.Unit ? rules.Units[command.DefIndex].Faction : rules.Buildings[command.DefIndex].Faction;
        if (faction != player.Faction.Id
            || (command.Kind == ProductionKind.Unit && !UnitTypes.TryGetById(rules.Units[command.DefIndex].Id, out _)))
        {
            return false;
        }

        var category = ProductionRules.CategoryOf(rules, command.Kind, command.DefIndex);
        var queue = player.QueueList(category);
        if (FirstOf(queue, command.Kind, command.DefIndex) is { Paused: true } paused)
        {
            paused.Paused = false;
            return true;
        }

        var available = command.Kind == ProductionKind.Unit ? player.CanBuildUnit(command.DefIndex) : player.CanBuildBuilding(command.DefIndex);
        if (!available || !ProductionRules.HasFactory(world, command.Player, category))
        {
            return false;
        }

        var room = category is ProductionCategory.Building or ProductionCategory.Defense
            ? 1 - queue.Count
            : IntMath.Min(command.Count, ProductionRules.MaxUnitQueue - queue.Count);
        for (var i = 0; i < room; i++)
        {
            queue.Add(new ProductionItem(command.Kind, command.DefIndex));
        }

        return room > 0;
    }

    /// <summary>Pauses the first unfinished item of the definition (4.10).</summary>
    private static bool ApplyPause(GameWorld world, PauseProductionCommand command)
    {
        var rules = world.Rules.Database;
        if (!ProductionRules.IsKnown(rules, command.Kind, command.DefIndex))
        {
            return false;
        }

        var queue = world.Player(command.Player).QueueList(ProductionRules.CategoryOf(rules, command.Kind, command.DefIndex));
        if (FirstOf(queue, command.Kind, command.DefIndex) is not { Paused: false, Ready: false } item)
        {
            return false;
        }

        item.Paused = true;
        return true;
    }

    /// <summary>Takes the last item of the definition out of its queue and gives back all that was paid for it (4.10).</summary>
    private static bool ApplyCancel(GameWorld world, CancelProductionCommand command, List<SimEvent>? events)
    {
        var rules = world.Rules.Database;
        if (!ProductionRules.IsKnown(rules, command.Kind, command.DefIndex))
        {
            return false;
        }

        var player = world.Player(command.Player);
        var queue = player.QueueList(ProductionRules.CategoryOf(rules, command.Kind, command.DefIndex));
        for (var i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i].Kind == command.Kind && queue[i].DefIndex == command.DefIndex)
            {
                var refund = queue[i].Paid;
                queue.RemoveAt(i);
                player.Credits += refund;
                events?.Add(new ProductionCancelled(world.CurrentTick, command.Player, command.Kind, command.DefIndex, refund));
                return true;
            }
        }

        return false;
    }

    /// <summary>The rally point of the player's standing factory, anywhere on the map (4.10).</summary>
    private static bool ApplyRallyPoint(GameWorld world, SetRallyPointCommand command)
    {
        if (!world.TryGet(command.Building, out var entity) || entity is not Building { IsAlive: true, Def.Produces.Length: > 0 } factory
            || factory.Owner != command.Player || !world.Contains(command.Point))
        {
            return false;
        }

        factory.RallyPoint = command.Point;
        return true;
    }

    private static ProductionItem? FirstOf(List<ProductionItem> queue, ProductionKind kind, int defIndex)
    {
        foreach (var item in queue)
        {
            if (item.Kind == kind && item.DefIndex == defIndex)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// The player's MCV becomes the construction yard of its faction, its footprint centred on the MCV's cell (4.9). Another unit, or
    /// someone else's, is ignored; a place where the yard cannot stand is refused with <see cref="CannotDeploy"/> and the MCV stays.
    /// </summary>
    private static bool ApplyDeploy(GameWorld world, DeployCommand command, List<SimEvent>? events)
    {
        var faction = world.Player(command.Player).Faction;
        if (!world.TryGet(command.Unit, out var entity) || entity is not Unit { IsAlive: true } unit || unit.Owner != command.Player
            || unit.Archetype.Id != faction.Mcv || !world.Rules.Database.TryGetBuilding(faction.ConstructionYard, out var yard))
        {
            return false;
        }

        var centre = unit.Position.ToCell();
        var cell = new CellPos(centre.X - (yard.Footprint.Width - 1) / 2, centre.Y - (yard.Footprint.Height - 1) / 2);
        var problem = PlacementRules.Check(world, command.Player, yard, cell, needsBase: false, ignoreUnit: unit.Id);
        if (problem != PlacementProblem.None)
        {
            events?.Add(new CannotDeploy(world.CurrentTick, command.Player, unit.Id, problem));
            return false;
        }

        // The MCV leaves the world at the end of the tick and frees its cell then; the yard takes the cells now.
        unit.MarkDead();
        var building = world.CreateBuilding(command.Player, yard, cell, ready: false, events);
        events?.Add(new BuildingPlaced(world.CurrentTick, building.Id, command.Player, building.DefIndex, cell));
        events?.Add(new Deployed(world.CurrentTick, unit.Id, building.Id));
        return true;
    }

    private static bool ApplySpawn(GameWorld world, SpawnTestUnitCommand command, List<SimEvent>? events)
    {
        if (!world.Contains(command.Position)
            || !UnitTypes.IsKnown(command.UnitType)
            || !world.Map.CanEnter(command.Position.ToCell(), world.Rules.Unit(command.UnitType).Locomotor)
            || !world.Reservations.HasRoomFor(command.Position.ToCell(), world.Rules.Unit(command.UnitType).Placement, EntityId.None))
        {
            return false;
        }

        var unit = world.SpawnUnit(command.Player, command.UnitType, command.Position, snapToSubcell: true);
        events?.Add(new UnitSpawned(world.CurrentTick, unit.Id, unit.Owner, unit.Type, unit.Position));
        return true;
    }

    // A point outside the map rejects the whole command; foreign, dead, unknown or repeated units are skipped one by one.
    private static bool ApplyRoute(
        GameWorld world, UnitsCommand command, ImmutableArray<WorldPos> points, bool loop, bool append, bool attackMove, List<SimEvent>? events)
    {
        foreach (var point in points)
        {
            if (!world.Contains(point))
            {
                return false;
            }
        }

        var units = OwnUnits(world, command);
        if (units.Count == 0)
        {
            return false;
        }

        // Each unit of a group gets a place of its own at every point, laid out now in the group's current shape
        // (docs/implementation/M2_unit_control.md, 4.6 and 4.10).
        var legs = new Dictionary<EntityId, ImmutableArray<UnitOrder>.Builder>();
        foreach (var unit in units)
        {
            legs[unit.Id] = ImmutableArray.CreateBuilder<UnitOrder>(points.Length);
        }

        foreach (var point in points)
        {
            foreach (var (unit, destination) in GroupDestinations.Assign(world, units, point))
            {
                legs[unit.Id].Add(new UnitOrder(destination, attackMove));
            }
        }

        foreach (var unit in units)
        {
            // A new route ends the fight (M4, 4.12): a plain move does not look for enemies until it arrives.
            UnitOrders.ForgetTarget(unit);
            var route = legs[unit.Id].ToImmutable();
            UnitOrders.GiveRoute(world, unit, route, loop, append);
            events?.Add(new UnitMoveStarted(world.CurrentTick, unit.Id, route[0].Point));
        }

        return true;
    }

    private static bool ApplyStop(GameWorld world, StopCommand command)
    {
        var units = OwnUnits(world, command);
        foreach (var unit in units)
        {
            UnitOrders.Stop(world, unit);
            UnitOrders.ForgetTarget(unit);
            unit.Anchor = unit.Position;
        }

        return units.Count > 0;
    }

    // Units in id order, each drawing from the simulation's generator: a random free neighbouring cell it can step to.
    private static bool ApplyScatter(GameWorld world, ScatterCommand command, List<SimEvent>? events)
    {
        var units = OwnUnits(world, command);
        foreach (var unit in units)
        {
            var cell = unit.Position.ToCell();
            var free = new List<CellPos>();
            foreach (var (dx, dy) in AStar.Directions)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (AStar.CanStep(world.Map, cell, dx, dy, unit.Archetype.Locomotor) && world.Reservations.HasRoomFor(next, unit))
                {
                    free.Add(next);
                }
            }

            if (free.Count == 0)
            {
                continue;
            }

            var target = free[world.Random.NextInt(free.Count)].Center;
            UnitOrders.ForgetTarget(unit);
            UnitOrders.GiveRoute(world, unit, [new UnitOrder(target, AttackMove: false)], loop: false, append: false);
            events?.Add(new UnitMoveStarted(world.CurrentTick, unit.Id, target));
        }

        return units.Count > 0;
    }

    /// <summary>The command's living units of its player, each once, in id order.</summary>
    private static List<Unit> OwnUnits(GameWorld world, UnitsCommand command)
    {
        var units = new List<Unit>();
        foreach (var id in command.Units)
        {
            if (world.TryGet(id, out var entity)
                && entity is Unit unit
                && unit.IsAlive
                && unit.Owner == command.Player
                && !units.Contains(unit))
            {
                units.Add(unit);
            }
        }

        units.Sort((a, b) => a.Id.CompareTo(b.Id));
        return units;
    }
}
