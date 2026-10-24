using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// How high a unit is drawn above the ground, in cells (stage 2b): a flier flies at a height of its own — the airship higher than the
/// jets, a soldier with a jet pack lower (stage 3a) — everything else stands on the ground. Only the look: the simulation keeps every
/// unit at its ground point.
/// </summary>
public static class UnitAltitude
{
    public const float Aircraft = 1.5f;
    public const float Airship = 2f;
    public const float Jumpjet = 1f;

    public static float Of(UnitArchetype archetype) =>
        !archetype.Flies ? 0f : archetype.Jumpjet ? Jumpjet : archetype.Type == UnitType.KirovAirship ? Airship : Aircraft;
}
