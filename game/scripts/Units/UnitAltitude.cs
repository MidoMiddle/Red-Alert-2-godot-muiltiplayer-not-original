using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// How high a unit is drawn above the ground, in cells (stage 2b): a flier flies at a height of its own — the airship higher than the
/// jets, a soldier with a jet pack lower (stage 3a) — everything else stands on the ground. Only the look: the simulation keeps every
/// unit at its ground point.
/// <para>
/// The helicopter (the Transport Helicopter, RA2's Night Hawk) sets down when it has nothing to do over open flat ground, and hangs
/// in the air over water, a building, a slope, a bridge, a tree or anything else on its cell, or another unit (the owner,
/// 2026-10-25). <c>UnitRenderer</c> brings it down and up at <see cref="ClimbRate"/> and keeps the height it is drawn at in
/// <see cref="Drawn"/>, so its bars and its click box go down with it.
/// </para>
/// </summary>
public static class UnitAltitude
{
    public const float Aircraft = 1.5f;
    public const float Airship = 2f;
    public const float Jumpjet = 1f;

    /// <summary>Under the water (stage 3e): a little below its face.</summary>
    public const float Submerged = -0.2f;

    /// <summary>A soldier in the water (Tanya, RA2 SpeedType=Amphibious): sunk to the shoulders, the head and arms above it.</summary>
    public const float Swimming = -0.25f;

    /// <summary>Cells a second a helicopter goes down to land or up to take off.</summary>
    public const float ClimbRate = 1.2f;

    private static readonly Dictionary<EntityId, float> DrawnHeights = new();

    public static float Of(UnitArchetype archetype) =>
        archetype.Has(UnitTalents.Submerged) ? Submerged
        : !archetype.Flies ? 0f : archetype.Jumpjet ? Jumpjet : archetype.Type == UnitType.KirovAirship ? Airship : Aircraft;

    /// <summary>The height the unit is drawn at now: a helicopter on its way down or up, else that of its kind.</summary>
    public static float Of(Unit unit) => DrawnHeights.TryGetValue(unit.Id, out var height) ? height : Of(unit.Archetype);

    /// <summary>How much lower than its kind a unit on the ground is drawn: a soldier on water swims (not on a high bridge over it), all else 0.</summary>
    public static float Swim(GameWorld world, Unit unit)
    {
        var cell = unit.Position.ToCell();
        return unit.Archetype.Category == UnitCategory.Infantry && !unit.Archetype.Flies && !unit.OnDeck && world.Map.Contains(cell)
            && world.Map.SurfaceAt(cell) is LandType.Shallow or LandType.Deep ? Swimming : 0f;
    }

    /// <summary>Whether the unit is a helicopter, which lands when idle.</summary>
    public static bool Lands(UnitArchetype archetype) => archetype.Type == UnitType.AllianceNighthawk;

    /// <summary>
    /// The height a helicopter goes to now: the ground when it is idle over a cell it can set down on, else its flying height. Any
    /// other unit: the height of its kind.
    /// </summary>
    public static float Target(GameWorld world, Unit unit)
    {
        var flying = Of(unit.Archetype);
        if (!Lands(unit.Archetype) || unit.MoveTarget is not null || unit.Path is not null || unit.Target is not null || unit.EnterTarget is not null)
        {
            return flying;
        }

        return CanLand(world, unit.Position.ToCell(), unit.Id) ? 0f : flying;
    }

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

    /// <summary>The height a helicopter is drawn at, moved towards <paramref name="target"/> by <see cref="ClimbRate"/> over <paramref name="delta"/> seconds.</summary>
    public static float Step(EntityId id, float flying, float target, float delta)
    {
        var height = DrawnHeights.TryGetValue(id, out var now) ? now : flying;
        var step = ClimbRate * delta;
        height = height < target ? System.MathF.Min(target, height + step) : System.MathF.Max(target, height - step);
        DrawnHeights[id] = height;
        return height;
    }

    /// <summary>Forgets the height of a unit that is gone.</summary>
    public static void Forget(EntityId id) => DrawnHeights.Remove(id);
}
