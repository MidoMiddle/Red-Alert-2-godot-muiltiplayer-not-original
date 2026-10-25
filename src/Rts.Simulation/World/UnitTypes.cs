using System;
using System.Collections.Generic;

namespace Rts.Simulation.World;

/// <summary>
/// The names of the unit types of M2: the data id of each <see cref="UnitType"/>, as map files and <c>game/data</c> write it. Names
/// only; what a type can do comes from the data, through <see cref="Rules.GameRules.Unit"/>.
/// </summary>
public static class UnitTypes
{
    // In UnitType order; null for a number that was taken out (UnitType 47).
    private static readonly string?[] Ids =
        ["conscript", "rocketeer", "engineer", "mcv", "harvester", "support_vehicle", "heavy_tank", "tesla_tank",
         "alliance_mcv", "alliance_harvester", "alliance_rifleman", "alliance_light_tank",
         "attack_dog", "tesla_trooper", "crazy_ivan", "terrorist", "desolator", "rocket_artillery", "apocalypse_tank",
         "terror_drone", "demolition_truck", "kirov_airship", "hover_transport", "typhoon_sub", "sea_scorpion",
         "giant_squid", "dreadnought", "alliance_engineer", "alliance_attack_dog", "alliance_rocketeer", "alliance_spy",
         "alliance_chrono_legionnaire", "alliance_tanya", "alliance_sniper", "alliance_ifv", "alliance_prism_tank",
         "alliance_mirage_tank", "alliance_tank_destroyer", "alliance_nighthawk", "alliance_harrier",
         "alliance_black_eagle", "alliance_amphibious_transport", "alliance_dolphin", "alliance_destroyer",
         "alliance_aegis_cruiser", "alliance_aircraft_carrier", null, "alliance_hornet",
         "paradrop_plane", "alliance_osprey", "yuri",
         "alliance_chrono_commando", "chrono_ivan", "alliance_psi_commando", "yuri_prime"];

    private static readonly UnitType[] Types =
    [
        UnitType.Conscript, UnitType.Rocketeer, UnitType.Engineer, UnitType.Mcv, UnitType.Harvester, UnitType.SupportVehicle,
        UnitType.HeavyTank, UnitType.TeslaTank, UnitType.AllianceMcv, UnitType.AllianceHarvester, UnitType.AllianceRifleman,
        UnitType.AllianceLightTank,
        UnitType.AttackDog, UnitType.TeslaTrooper, UnitType.CrazyIvan, UnitType.Terrorist, UnitType.Desolator,
        UnitType.RocketArtillery, UnitType.ApocalypseTank, UnitType.TerrorDrone, UnitType.DemolitionTruck, UnitType.KirovAirship,
        UnitType.HoverTransport, UnitType.TyphoonSub, UnitType.SeaScorpion, UnitType.GiantSquid, UnitType.Dreadnought,
        UnitType.AllianceEngineer, UnitType.AllianceAttackDog, UnitType.AllianceRocketeer, UnitType.AllianceSpy,
        UnitType.AllianceChronoLegionnaire, UnitType.AllianceTanya, UnitType.AllianceSniper, UnitType.AllianceIfv,
        UnitType.AlliancePrismTank, UnitType.AllianceMirageTank, UnitType.AllianceTankDestroyer, UnitType.AllianceNighthawk,
        UnitType.AllianceHarrier, UnitType.AllianceBlackEagle, UnitType.AllianceAmphibiousTransport, UnitType.AllianceDolphin,
        UnitType.AllianceDestroyer, UnitType.AllianceAegisCruiser, UnitType.AllianceAircraftCarrier,
        UnitType.AllianceHornet, UnitType.ParadropPlane, UnitType.AllianceOsprey, UnitType.Yuri,
        UnitType.AllianceChronoCommando, UnitType.ChronoIvan, UnitType.AlliancePsiCommando, UnitType.YuriPrime,
    ];

    /// <summary>Every unit type, in <see cref="UnitType"/> order.</summary>
    public static IReadOnlyList<UnitType> All => Types;

    /// <summary>The highest number of a type: tables kept by type number are this long.</summary>
    public static int Highest => Ids.Length;

    public static bool IsKnown(UnitType type) => type >= UnitType.Conscript && (int)type <= Ids.Length && Ids[(int)type - 1] is not null;

    /// <summary>The data id of a type, such as <c>heavy_tank</c>.</summary>
    public static string IdOf(UnitType type) => IsKnown(type)
        ? Ids[(int)type - 1]!
        : throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown unit type.");

    /// <summary>Finds a type by its data id.</summary>
    public static bool TryGetById(string id, out UnitType type)
    {
        for (var i = 0; i < Ids.Length; i++)
        {
            if (string.Equals(Ids[i], id, StringComparison.Ordinal))
            {
                type = (UnitType)(i + 1);
                return true;
            }
        }

        type = default;
        return false;
    }
}
