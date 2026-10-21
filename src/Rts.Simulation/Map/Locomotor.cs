using System;

namespace Rts.Simulation.Map;

/// <summary>
/// How a unit moves over the ground (docs/implementation/M2_unit_control.md, 4.3; docs/implementation/M7_maps.md, 4.3). Decides
/// where it can go and how fast on each land type, see <see cref="LocomotorRules"/>. Values are part of the rules: never
/// renumber. Amphibious, naval and hover units come in M11; the types are in the tables already.
/// </summary>
public enum Locomotor : byte
{
    Foot = 1,
    Track = 2,
    Wheel = 3,
    Amphibious = 4,
    Naval = 5,
    Hover = 6,
}

/// <summary>
/// Speed of each locomotor on each land type and on slopes, in whole percent; 0 means impassable
/// (docs/implementation/M7_maps.md, 4.3). Kept in code until the data files of M3 (terrain.yaml), which move every table at once.
/// </summary>
public static class LocomotorRules
{
    public const int Count = 6;

    // Rows: LandType 1…8; columns: Foot, Track, Wheel, Amphibious, Naval, Hover.
    private static readonly byte[,] Percent =
    {
        { 100, 100, 100, 100, 0, 100 },   // Clear
        { 90, 90, 75, 75, 0, 75 },        // Rough
        { 100, 100, 120, 100, 0, 100 },   // Road: only wheels are faster
        { 90, 80, 70, 100, 0, 100 },      // Beach
        { 0, 0, 0, 100, 0, 100 },         // Shallow: for small ships too, once ships have a size (M11)
        { 0, 0, 0, 100, 100, 100 },       // Deep
        { 0, 0, 0, 0, 0, 0 },             // Rock
        { 80, 90, 90, 100, 0, 100 },      // Ice
    };

    // A slope multiplies the land's speed: foot 80 %, track 75 %, wheel 65 % (plan); amphibious and hover as wheels; ships
    // never meet a slope (water is flat), so 100 %.
    private static readonly byte[] SlopePercent = [80, 75, 65, 65, 100, 65];

    public static bool IsKnown(LandType land) => land >= LandType.Clear && land <= LandType.Ice;

    public static bool IsKnown(Locomotor locomotor) => locomotor >= Locomotor.Foot && locomotor <= Locomotor.Hover;

    /// <summary>Speed multiplier in percent for <paramref name="locomotor"/> on flat <paramref name="land"/>; 0 is impassable.</summary>
    public static int SpeedPercent(Locomotor locomotor, LandType land)
    {
        if (!IsKnown(land))
        {
            throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type.");
        }

        if (!IsKnown(locomotor))
        {
            throw new ArgumentOutOfRangeException(nameof(locomotor), locomotor, "Unknown locomotor.");
        }

        return Percent[(int)land - 1, (int)locomotor - 1];
    }

    /// <summary>The multiplier of a slope for <paramref name="locomotor"/>, in percent.</summary>
    public static int SlopeMultiplier(Locomotor locomotor) =>
        IsKnown(locomotor) ? SlopePercent[(int)locomotor - 1] : throw new ArgumentOutOfRangeException(nameof(locomotor), locomotor, "Unknown locomotor.");

    /// <summary>
    /// Speed in percent on a cell of <paramref name="land"/> and <paramref name="shape"/> (docs/implementation/M7_maps.md, 4.4):
    /// a cliff stops everybody; a slope multiplies the land's speed, rounded to the nearest percent.
    /// </summary>
    public static int CellPercent(Locomotor locomotor, LandType land, CellShape shape)
    {
        var percent = SpeedPercent(locomotor, land);
        return shape switch
        {
            CellShape.Cliff => 0,
            CellShape.Slope => (int)Math.IntMath.DivRound((long)percent * SlopeMultiplier(locomotor), 100),
            _ => percent,
        };
    }

    public static bool IsPassable(Locomotor locomotor, LandType land) => SpeedPercent(locomotor, land) > 0;
}
