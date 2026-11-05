using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// How high a unit is drawn above the ground, in cells (stage 2b): a flier flies at a height of its own — the airship higher than the
/// jets, a soldier with a jet pack lower (stage 3a) — everything else stands on the ground. The heights are the simulation's
/// (<see cref="Altitude"/>, rules v129: the aimed shot of the player inside a unit meets a flier where it is drawn), in cells here.
/// <para>
/// The helicopter (the Transport Helicopter, RA2's Night Hawk) sets down when it has nothing to do over open flat ground, and hangs
/// in the air over water, a building, a slope, a bridge, a tree or anything else on its cell, or another unit (the owner,
/// 2026-10-25); a jet sets down on its pad. Since rules v102 the simulation decides it (<see cref="FlightSystem"/>) and makes a flier
/// climb before it moves, a carrier's jet too (the owner, 2026-10-26). <c>UnitRenderer</c> brings it down and up at
/// <see cref="ClimbRate"/> and keeps the height it is drawn at in <see cref="Drawn"/>, so its bars and its click box go with it.
/// </para>
/// </summary>
public static class UnitAltitude
{
    private const float Cell = WorldPos.LeptonsPerCell;

    public const float Aircraft = Altitude.Aircraft / Cell;
    public const float Airship = Altitude.Airship / Cell;
    public const float Jumpjet = Altitude.Jumpjet / Cell;

    /// <summary>Under the water (stage 3e): a little below its face.</summary>
    public const float Submerged = Altitude.Submerged / Cell;

    /// <summary>A soldier in the water (Tanya, RA2 SpeedType=Amphibious): sunk to the shoulders, the head and arms above it.</summary>
    public const float Swimming = Altitude.Swimming / Cell;

    /// <summary>Cells a second a helicopter goes down to land or up to take off.</summary>
    public const float ClimbRate = 1.2f;

    private static readonly Dictionary<EntityId, float> DrawnHeights = new();

    public static float Of(UnitArchetype archetype) => Altitude.Of(archetype) / Cell;

    /// <summary>The height the unit is drawn at now: a helicopter on its way down or up, else that of its kind.</summary>
    public static float Of(Unit unit) => DrawnHeights.TryGetValue(unit.Id, out var height) ? height : Of(unit.Archetype);

    /// <summary>How much lower than its kind a unit on the ground is drawn: a soldier on water swims (not on a high bridge over it), all else 0.</summary>
    public static float Swim(GameWorld world, Unit unit) => Altitude.Swim(world, unit) / Cell;

    /// <summary>
    /// Whether the unit's height changes in flight: one that lands (<see cref="FlightSystem.Lands"/>: the helicopter, a jet of an air force
    /// command) or a carrier's jet, which rises off the deck.
    /// </summary>
    public static bool Changes(UnitArchetype archetype) =>
        FlightSystem.Lands(archetype) || (archetype.Flies && archetype.Has(UnitTalents.Spawned));

    /// <summary>How high a jet stands on its pad: on the pad's plate.</summary>
    public const float OnPad = Altitude.OnPad / Cell;

    /// <summary>How high a carrier's jet starts its climb: on the deck.</summary>
    public const float Deck = Altitude.Deck / Cell;

    /// <summary>
    /// The height the unit goes to now, by the simulation (<see cref="FlightSystem"/>, rules v102): climbing, the share of the way up from
    /// the ground — the pad, the deck — its <see cref="Unit.ClimbTicks"/> say; set down, the ground or its pad; else its flying height.
    /// </summary>
    public static float Target(GameWorld world, Unit unit) => Altitude.Target(unit) / Cell;

    /// <summary>
    /// The height a flier that lands is drawn at, moved towards <paramref name="target"/> by <see cref="ClimbRate"/> over
    /// <paramref name="delta"/> seconds; a unit new to the screen starts at its target — a jet launched on the deck, a helicopter out of
    /// the factory where it is.
    /// </summary>
    public static float Step(EntityId id, float target, float delta)
    {
        var height = DrawnHeights.TryGetValue(id, out var now) ? now : target;
        var step = ClimbRate * delta;
        height = height < target ? System.MathF.Min(target, height + step) : System.MathF.Max(target, height - step);
        DrawnHeights[id] = height;
        return height;
    }

    /// <summary>Forgets the height of a unit that is gone.</summary>
    public static void Forget(EntityId id) => DrawnHeights.Remove(id);
}
