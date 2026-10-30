using System;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;

namespace Rts.Simulation.Map;

/// <summary>
/// Speed of each locomotor on each land type and on slopes, in whole percent; 0 means impassable (docs/implementation/M7_maps.md, 4.3).
/// A match takes it from the data files (<c>terrain.yaml</c>, <c>locomotors.yaml</c>, through <see cref="Rules.GameRules"/>); a map
/// outside a match (the editor, the map checks, the generator) uses <see cref="Reference"/>, which a test keeps equal to the data.
/// Immutable; the step costs of the path search are worked out once with it.
/// </summary>
public sealed class LandSpeeds
{
    public const int LocomotorCount = 6;
    public const int LandCount = 11;

    // The ids of the data files, in enum order.
    private static readonly string[] LocomotorIds = ["foot", "track", "wheel", "amphibious", "naval", "hover"];
    private static readonly string[] LandIds = ["clear", "rough", "road", "beach", "shallow", "deep", "rock", "ice", "unpaved", "path", "highway"];

    private readonly byte[] _percent;
    private readonly byte[] _slopePercent;
    private readonly int[] _stepCosts;
    private readonly (int Straight, int Diagonal)[] _minStepCosts;
    private readonly (int Straight, int Diagonal)[] _minStepCostsWithHighway;

    /// <param name="percent">Rows by <see cref="LandType"/> 1…8, columns by <see cref="Locomotor"/> 1…6, each 0…255.</param>
    /// <param name="slopePercent">The multiplier of a slope for each <see cref="Locomotor"/> 1…6, 0…100.</param>
    public LandSpeeds(int[,] percent, int[] slopePercent)
    {
        if (percent.GetLength(0) != LandCount || percent.GetLength(1) != LocomotorCount || slopePercent.Length != LocomotorCount)
        {
            throw new ArgumentException($"Expected {LandCount} × {LocomotorCount} speeds and {LocomotorCount} slopes.");
        }

        _percent = new byte[LandCount * LocomotorCount];
        for (var land = 0; land < LandCount; land++)
        {
            for (var locomotor = 0; locomotor < LocomotorCount; locomotor++)
            {
                var value = percent[land, locomotor];
                if (value is < 0 or > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(percent), value, "A speed must be 0 to 255 percent.");
                }

                _percent[land * LocomotorCount + locomotor] = (byte)value;
            }
        }

        _slopePercent = new byte[LocomotorCount];
        for (var locomotor = 0; locomotor < LocomotorCount; locomotor++)
        {
            if (slopePercent[locomotor] is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(slopePercent), slopePercent[locomotor], "A slope must be 0 to 100 percent.");
            }

