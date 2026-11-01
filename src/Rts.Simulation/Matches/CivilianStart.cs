using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Matches;

/// <summary>
/// The civilians of the map at the start of a match with the option (docs/map_elements/13_civilians, 5): of the kinds that live in its
/// climate (<see cref="MatchSetup.Climate"/>), up to <see cref="PerHouse"/> out of every house of the map they call home and up to
/// <see cref="PerPoint"/> by every gathering point of theirs, at most <see cref="Most"/> in all, none within <see cref="StartCells"/>
/// cells of a start. Houses and points in an order the simulation's generator shuffles, round by round — one out of each, then a second —
/// so the civilians spread over the map before the most is reached. The animals graze by their points as the people stand by theirs. The
/// parked cars (4.6) take a cell of open ground beside a road near a point of theirs, one by a point, at most <see cref="MostCars"/>.
/// Made by the world as it is built, as the starting units are.
/// </summary>
internal static class CivilianStart
{
    public const int PerHouse = 2;
    public const int PerPoint = 3;
    public const int Most = 40;
    public const int StartCells = 12;
    public const int MostCars = 10;

    /// <summary>How far from the footprint of its point a parked car may stand, in cells.</summary>
    public const int CurbCells = 3;

    public static void Place(GameWorld world, MatchSetup setup)
    {
        if (!setup.Options.Civilians || setup.Climate.Length == 0)
        {
            return;
        }

        var database = world.Rules.Database;
        var kinds = new List<(UnitType Type, Rules.UnitDef Def)>();
        foreach (var type in UnitTypes.All)
        {
            var def = database.Units[database.IndexOfUnit(UnitTypes.IdOf(type))];
            if (def.Civilian && def.Climates.Contains(setup.Climate))
            {
                kinds.Add((type, def));
            }
        }

        if (kinds.Count == 0)
        {
            return;
        }

        // Where they come out: the houses and the gathering points, each with the kinds that live by it.
        var houses = new List<(Building House, List<UnitType> Kinds)>();
        var points = new List<(CellPos Cell, Building? Building, List<UnitType> Kinds)>();
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, Def: null } building || !BuildingTypes.IsKnown(building.Kind)
                || FromStarts(setup, building.Cell) < StartCells)
            {
                continue;
            }

            if (Of(kinds, building.Type.Id, homes: true) is { Count: > 0 } living)
            {
                houses.Add((building, living));
            }

            if (Of(kinds, building.Type.Id, homes: false) is { Count: > 0 } gathering)
            {
                points.Add((building.Cell, building, gathering));
            }
        }

        foreach (var mapObject in world.Map.Objects)
        {
            if (mapObject.IsAlive && FromStarts(setup, mapObject.Cell) >= StartCells
                && Of(kinds, mapObject.Type.Id, homes: false) is { Count: > 0 } gathering)
            {
                points.Add((mapObject.Cell, null, gathering));
            }
        }

        Shuffle(world, houses);
        Shuffle(world, points);
        Park(world, points);
        var placed = 0;
        for (var round = 0; round < PerPoint && placed < Most; round++)
        {
            for (var i = 0; i < houses.Count && round < PerHouse && placed < Most; i++)
            {
                var (house, living) = houses[i];
                var type = living[world.Random.NextInt(living.Count)];
                if (ProductionSystem.ExitAround(world, house, world.Rules.Unit(type)) is { } cell && Open(world, cell))
                {
                    CivilianSystem.Spawn(world, type, cell);
                    placed++;
                }
            }

            for (var i = 0; i < points.Count && placed < Most; i++)
            {
                var (point, building, all) = points[i];
                var gathering = all.FindAll(kind => world.Rules.Unit(kind).Category != UnitCategory.Vehicle);
                if (gathering.Count == 0)
                {
                    continue;
                }

                var type = gathering[world.Random.NextInt(gathering.Count)];
                var archetype = world.Rules.Unit(type);
                var cell = building is not null ? ProductionSystem.ExitAround(world, building, archetype) : Beside(world, point, archetype);
                if (cell is { } at && Open(world, at))
                {
                    CivilianSystem.Spawn(world, type, at);
                    placed++;
                }
            }
        }
    }

    // The parked cars: by every point some car parks by, in the shuffled order, one car on the first open cell beside a road.
    private static void Park(GameWorld world, List<(CellPos Cell, Building? Building, List<UnitType> Kinds)> points)
    {
        var parked = 0;
        foreach (var (point, building, all) in points)
        {
            var cars = all.FindAll(kind => world.Rules.Unit(kind).Category == UnitCategory.Vehicle);
            if (cars.Count == 0)
            {
                continue;
            }

            if (parked >= MostCars)
            {
                return;
            }

            var type = cars[world.Random.NextInt(cars.Count)];
            var (near, far) = building is not null ? (building.Cell, building.FarCell) : (point, point);
            if (Curb(world, near, far, world.Rules.Unit(type)) is { } cell)
            {
                world.SpawnUnit(PlayerId.Neutral, type, cell.Center);
                parked++;
            }
        }
    }

    // The first cell within CurbCells of the footprint, ring by ring, that is open ground a car stands on beside asphalt: not the road.
    private static CellPos? Curb(GameWorld world, CellPos near, CellPos far, UnitArchetype car)
    {
        for (var ring = 1; ring <= CurbCells; ring++)
        {
            for (var y = near.Y - ring; y <= far.Y + ring; y++)
            {
                for (var x = near.X - ring; x <= far.X + ring; x++)
                {
                    var cell = new CellPos(x, y);
                    var outside = IntMath.Max(IntMath.Max(near.X - x, x - far.X), IntMath.Max(near.Y - y, y - far.Y));
                    if (outside == ring && world.Map.Contains(cell) && !Road(world, cell) && Open(world, cell)
                        && world.Map.CanEnter(cell, car.Locomotor) && world.Reservations.HasRoomFor(cell, car.Placement, EntityId.None)
                        && (Road(world, new CellPos(x + 1, y)) || Road(world, new CellPos(x - 1, y)) || Road(world, new CellPos(x, y + 1)) || Road(world, new CellPos(x, y - 1))))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    private static bool Road(GameWorld world, CellPos cell) =>
        world.Map.Contains(cell) && world.Map.LandAt(cell) is Map.LandType.Road or Map.LandType.Highway;

    // The kinds that call the thing of the map home, or gather by it.
    private static List<UnitType> Of(List<(UnitType Type, Rules.UnitDef Def)> kinds, string id, bool homes)
    {
        var found = new List<UnitType>();
        foreach (var (type, def) in kinds)
        {
            if ((homes ? def.Homes : def.GathersAt).Contains(id))
            {
                found.Add(type);
            }
        }

        return found;
    }

    // The point itself when a civilian may stand on it (a bench), else the first free cell beside it (a well).
    private static CellPos? Beside(GameWorld world, CellPos point, UnitArchetype archetype)
    {
        for (var ring = 0; ring <= 1; ring++)
        {
            for (var y = point.Y - ring; y <= point.Y + ring; y++)
            {
                for (var x = point.X - ring; x <= point.X + ring; x++)
                {
                    var cell = new CellPos(x, y);
                    if (IntMath.Max(IntMath.Abs(x - point.X), IntMath.Abs(y - point.Y)) == ring && world.Map.Contains(cell)
                        && world.Map.CanEnter(cell, archetype.Locomotor) && world.Reservations.HasRoomFor(cell, archetype.Placement, EntityId.None))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    // No civilian on ore or in the water.
    private static bool Open(GameWorld world, CellPos cell) =>
        world.Map.OreAt(cell).IsEmpty && world.Map.LandAt(cell) is not (Map.LandType.Shallow or Map.LandType.Deep);

    // The cells to the nearest start of the map, taken or not.
    private static int FromStarts(MatchSetup setup, CellPos cell)
    {
        var nearest = int.MaxValue;
        foreach (var start in setup.MapStarts)
        {
            nearest = IntMath.Min(nearest, IntMath.Max(IntMath.Abs(start.X - cell.X), IntMath.Abs(start.Y - cell.Y)));
        }

        return nearest;
    }

    // Fisher–Yates with the simulation's generator.
    private static void Shuffle<T>(GameWorld world, List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = world.Random.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
