using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Take-off and landing (rules v102, the owner's wish of 2026-10-26: a flier climbs to its flying height before it sets out). Before the
/// movement. A flier that lands (<see cref="Lands"/>: the helicopter, RA2's Night Hawk; a strike jet of an air force command) is
/// <see cref="Unit.Grounded"/> while it has nothing to do — the helicopter over a cell it can set down on (<see cref="CanLand"/>), a
/// jet on its pad (<see cref="AmmoSystem.OnPad"/>). Given anything to do — a move, a path, a target, a building to enter — it first
/// climbs for <see cref="ClimbTicks"/> (<see cref="Unit.ClimbTicks"/>) and only then moves. A Hornet launched from its carrier climbs off
/// the deck the same way (<see cref="AmmoSystem"/>). Climbing, a unit neither moves nor fires.
/// </summary>
public static class FlightSystem
{
    /// <summary>
    /// The ticks a flier climbs from the ground to its flying height: 1.5 cells at 1.2 cells a second (the height and the rate the client
    /// draws), 1.25 s.
    /// </summary>
    public const int ClimbTicks = 19;

    public static void Update(GameWorld world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit || !Lands(unit.Archetype))
            {
                continue;
            }

            var busy = unit.MoveTarget is not null || unit.Path is not null || unit.Target is not null || unit.EnterTarget is not null;
            if (unit.Grounded && busy)
            {
                (unit.Grounded, unit.ClimbTicks) = (false, ClimbTicks);
            }
            else if (!unit.Grounded && !busy && unit.ClimbTicks == 0
                && (unit.Archetype.Type == UnitType.AllianceNighthawk ? CanLand(world, unit.Position.ToCell(), unit.Id) : AmmoSystem.OnPad(world, unit)))
            {
                unit.Grounded = true;
            }
        }
    }

    /// <summary>Whether a flier lands: the helicopter when idle, a jet of an air force command on its pad (RA2); not a carrier's Hornet.</summary>
    public static bool Lands(UnitArchetype archetype) =>
        archetype.Type == UnitType.AllianceNighthawk || (archetype.Flies && archetype.Ammo > 0 && !archetype.Has(UnitTalents.Spawned));

    /// <summary>
    /// Whether a helicopter can set down on <paramref name="cell"/>: open land — not water, rock or a bridge — on one level, with no
    /// building, no standing map object and no other unit on it.
    /// </summary>
    public static bool CanLand(GameWorld world, CellPos cell, EntityId self)
    {
        var map = world.Map;
        if (!map.Contains(cell) || map.LandAt(cell) is not (LandType.Clear or LandType.Rough or LandType.Road or LandType.Beach or LandType.Track or LandType.Path)
            || map.BridgeAt(cell) is not null || world.BuildingAt(cell) is not null)
        {
            return false;
        }

        var heights = map.Heights;
        var level = heights.CornerLevel(cell.X, cell.Y);
        if (heights.CornerLevel(cell.X + 1, cell.Y) != level || heights.CornerLevel(cell.X, cell.Y + 1) != level
            || heights.CornerLevel(cell.X + 1, cell.Y + 1) != level)
        {
            return false;
        }

        foreach (var mapObject in map.ObjectsAt(cell))
        {
            if (mapObject.IsAlive)
            {
                return false;
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true } other && other.Id != self && !other.Archetype.Flies && other.Position.ToCell() == cell)
            {
                return false;
            }
        }

        return true;
    }
}