            _slopePercent[locomotor] = (byte)slopePercent[locomotor];
        }

        _stepCosts = BuildStepCosts();
        _minStepCosts = BuildMinStepCosts(highway: false);
        _minStepCostsWithHighway = BuildMinStepCosts(highway: true);
    }

    /// <summary>
    /// The table of M7 (docs/implementation/M7_maps.md, 4.3), equal to <c>game/data</c> (a test checks it). Maps that are not in a
    /// match use it.
    /// </summary>
    public static LandSpeeds Reference { get; } = new(
        new[,]
        {
            // Foot, Track, Wheel, Amphibious, Naval, Hover.
            { 100, 100, 100, 100, 0, 100 },   // Clear
            { 90, 90, 75, 75, 0, 75 },        // Rough
            { 100, 100, 120, 100, 0, 100 },   // Road: only wheels are faster
            { 90, 80, 70, 100, 0, 100 },      // Beach
            { 0, 0, 0, 100, 0, 100 },         // Shallow: for small ships too, once ships have a size (M11)
            { 0, 0, 0, 100, 100, 100 },       // Deep
            { 0, 0, 0, 0, 0, 0 },             // Rock
            { 80, 90, 90, 100, 0, 100 },      // Ice
            { 100, 100, 110, 100, 0, 100 },   // Track: gravel, dirt and packed-snow roads; wheels only
            { 100, 100, 100, 100, 0, 100 },   // Path: footpaths and trails; the foot bonus waits for its own version step
            { 100, 115, 130, 100, 0, 100 },   // Highway: wheels most, tracks less (docs/map_elements/04_roads)
        },
        // Foot 80 %, track 75 %, wheel 65 %; amphibious and hover as wheels; ships never meet a slope (water is flat).
        [80, 75, 65, 65, 100, 65]);

    public static bool IsKnown(LandType land) => land >= LandType.Clear && land <= LandType.Highway;

    public static bool IsKnown(Locomotor locomotor) => locomotor >= Locomotor.Foot && locomotor <= Locomotor.Hover;

    /// <summary>The id of the locomotor in the data files, such as <c>track</c>.</summary>
    public static string IdOf(Locomotor locomotor) => IsKnown(locomotor)
        ? LocomotorIds[(int)locomotor - 1]
        : throw new ArgumentOutOfRangeException(nameof(locomotor), locomotor, "Unknown locomotor.");

    /// <summary>The id of the land type in the data files, such as <c>shallow</c>.</summary>
    public static string IdOf(LandType land) => IsKnown(land)
        ? LandIds[(int)land - 1]
        : throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type.");

    /// <summary>Speed multiplier in percent for <paramref name="locomotor"/> on flat <paramref name="land"/>; 0 is impassable.</summary>
    public int SpeedPercent(Locomotor locomotor, LandType land) => _percent[Index(land, locomotor)];

    /// <summary>The multiplier of a slope for <paramref name="locomotor"/>, in percent.</summary>
    public int SlopeMultiplier(Locomotor locomotor) => IsKnown(locomotor)
        ? _slopePercent[(int)locomotor - 1]
        : throw new ArgumentOutOfRangeException(nameof(locomotor), locomotor, "Unknown locomotor.");

    /// <summary>
    /// Speed in percent on a cell of <paramref name="land"/> and <paramref name="shape"/> (docs/implementation/M7_maps.md, 4.4):
    /// a cliff stops everybody; a slope multiplies the land's speed, rounded to the nearest percent.
    /// </summary>
    public int CellPercent(Locomotor locomotor, LandType land, CellShape shape)
    {
        var percent = SpeedPercent(locomotor, land);
        return shape switch
        {
            CellShape.Cliff => 0,
            CellShape.Slope => (int)IntMath.DivRound((long)percent * SlopeMultiplier(locomotor), 100),
            _ => percent,
        };
    }

    public bool IsPassable(Locomotor locomotor, LandType land) => SpeedPercent(locomotor, land) > 0;

    /// <summary>Path cost of entering flat <paramref name="land"/>, or 0 when the locomotor cannot (docs/implementation/M2_unit_control.md, 4.4).</summary>
    public int StepCost(Locomotor locomotor, LandType land, bool diagonal) => _stepCosts[Index(land, locomotor) * 2 + (diagonal ? 1 : 0)];

    /// <summary>
    /// The cheapest straight and diagonal step of the locomotor on any land: the base of the path search heuristic. The highway counts
    /// only on a map that has one (<paramref name="highway"/>): it is the cheapest land of all, and on every other map it would only
    /// weaken the heuristic, so the search would open more nodes than before for nothing (docs/map_elements/04_roads).
    /// </summary>
    public (int Straight, int Diagonal) MinStepCosts(Locomotor locomotor, bool highway = false) => IsKnown(locomotor)
        ? (highway ? _minStepCostsWithHighway : _minStepCosts)[(int)locomotor - 1]
        : throw new ArgumentOutOfRangeException(nameof(locomotor), locomotor, "Unknown locomotor.");

    /// <summary>True when both tables hold the same speeds and slopes.</summary>
    public bool ContentEquals(LandSpeeds other) =>
        _percent.AsSpan().SequenceEqual(other._percent) && _slopePercent.AsSpan().SequenceEqual(other._slopePercent);

    private static int Index(LandType land, Locomotor locomotor)
    {
        if (!IsKnown(land))
        {
            throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type.");
        }

        if (!IsKnown(locomotor))
        {
            throw new ArgumentOutOfRangeException(nameof(locomotor), locomotor, "Unknown locomotor.");
        }

        return ((int)land - 1) * LocomotorCount + (int)locomotor - 1;
    }

    private int[] BuildStepCosts()
    {
        var costs = new int[LandCount * LocomotorCount * 2];
        for (var i = 0; i < _percent.Length; i++)
        {
            var percent = _percent[i];
            costs[i * 2] = percent == 0 ? 0 : (int)IntMath.DivRound(AStar.StraightCost * 100L, percent);
            costs[i * 2 + 1] = percent == 0 ? 0 : (int)IntMath.DivRound(AStar.DiagonalCost * 100L, percent);
        }

        return costs;
    }

    private (int, int)[] BuildMinStepCosts(bool highway)
    {
        var result = new (int, int)[LocomotorCount];
        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            var straight = int.MaxValue;
            var diagonal = int.MaxValue;
            for (var land = LandType.Clear; land <= (highway ? LandType.Highway : LandType.Path); land++)
            {
                var cost = StepCost(locomotor, land, diagonal: false);
                if (cost > 0)
                {
                    straight = IntMath.Min(straight, cost);
                    diagonal = IntMath.Min(diagonal, StepCost(locomotor, land, diagonal: true));
                }
            }

            result[(int)locomotor - 1] = (straight, diagonal);
        }

        return result;
    }
}
