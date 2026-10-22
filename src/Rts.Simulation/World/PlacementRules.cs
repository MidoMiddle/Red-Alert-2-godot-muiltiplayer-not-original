using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;

namespace Rts.Simulation.World;

/// <summary>Why a building cannot stand where it was asked to; <see cref="None"/> when it can. Values go into events: never renumber.</summary>
public enum PlacementProblem : byte
{
    None = 0,

    /// <summary>A cell of the footprint is off the map.</summary>
    OutsideMap = 1,

    /// <summary>A cell is land that takes no building (<c>buildable: false</c> in the data): water, rock, ice.</summary>
    NotBuildable = 2,

    /// <summary>A cell is a slope or a cliff, or lies at another level than the rest.</summary>
    NotFlat = 3,

    /// <summary>A cell holds a map object, an obstacle, a bridge or a building.</summary>
    Taken = 4,

    /// <summary>A unit stands in a cell or is on its way into it.</summary>
    Units = 5,

    /// <summary>No cell of the footprint is within <c>adjacent</c> cells of a building of the player that extends the base.</summary>
    TooFar = 6,

    /// <summary>The player may not build this yet: the prerequisites are missing (checked by the command, not here).</summary>
    NotAvailable = 7,

    /// <summary>The building has not been produced yet: it is not ready in its queue (checked by the command, not here).</summary>
    NotReady = 8,
}

/// <summary>
/// Where a building of a player may stand (docs/implementation/M3_base_economy.md, 4.8). One function for the simulation, which
/// decides, and the client, which shows the footprint in red or green: they cannot disagree. Ore under the footprint joins with
/// ore in the match (step 9).
/// </summary>
public static class PlacementRules
{
    /// <param name="cell">The north-west cell of the footprint.</param>
    /// <param name="needsBase">False for the construction yard of an MCV, which starts a base where there is none.</param>
    /// <param name="ignoreUnit">A unit that does not stand in the way: the MCV that turns into the building.</param>
    /// <returns>The first problem found: the footprint, then each cell in order (taken, land, level, units), then the base.</returns>
    public static PlacementProblem Check(
        GameWorld world, PlayerId player, BuildingDef def, CellPos cell, bool needsBase = true, EntityId ignoreUnit = default)
    {
        var map = world.Map;
        var far = new CellPos(cell.X + def.Footprint.Width - 1, cell.Y + def.Footprint.Height - 1);
        if (!map.Contains(cell) || !map.Contains(far))
        {
            return PlacementProblem.OutsideMap;
        }

        var level = map.Heights.CellLevel(cell);
        for (var y = cell.Y; y <= far.Y; y++)
        {
            for (var x = cell.X; x <= far.X; x++)
            {
                var problem = CellProblem(world, new CellPos(x, y), level, ignoreUnit);
                if (problem != PlacementProblem.None)
                {
                    return problem;
                }
            }
        }

        return needsBase && !NearBase(world, player, def, cell, far) ? PlacementProblem.TooFar : PlacementProblem.None;
    }

    private static PlacementProblem CellProblem(GameWorld world, CellPos cell, int level, EntityId ignoreUnit)
    {
        var map = world.Map;
        if (map.IsBlocked(cell) || map.HasBuilding(cell) || map.BridgeAt(cell) is not null || HasStandingObject(map, cell))
        {
            return PlacementProblem.Taken;
        }

        if (!world.Rules.IsBuildable(map.LandAt(cell)))
        {
            return PlacementProblem.NotBuildable;
        }

        if (map.Heights.ShapeAt(cell) != CellShape.Flat || map.Heights.CellLevel(cell) != level)
        {
            return PlacementProblem.NotFlat;
        }

        var reservations = world.Reservations;
        var onlyIgnored = reservations.InfantryCount(cell) == 0 && reservations.VehicleAt(cell) == ignoreUnit;
        return reservations.IsFree(cell) || (ignoreUnit != default && onlyIgnored) ? PlacementProblem.None : PlacementProblem.Units;
    }

    private static bool HasStandingObject(MapGrid map, CellPos cell)
    {
        foreach (var mapObject in map.ObjectsAt(cell))
        {
            if (mapObject.IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when at most <c>adjacent</c> cells lie between the footprint and the footprint of a standing building of the player that
    /// extends the base, ready or growing (Chebyshev: diagonals count as one).
    /// </summary>
    private static bool NearBase(GameWorld world, PlayerId player, BuildingDef def, CellPos cell, CellPos far)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def.ExtendsBase: true } building && building.Owner == player)
            {
                var gapX = IntMath.Max(0, IntMath.Max(building.Cell.X - far.X, cell.X - building.FarCell.X) - 1);
                var gapY = IntMath.Max(0, IntMath.Max(building.Cell.Y - far.Y, cell.Y - building.FarCell.Y) - 1);
                if (IntMath.Max(gapX, gapY) <= def.Adjacent)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
