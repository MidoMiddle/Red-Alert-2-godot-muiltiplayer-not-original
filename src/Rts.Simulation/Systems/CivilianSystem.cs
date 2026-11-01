using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The civilians of the map (docs/map_elements/13_civilians, 4): units of nobody with a <see cref="CivilianMind"/>, only in a match with
/// the option (<see cref="Matches.MatchOptions.Civilians"/>). At rest by a gathering point of their place for 10 to 40 s, they stroll to
/// another of their kind within <see cref="WanderCells"/> of where they came out, never near a player's building, on ore or in the
/// water. A shot landing within <see cref="AlarmCells"/>, or a player's vehicle within <see cref="VehicleCells"/>, frightens them
/// (<see cref="Alarm"/>, before the damage): they run half again as fast and frighten those within <see cref="SpreadCells"/> — to a house
/// of the map (<c>shelter</c>) within <see cref="ShelterCells"/> that no soldiers hold, where they go in at the wall and leave the map,
/// or away from the danger, where they crouch for 15 s. They hide without taking a seat of the garrison (<see cref="Building.Sheltered"/>,
/// the owner, 2026-11-01): after 20 s with no shot near the house they come out one a second; soldiers moving in send them all out
/// running; a house that falls kills them. An animal (<see cref="Rules.UnitDef.Animal"/>) runs twice as fast and hides in no house; a
/// parked car has no mind and stands, and blown up leaves its burning wreck on its cell for <see cref="WreckTicks"/> (<see cref="Burn"/>).
/// A stroll keeps off the asphalt (<see cref="Walk"/>): along the sidewalk, crossing a road where it must (the owner, 2026-11-02: no
/// crossings); the beachgoers stroll out onto the piers too. Random draws only in such a match, so every other keeps its states.
/// </summary>
public static class CivilianSystem
{
    /// <summary>How far a landing shot frightens a civilian, in cells.</summary>
    public const int AlarmCells = 5;

    /// <summary>How near a player's vehicle frightens a civilian, in cells.</summary>
    public const int VehicleCells = 2;

    /// <summary>How far a running civilian frightens the others, in cells.</summary>
    public const int SpreadCells = 3;

    /// <summary>How far a frightened civilian looks for a house to hide in, in cells from its footprint.</summary>
    public const int ShelterCells = 6;

    /// <summary>How far a civilian with no house near runs, in cells.</summary>
    public const int RunCells = 7;

    /// <summary>How far from where it came out a civilian strolls, in cells.</summary>
    public const int WanderCells = 8;

    /// <summary>How near a player's building a civilian never goes, in cells.</summary>
    public const int BaseCells = 6;

    /// <summary>The shortest rest, and how much longer a rest may be, in ticks (10 to 40 s).</summary>
    public const int RestTicks = 150;
    public const int RestSpread = 450;

    /// <summary>The ticks a civilian crouches where it ran to (15 s).</summary>
    public const int CoverTicks = 225;

    /// <summary>The ticks of quiet before the civilians come out of a house (20 s), and between two of them (1 s).</summary>
    public const int QuietTicks = 300;
    public const int ExitTicks = 15;

    /// <summary>How long the burning wreck of a parked car holds its cell (4.6): 30 s.</summary>
    public const int WreckTicks = 450;

    // What a step costs a stroll: across, along a diagonal, and on the asphalt — three times as much, so a stroll goes round by the
    // sidewalk unless the road is the only way.
    private const int Step = 10;
    private const int Diagonal = 14;
    private const int OnRoad = 3;

    // How far round the stroll's start and end the way is looked for, in cells; and the longest leg of it: the path between two legs
    // is the movement's own, which likes the asphalt (it is quicker), so the legs are short enough to leave it no room to stray.
    private const int WalkMargin = 4;
    private const int WalkLeg = 3;

    /// <summary>The ticks between two looks for vehicles near, staggered by id, as targets are looked for.</summary>
    private const int LookPeriod = 4;

    /// <summary>Whether the match has civilians.</summary>
    public static bool Active(GameWorld world) => world.Match is { Options.Civilians: true };

