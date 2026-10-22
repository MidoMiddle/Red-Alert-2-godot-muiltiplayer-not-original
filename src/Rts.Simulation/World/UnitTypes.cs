using System;
using System.Collections.Generic;

namespace Rts.Simulation.World;

/// <summary>
/// The names of the unit types of M2: the data id of each <see cref="UnitType"/>, as map files and <c>game/data</c> write it. Names
/// only; what a type can do comes from the data, through <see cref="Rules.GameRules.Unit"/>.
/// </summary>
public static class UnitTypes
{
    // In UnitType order.
    private static readonly string[] Ids =
        ["conscript", "rocketeer", "engineer", "mcv", "harvester", "support_vehicle", "heavy_tank", "tesla_tank"];

    private static readonly UnitType[] Types =
    [
        UnitType.Conscript, UnitType.Rocketeer, UnitType.Engineer, UnitType.Mcv, UnitType.Harvester, UnitType.SupportVehicle,
        UnitType.HeavyTank, UnitType.TeslaTank,
    ];

    /// <summary>Every unit type, in <see cref="UnitType"/> order.</summary>
    public static IReadOnlyList<UnitType> All => Types;

    public static bool IsKnown(UnitType type) => type >= UnitType.Conscript && (int)type <= Ids.Length;

    /// <summary>The data id of a type, such as <c>heavy_tank</c>.</summary>
    public static string IdOf(UnitType type) => IsKnown(type)
        ? Ids[(int)type - 1]
        : throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown unit type.");

    /// <summary>Finds a type by its data id.</summary>
    public static bool TryGetById(string id, out UnitType type)
    {
        for (var i = 0; i < Ids.Length; i++)
        {
            if (string.Equals(Ids[i], id, StringComparison.Ordinal))
            {
                type = Types[i];
                return true;
            }
        }

        type = default;
        return false;
    }
}
