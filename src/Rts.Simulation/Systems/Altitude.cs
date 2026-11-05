using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// How high a unit is over the ground, in leptons (stage 2b; the rules since v129, for the aimed shot: going into a unit, 2026-11-05).
/// A flier flies at a height of its own — the airship higher than the jets, a soldier with a jet pack lower —, a submarine is under the
/// water, a soldier in the water swims, everything else stands on the ground or on the deck of a high bridge. A flier that lands
/// climbs and comes down by its <see cref="Unit.ClimbTicks"/> (<see cref="FlightSystem"/>). The client draws the same heights
/// (<c>UnitAltitude</c>), eased.
/// </summary>
public static class Altitude
{
    public const int Aircraft = 384;
    public const int Airship = 512;
    public const int Jumpjet = 256;

    /// <summary>Under the water (stage 3e): a little below its face.</summary>
    public const int Submerged = -51;

    /// <summary>A soldier in the water (Tanya, RA2 SpeedType=Amphibious): sunk to the shoulders.</summary>
    public const int Swimming = -64;

    /// <summary>How high a jet stands on its pad: on the pad's plate.</summary>
    public const int OnPad = 26;

    /// <summary>How high a carrier's jet starts its climb: on the deck.</summary>
    public const int Deck = 77;

    /// <summary>The height a unit of the kind flies (or sinks) at.</summary>
    public static int Of(UnitArchetype archetype) =>
        archetype.Has(UnitTalents.Submerged) ? Submerged
        : !archetype.Flies ? 0 : archetype.Jumpjet ? Jumpjet : archetype.Type == UnitType.KirovAirship ? Airship : Aircraft;

    /// <summary>
    /// The height the unit is at now over its floor: climbing, the share of the way up from the ground — the pad, the deck — its
    /// <see cref="Unit.ClimbTicks"/> say; set down, the ground or its pad; else that of its kind.
    /// </summary>
    public static int Target(Unit unit)
    {
        var flying = Of(unit.Archetype);
        if (!unit.Archetype.Flies)
        {
            return flying;
        }

        var ground = unit.Archetype.Type == UnitType.AllianceNighthawk ? 0 : unit.Archetype.Has(UnitTalents.Spawned) ? Deck : OnPad;
        if (unit.ClimbTicks > 0)
        {
            return ground + ((flying - ground) * (FlightSystem.ClimbTicks - unit.ClimbTicks) / FlightSystem.ClimbTicks);
        }

        return unit.Grounded ? ground : flying;
    }

    /// <summary>How much lower a unit on the ground is: a soldier on water swims (not on a high bridge over it), all else 0.</summary>
    public static int Swim(GameWorld world, Unit unit)
    {
        var cell = unit.Position.ToCell();
        return unit.Archetype.Category == UnitCategory.Infantry && !unit.Archetype.Flies && !unit.OnDeck && world.Map.Contains(cell)
            && world.Map.SurfaceAt(cell) is LandType.Shallow or LandType.Deep ? Swimming : 0;
    }

    /// <summary>
    /// What the unit stands on, in leptons: the deck of a high bridge for one up there — an end cell of a deck always counts as the deck,
    /// from the ground nobody stands on it —, else the terrain.
    /// </summary>
    public static long Floor(GameWorld world, Unit unit) => Floor(world, unit.Position, unit.OnDeck);

    /// <summary>The floor at <paramref name="position"/>: <see cref="Floor(GameWorld, Unit)"/>.</summary>
    public static long Floor(GameWorld world, WorldPos position, bool onDeck)
    {
        var map = world.Map;
        if (map.DeckCount > 0 && map.DeckSlot(position.ToCell()) is var slot and >= 0 && (onDeck || map.DeckExit(slot) < 0))
        {
            return (long)map.DeckLevel(slot) * CornerHeightGrid.LevelHeight;
        }

        return map.Heights.HeightAt(position).ToIntFloor();
    }

    /// <summary>Where the unit is up and down, in leptons: its floor, its height over it and its swim.</summary>
    public static long Of(GameWorld world, Unit unit) => Floor(world, unit) + Target(unit) + Swim(world, unit);
}
