using System;
using System.Collections.Generic;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Simulation.World;

/// <summary>
/// The unit types of M2, kept in code until the YAML data of M3 (docs/implementation/M2_unit_control.md, 4.11).
/// Roster and ids follow docs/implementation/M3_base_economy.md, 4.6; speed and turn rate are those of the
/// RA2 counterpart from docs/ra2_units, converted by two starting constants that can be tuned in play.
/// </summary>
public static class UnitArchetypes
{
    /// <summary>Leptons per tick for one point of RA2 <c>Speed</c>: a heavy tank (6) drives ~2.1 cells per second.</summary>
    public const int SpeedScale = 6;

    /// <summary>RA2 <c>ROT</c> 5, the value of most vehicles, turns this many <c>BAngle</c> units per tick (≈ 11°).</summary>
    public const int TurnRateForRot5 = 2048;

    private static readonly UnitArchetype[] ByType =
    [
        Infantry(UnitType.Conscript, "conscript"),
        Infantry(UnitType.Rocketeer, "rocketeer"),
        Infantry(UnitType.Engineer, "engineer"),
        Vehicle(UnitType.Mcv, "mcv", Locomotor.Track, ra2Speed: 4, hasTurret: false),
        Vehicle(UnitType.Harvester, "harvester", Locomotor.Track, ra2Speed: 4, hasTurret: true),
        Vehicle(UnitType.SupportVehicle, "support_vehicle", Locomotor.Wheel, ra2Speed: 8, hasTurret: true),
        Vehicle(UnitType.HeavyTank, "heavy_tank", Locomotor.Track, ra2Speed: 6, hasTurret: true),
        Vehicle(UnitType.TeslaTank, "tesla_tank", Locomotor.Track, ra2Speed: 6, hasTurret: true),
    ];

    /// <summary>Every unit type, in <see cref="UnitType"/> order.</summary>
    public static IReadOnlyList<UnitArchetype> All => ByType;

    public static bool IsKnown(UnitType type) => type >= UnitType.Conscript && (int)type <= ByType.Length;

    public static UnitArchetype Get(UnitType type) => IsKnown(type)
        ? ByType[(int)type - 1]
        : throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown unit type.");

    /// <summary>Finds a type by its data id (<c>heavy_tank</c>), as map files and data name it.</summary>
    public static bool TryGetById(string id, out UnitArchetype archetype)
    {
        foreach (var candidate in ByType)
        {
            if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
            {
                archetype = candidate;
                return true;
            }
        }

        archetype = null!;
        return false;
    }

    public static int SpeedFromRa2(int ra2Speed) => ra2Speed * SpeedScale;

    public static int TurnRateFromRa2(int ra2Rot) => (int)IntMath.DivRound((long)ra2Rot * TurnRateForRot5, 5);

    // Standard RA2 infantry: Speed 4, turns instantly (no ROT).
    private static UnitArchetype Infantry(UnitType type, string id) =>
        new(type, id, UnitCategory.Infantry, Locomotor.Foot, UnitPlacement.Subcell, Ra2Speed: 4, Ra2Rot: 0, HasTurret: false);

    // Every vehicle of the roster has ROT 5 in RA2.
    private static UnitArchetype Vehicle(UnitType type, string id, Locomotor locomotor, int ra2Speed, bool hasTurret) =>
        new(type, id, UnitCategory.Vehicle, locomotor, UnitPlacement.Cell, ra2Speed, Ra2Rot: 5, hasTurret);
}