    /// <summary>
    /// Before the damage, while the shots that landed this tick are still in <see cref="GameWorld.Hits"/>: the civilians near them, and
    /// near a player's vehicle, are frightened; the houses near them start their quiet again.
    /// </summary>
    public static void Alarm(GameWorld world)
    {
        if (!Active(world))
        {
            return;
        }

        var radius = (long)AlarmCells * WorldPos.LeptonsPerCell;
        foreach (var hit in world.Hits)
        {
            Frighten(world, hit.Point, radius);
            foreach (var entity in world.Entities)
            {
                if (entity is Building { IsAlive: true, Sheltered.Count: > 0 } house
                    && Outside(house, hit.Point.ToCell()) <= AlarmCells)
                {
                    house.ShelterQuiet = 0;
                }
            }
        }

        var near = world.Area;
        var vehicleRadius = (long)VehicleCells * WorldPos.LeptonsPerCell;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, Civilian: { Danger: null, State: not (CivilianState.Flee or CivilianState.Wreck) } mind } civilian
                || (world.CurrentTick + civilian.Id.Value) % LookPeriod != 0)
            {
                continue;
            }

            near.Clear();
            world.Spatial.QueryRadius(world, civilian.Position, vehicleRadius, near);
            foreach (var id in near)
            {
                if (world.TryGet(id, out var found) && found is Unit { IsAlive: true } vehicle && !vehicle.Owner.IsNeutral
                    && vehicle.Archetype.Category == UnitCategory.Vehicle)
                {
                    mind.Danger = vehicle.Position;
                    break;
                }
            }
        }
    }

    /// <summary>After the damage and the garrisons: every civilian acts on its state; the houses let their civilians out.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        if (!Active(world))
        {
            return;
        }

        // By index: a civilian that comes out of a house joins the list and acts from the next tick.
        var entities = world.Entities;
        var count = entities.Count;
        for (var i = 0; i < count; i++)
        {
            if (entities[i] is Unit { IsAlive: true, Civilian: { } mind } civilian)
            {
                Act(world, civilian, mind);
            }
        }

        for (var i = 0; i < count; i++)
        {
            if (entities[i] is Building { IsAlive: true, Sheltered.Count: > 0 } house)
            {
                house.ShelterQuiet++;
                if (house.ShelterQuiet >= QuietTicks && (house.ShelterQuiet - QuietTicks) % ExitTicks == 0)
                {
                    LetOut(world, house, flee: false, events);
                }
            }
        }
    }

    /// <summary>
    /// Soldiers move into the house (<see cref="GarrisonSystem.Enter"/>): every civilian hiding in it comes out and runs, those that find
    /// no free cell around it stay.
    /// </summary>
    internal static void Evict(GameWorld world, Building house, List<SimEvent>? events)
    {
        while (house.Sheltered.Count > 0 && LetOut(world, house, flee: true, events))
        {
        }
    }

    /// <summary>The house falls (<see cref="DeathSystem.Destroy"/>): the civilians hiding in it die with it, as passengers do.</summary>
    internal static void Bury(Building house)
    {
        house.Sheltered.Clear();
        house.ShelterQuiet = 0;
    }

    /// <summary>A civilian of <paramref name="type"/> comes out on <paramref name="cell"/>, at rest for a while.</summary>
    internal static Unit Spawn(GameWorld world, UnitType type, CellPos cell)
    {
        var unit = world.SpawnUnit(PlayerId.Neutral, type, cell.Center, snapToSubcell: true);
        unit.Civilian = new CivilianMind(cell, RestTicks + world.Random.NextInt(RestSpread));
        return unit;
    }

    private static void Act(GameWorld world, Unit civilian, CivilianMind mind)
    {
        if (mind.State == CivilianState.Wreck)
        {
            // Burnt out: the wreck is gone, without a death, and its cell is free at the end of the tick.
            if (--mind.Timer <= 0)
            {
                civilian.MarkDead();
            }

            return;
        }

        if (mind.Danger is { } danger)
        {
            mind.Danger = null;
            if (mind.State == CivilianState.Cover)
            {
                // Under fire in cover, it keeps its head down.
                mind.Timer = CoverTicks;
            }
            else if (mind.State != CivilianState.Flee)
            {
                Run(world, civilian, mind, danger);
            }

            return;
        }

        switch (mind.State)
        {
            case CivilianState.Rest:
                if (--mind.Timer <= 0)
                {
                    Stroll(world, civilian, mind);
                }

                break;
            case CivilianState.Stroll:
                if (Idle(civilian))
                {
                    Rest(world, mind);
                }

                break;
            case CivilianState.Flee:
                Flee(world, civilian, mind);
                break;
            case CivilianState.Cover:
                if (--mind.Timer <= 0)
                {
                    Stroll(world, civilian, mind);
                }

                break;
        }
    }

    // Frightened: half again as fast, to a house near or away; those near it are frightened too. An animal runs twice as fast, away.
    private static void Run(GameWorld world, Unit civilian, CivilianMind mind, WorldPos danger)
    {
        var animal = Animal(world, civilian);
        mind.State = CivilianState.Flee;
        Forget(civilian);
        civilian.Speed = animal ? civilian.Archetype.Speed * 2 : civilian.Archetype.Speed * 3 / 2;
        Frighten(world, civilian.Position, (long)SpreadCells * WorldPos.LeptonsPerCell);
        if (!animal && HouseNear(world, civilian) is { } house)
        {
            mind.Shelter = house.Id;
            UnitOrders.MoveTo(world, civilian, EntrySystem.Door(world, house, civilian));
        }
        else if (Away(world, civilian, danger) is { } away)
        {
            mind.Shelter = null;
            UnitOrders.MoveTo(world, civilian, away);
        }
        else
        {
            TakeCover(civilian, mind);
        }
    }

    private static void Flee(GameWorld world, Unit civilian, CivilianMind mind)
    {
        if (mind.Shelter is { } id)
        {
            if (world.TryGet(id, out var found) && found is Building house && Open(house))
            {
                if (EntrySystem.Outside(house, civilian.Position.ToCell()) <= EntrySystem.DoorReach)
                {
                    Hide(civilian, house);
                    return;
                }

                if (!Idle(civilian))
                {
                    return;
                }
            }

            // The house fell, soldiers took it, or there is no way to it: it runs on, away from it.
            mind.Shelter = null;
            if (found is { } gone && Away(world, civilian, Centre(gone)) is { } away)
            {
                UnitOrders.MoveTo(world, civilian, away);
                return;
            }
        }

        if (Idle(civilian))
        {
            TakeCover(civilian, mind);
        }
    }

    private static void TakeCover(Unit civilian, CivilianMind mind)
    {
        civilian.Speed = civilian.Archetype.Speed;
        mind.State = CivilianState.Cover;
        mind.Shelter = null;
        mind.Timer = CoverTicks;
    }

    private static void Rest(GameWorld world, CivilianMind mind)
    {
        mind.State = CivilianState.Rest;
        mind.Timer = RestTicks + world.Random.NextInt(RestSpread);
    }

    // To a gathering point of its kind within its wander, or a few cells round where it came out; it rests where it stands without one.
    private static void Stroll(GameWorld world, Unit civilian, CivilianMind mind)
    {
        civilian.Speed = civilian.Archetype.Speed;
        var points = new List<CellPos>();
        var def = world.Rules.Database.Units[world.Rules.Database.IndexOfUnit(civilian.Archetype.Id)];
        foreach (var mapObject in world.Map.Objects)
        {
            if (mapObject.IsAlive && def.GathersAt.Contains(mapObject.Type.Id) && Chebyshev(mapObject.Cell, mind.Anchor) <= WanderCells
                && StandBy(world, mapObject.Cell, civilian) is { } spot)
            {
                points.Add(spot);
            }
        }

        // Out onto a pier (docs/beach_elements/14_pier): a standing segment of it within reach.
        foreach (var bridge in world.Map.Bridges)
        {
            if (!def.GathersAt.Contains(bridge.Type.Id))
            {
                continue;
            }

            foreach (var segment in bridge.Segments)
            {
                if (segment.IsIntact && Chebyshev(segment.Cell, mind.Anchor) <= WanderCells && Fits(world, segment.Cell, civilian))
                {
                    points.Add(segment.Cell);
                }
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def: null } building && BuildingTypes.IsKnown(building.Kind)
                && def.GathersAt.Contains(building.Type.Id) && Outside(building, mind.Anchor) <= WanderCells)
            {
                var door = EntrySystem.Door(world, building, civilian).ToCell();
                if (Fits(world, door, civilian))
                {
                    points.Add(door);
                }
            }
        }

        CellPos? target = points.Count > 0 ? points[world.Random.NextInt(points.Count)] : null;
        for (var attempt = 0; target is null && attempt < 4; attempt++)
        {
            var cell = new CellPos(mind.Anchor.X + world.Random.NextInt(-4, 5), mind.Anchor.Y + world.Random.NextInt(-4, 5));
            if (Fits(world, cell, civilian))
            {
                target = cell;
            }
        }

        if (target is { } to && to != civilian.Position.ToCell())
        {
            mind.State = CivilianState.Stroll;
            Walk(world, civilian, to);
        }
        else
        {
            Rest(world, mind);
        }
    }

    // The civilian goes into the house: it leaves the map without a death and waits inside with its hit points.
    private static void Hide(Unit civilian, Building house)
    {
        house.Sheltered.Add(new Occupant(civilian.Archetype, civilian.Hp));
        house.ShelterQuiet = 0;
        civilian.MarkDead();
    }

    // The first civilian inside comes out around the house — running from it when soldiers took it; false when no cell is free.
    private static bool LetOut(GameWorld world, Building house, bool flee, List<SimEvent>? events)
    {
        var occupant = house.Sheltered[0];
        if (ProductionSystem.ExitAround(world, house, world.Rules.Unit(occupant.Type)) is not { } cell)
        {
            return false;
        }

        house.Sheltered.RemoveAt(0);
        var civilian = Spawn(world, occupant.Type, cell);
        world.SetHp(civilian, occupant.Hp, events);
        if (flee)
        {
            civilian.Civilian!.Danger = Centre(house);
        }

        return true;
    }

    // Every civilian within the radius not yet running is frightened by the danger at the point.
    private static void Frighten(GameWorld world, WorldPos point, long radius)
    {
        var near = world.Nearby;
        near.Clear();
        world.Spatial.QueryRadius(world, point, radius, near);
        foreach (var id in near)
        {
            if (world.TryGet(id, out var found) && found is Unit { IsAlive: true, Civilian: { Danger: null, State: not (CivilianState.Flee or CivilianState.Wreck) } mind })
            {
                mind.Danger = point;
            }
        }
    }

    // The nearest house of the map within reach that no soldiers hold; the first of the world on a tie.
    private static Building? HouseNear(GameWorld world, Unit civilian)
    {
        var cell = civilian.Position.ToCell();
        Building? best = null;
        var bestDistance = ShelterCells + 1;
        foreach (var entity in world.Entities)
        {
            if (entity is Building house && Open(house) && Outside(house, cell) is var distance && distance < bestDistance)
            {
                (best, bestDistance) = (house, distance);
            }
        }

        return best;
    }

    // A house of the map civilians can hide in now: standing, nobody's, no soldiers inside.
    private static bool Open(Building house) =>
        house is { IsAlive: true, Def: null, Occupants.Count: 0 } && house.Owner.IsNeutral && BuildingTypes.IsKnown(house.Kind) && house.Type.Shelter;

    // A point about RunCells from the civilian, straight away from the danger, or turned by 45° or 90° to either side; null when none
    // of them is open ground.
    private static WorldPos? Away(GameWorld world, Unit civilian, WorldPos danger)
    {
        var (x, y) = ((long)civilian.Position.X - danger.X, (long)civilian.Position.Y - danger.Y);
        if (x == 0 && y == 0)
        {
            (x, y) = (civilian.Id.Value % 2 == 0 ? 1 : -1, 1);
        }

        var length = IntMath.Sqrt((x * x) + (y * y));
        var reach = (long)RunCells * WorldPos.LeptonsPerCell;
        (x, y) = (x * reach / length, y * reach / length);
        // cos 45° ≈ 181/256.
        (long, long)[] ways = [(x, y), ((x - y) * 181 / 256, (x + y) * 181 / 256), ((x + y) * 181 / 256, (y - x) * 181 / 256), (-y, x), (y, -x)];
        foreach (var (dx, dy) in ways)
        {
            var point = new WorldPos(civilian.Position.X + (int)dx, civilian.Position.Y + (int)dy);
            if (world.Contains(point) && Fits(world, point.ToCell(), civilian))
            {
                return point.ToCell().Center;
            }
        }

        return null;
    }

    // Where to stand by a gathering point: its own cell when a soldier may stand there (a bench, a sun lounger), else the first open cell
    // beside it (a well).
    private static CellPos? StandBy(GameWorld world, CellPos point, Unit civilian)
    {
        for (var ring = 0; ring <= 1; ring++)
        {
            for (var y = point.Y - ring; y <= point.Y + ring; y++)
            {
                for (var x = point.X - ring; x <= point.X + ring; x++)
                {
                    var cell = new CellPos(x, y);
                    if (Chebyshev(cell, point) == ring && Fits(world, cell, civilian))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    // Open ground a civilian may walk to: on the map, enterable, no ore, no water (the deck of a low bridge or a pier is a road),
    // not near a player's building.
    private static bool Fits(GameWorld world, CellPos cell, Unit civilian)
    {
        if (!world.Map.Contains(cell) || !world.Map.CanEnter(cell, civilian.Archetype.Locomotor) || !world.Map.OreAt(cell).IsEmpty
            || world.Map.SurfaceAt(cell) is LandType.Shallow or LandType.Deep)
        {
            return false;
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true } building && !building.Owner.IsNeutral && Outside(building, cell) <= BaseCells)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// After the tick's dead have left (<see cref="GameWorld.RemoveDead"/>): the parked cars blown up this tick leave their burning
    /// wrecks where they stood, turned as they were (4.6).
    /// </summary>
    public static void Burn(GameWorld world)
    {
        foreach (var (type, position, facing) in world.CarWrecks)
        {
            var wreck = world.SpawnUnit(PlayerId.Neutral, type, position);
            wreck.Facing = facing;
            wreck.Civilian = new CivilianMind(position.ToCell(), WreckTicks) { State = CivilianState.Wreck };
        }

        world.CarWrecks.Clear();
    }

    /// <summary>Whether the unit is the burning wreck of a parked car: nothing hurts it.</summary>
    public static bool IsWreck(Entity entity) => entity is Unit { Civilian.State: CivilianState.Wreck };

    // A stroll to the cell, keeping off the asphalt: the cheapest way within the box round its start and end, where a step on a road
    // costs three, as legs from turn to turn, WalkLeg cells at most; straight there when no road is near.
    internal static void Walk(GameWorld world, Unit civilian, CellPos to)
    {
        Forget(civilian);
        var map = world.Map;
        var from = civilian.Position.ToCell();
        var (x0, y0) = (IntMath.Max(0, IntMath.Min(from.X, to.X) - WalkMargin), IntMath.Max(0, IntMath.Min(from.Y, to.Y) - WalkMargin));
        var (x1, y1) = (IntMath.Min(map.Width - 1, IntMath.Max(from.X, to.X) + WalkMargin), IntMath.Min(map.Height - 1, IntMath.Max(from.Y, to.Y) + WalkMargin));
        var (w, h) = (x1 - x0 + 1, y1 - y0 + 1);
        var road = false;
        for (var y = y0; y <= y1 && !road; y++)
        {
            for (var x = x0; x <= x1 && !road; x++)
            {
                road = Asphalt(map, new CellPos(x, y));
            }
        }

        if (!road)
        {
            UnitOrders.MoveTo(world, civilian, to.Center);
            return;
        }

        var cost = new int[w * h];
        var before = new int[w * h];
        System.Array.Fill(cost, int.MaxValue);
        int Index(CellPos cell) => ((cell.Y - y0) * w) + cell.X - x0;
        bool Open(CellPos cell) => cell.X >= x0 && cell.X <= x1 && cell.Y >= y0 && cell.Y <= y1
            && map.CanEnter(cell, civilian.Archetype.Locomotor) && map.SurfaceAt(cell) is not (LandType.Shallow or LandType.Deep);

        // Dijkstra; the queue orders by cost, then by the cell, so equal ways always resolve alike.
        var queue = new PriorityQueue<int, long>();
        cost[Index(from)] = 0;
        before[Index(from)] = -1;
        queue.Enqueue(Index(from), Index(from));
        while (queue.TryDequeue(out var at, out var key))
        {
            if (key / (w * h) != cost[at])
            {
                continue;
            }

            var cell = new CellPos(x0 + (at % w), y0 + (at / w));
            if (cell == to)
            {
                break;
            }

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = new CellPos(cell.X + dx, cell.Y + dy);
                    if ((dx == 0 && dy == 0) || !Open(next)
                        || (dx != 0 && dy != 0 && (!Open(new CellPos(cell.X + dx, cell.Y)) || !Open(new CellPos(cell.X, cell.Y + dy)))))
                    {
                        continue;
                    }

                    var step = (dx != 0 && dy != 0 ? Diagonal : Step) * (Asphalt(map, next) ? OnRoad : 1);
                    var index = Index(next);
                    if (cost[at] + step < cost[index])
                    {
                        cost[index] = cost[at] + step;
                        before[index] = at;
                        queue.Enqueue(index, ((long)cost[index] * w * h) + index);
                    }
                }
            }
        }

        if (cost[Index(to)] == int.MaxValue)
        {
            UnitOrders.MoveTo(world, civilian, to.Center);
            return;
        }

        // The way back from the end, kept at its turns.
        var cells = new List<CellPos>();
        for (var at = Index(to); at >= 0; at = before[at])
        {
            cells.Add(new CellPos(x0 + (at % w), y0 + (at / w)));
        }

        cells.Reverse();
        var legs = System.Collections.Immutable.ImmutableArray.CreateBuilder<UnitOrder>();
        var since = 0;
        for (var i = 1; i < cells.Count; i++)
        {
            var last = i == cells.Count - 1;
            if (last || ++since >= WalkLeg
                || (cells[i + 1].X - cells[i].X, cells[i + 1].Y - cells[i].Y) != (cells[i].X - cells[i - 1].X, cells[i].Y - cells[i - 1].Y))
            {
                legs.Add(new UnitOrder(cells[i].Center, AttackMove: false));
                since = 0;
            }
        }

        if (legs.Count <= 1)
        {
            UnitOrders.MoveTo(world, civilian, to.Center);
            return;
        }

        UnitOrders.GiveRoute(world, civilian, legs.ToImmutable(), loop: false, append: false);
    }

    // The asphalt of a road or a highway, not the deck of a bridge.
    private static bool Asphalt(MapGrid map, CellPos cell) => map.LandAt(cell) is LandType.Road or LandType.Highway;

    // The route of an earlier stroll forgotten, so a new way is not followed by the rest of the old.
    private static void Forget(Unit civilian)
    {
        civilian.Orders = [];
        civilian.OrderIndex = 0;
        civilian.LoopOrders = false;
    }

    private static bool Idle(Unit unit) => unit.MoveTarget is null && !unit.PathPending;

    private static bool Animal(GameWorld world, Unit civilian) =>
        world.Rules.Database.Units[world.Rules.Database.IndexOfUnit(civilian.Archetype.Id)].Animal;

    private static int Outside(Building building, CellPos cell) => EntrySystem.Outside(building, cell);

    private static int Chebyshev(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));

    private static WorldPos Centre(Entity entity) => entity switch
    {
        Building building => Combat.Aim.CentreOf(building),
        Unit unit => unit.Position,
        _ => default,
    };
}
