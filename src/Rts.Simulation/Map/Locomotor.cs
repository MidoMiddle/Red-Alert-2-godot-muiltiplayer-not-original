using System;

namespace Rts.Simulation.Map;

/// <summary>
/// How a unit moves over the ground (docs/implementation/M2_unit_control.md, 4.3). Decides where it can go
/// and how fast on each land type, see <see cref="LocomotorRules"/>. Values are part of the rules: never renumber.
/// </summary>
public enum Locomotor : byte
{
    Foot = 1,
    Track = 2,
    Wheel = 3,
}

/// <summary>
/// Speed of each locomotor on each land type, in whole percent; 0 means impassable
/// (docs/implementation/M7_maps.md, 4.3). Kept in code until the data files of M3/M7 (terrain.yaml).
/// </summary>
public static class LocomotorRules
{
    // Rows: LandType 1…8; columns: Foot, Track, Wheel.
    private static readonly byte[,] Percent =
    {
        { 100, 100, 100 }, // Clear
        { 90, 90, 75 },    // Rough
        { 100, 100, 120 }, // Road: only wheels are faster
        { 90, 80, 70 },    // Beach
        { 0, 0, 0 },       // Shallow: amphibious and naval units come in M11
        { 0, 0, 0 },       // Deep
        { 0, 0, 0 },       // Rock
        { 80, 90, 90 },    // Ice
    };

    public static bool IsKnown(LandType land) => land >= LandType.Clear && land <= LandType.Ice;

    /// <summary>Speed multiplier in percent for <paramref name="locomotor"/> on <paramref name="land"/>; 0 is impassable.</summary>
    public static int SpeedPercent(Locomotor locomotor, LandType land)
    {
        if (!IsKnown(land))
        {
            throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type.");
        }

        if (locomotor < Locomotor.Foot || locomotor > Locomotor.Wheel)
        {
            throw new ArgumentOutOfRangeException(nameof(locomotor), locomotor, "Unknown locomotor.");
        }

        return Percent[(int)land - 1, (int)locomotor - 1];
    }

    public static bool IsPassable(Locomotor locomotor, LandType land) => SpeedPercent(locomotor, land) > 0;
}
