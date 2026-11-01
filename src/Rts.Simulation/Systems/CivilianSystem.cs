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
/// parked car has no mind and stands. Random draws only in such a match, so every other keeps its states.
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
            if (entity is not Unit { IsAlive: true, Civilian: { Danger: null, State: not CivilianState.Flee } mind } civilian
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
            UnitOrders.MoveTo(world, civilian, to.Center);
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
            if (world.TryGet(id, out var found) && found is Unit { IsAlive: true, Civilian: { Danger: null, State: not CivilianState.Flee } mind })
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

    // Open ground a civilian may walk to: on the map, enterable, no ore, no water, not near a player's building.
    private static bool Fits(GameWorld world, CellPos cell, Unit civilian)
    {
        if (!world.Map.Contains(cell) || !world.Map.CanEnter(cell, civilian.Archetype.Locomotor) || !world.Map.OreAt(cell).IsEmpty
            || world.Map.LandAt(cell) is LandType.Shallow or LandType.Deep)
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
