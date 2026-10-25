using System.Collections.Generic;

namespace Rts.Simulation.World;

/// <summary>Kinds of buildings. Values are part of the rules (replays, hash): never renumber.</summary>
public enum BuildingKind : byte
{
    NeutralShed = 1,
    NeutralHouseA = 2,
    NeutralApartment = 3,
    TechOilDerrick = 4,
    TechHospital = 5,
    TechAirfield = 6,
    NeutralFuelTank = 7,
    NeutralSmokestack = 8,
    NeutralCoolingTower = 9,
    NeutralFactoryHall = 10,
    NeutralBoilerHouse = 11,
    TechBridgeRepairHut = 12,

    // The buildings of the element documents (docs/*_elements), 2026-10-25: town, beach, desert, harbour, farm.
    NeutralSchool = 13,
    NeutralOffice = 14,
    NeutralChurch = 15,
    NeutralShop = 16,
    NeutralKiosk = 17,
    NeutralSkyscraper = 18,
    NeutralCafe = 19,
    NeutralBank = 20,
    NeutralHotel = 21,
    NeutralFireStation = 22,
    NeutralAutoRepair = 23,
    NeutralFountain = 24,
    NeutralBeachCabin = 25,
    NeutralBeachBar = 26,
    NeutralLifeguardTower = 27,
    NeutralLighthouse = 28,
    NeutralBoatShed = 29,
    NeutralAdobeHouse = 30,
    NeutralAncientRuins = 31,
    NeutralRockMesa = 32,
    NeutralRockArch = 33,
    NeutralPortCrane = 34,
    NeutralContainerStack = 35,
    NeutralPortWarehouse = 36,
    NeutralWaterTower = 37,
    NeutralFarmhouse = 38,
    NeutralBarn = 39,
    NeutralSilo = 40,
    NeutralWindmill = 41,

    // The last two tech buildings of M11 a5 (2026-10-25).
    TechMachineShop = 42,
    TechPowerPlant = 43,
}

/// <summary>
/// What a kind of building is (docs/implementation/M7_maps.md, 4.8): its footprint of <see cref="Width"/> × <see cref="Height"/>
/// cells from the north-west corner, its hit points, how many soldiers it would hold and whether an engineer would take it.
/// An engineer takes one that is <see cref="Capturable"/> (stage 1 of the loose ends); a tech building taken gives its owner what its
/// kind gives (<see cref="Systems.TechBuildingSystem"/>), the airfield its <see cref="Superweapon"/> every <see cref="Recharge"/> ticks.
/// Garrisons are M11: the field is here, the logic is not.
/// </summary>
public sealed record BuildingType(
    BuildingKind Kind,
    string Id,
    int Width,
    int Height,
    int MaxHp,
    int Garrison,
    bool Capturable,
    Rules.SuperweaponKind Superweapon = Rules.SuperweaponKind.None,
    int Recharge = 0);

/// <summary>
/// The neutral buildings and tech buildings of the rules so far — winter and industrial sets, and the town, beach, desert, harbour and
/// farm buildings of the element documents (docs/city_elements … docs/temperate_elements) — kept in code until the data files of
/// M3 (game/data/neutral/).
/// Hit points are a rough guide until the balance of M4.
/// </summary>
public static class BuildingTypes
{
    private static readonly BuildingType[] Types =
    [
        new(BuildingKind.NeutralShed, "neutral_shed", 1, 1, MaxHp: 300, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralHouseA, "neutral_house_a", 2, 2, MaxHp: 800, Garrison: 3, Capturable: false),
        new(BuildingKind.NeutralApartment, "neutral_apartment", 3, 3, MaxHp: 1500, Garrison: 10, Capturable: false),
        new(BuildingKind.TechOilDerrick, "tech_oil_derrick", 2, 2, MaxHp: 1000, Garrison: 0, Capturable: true),
        new(BuildingKind.TechHospital, "tech_hospital", 3, 3, MaxHp: 1500, Garrison: 0, Capturable: true),
        // RA2 ParaDropSpecial: a paradrop every 4 minutes.
        new(BuildingKind.TechAirfield, "tech_airfield", 4, 3, MaxHp: 2000, Garrison: 0, Capturable: true, Rules.SuperweaponKind.Paradrop, 240 * SimulationSettings.DefaultTicksPerSecond),
        new(BuildingKind.NeutralFuelTank, "neutral_fuel_tank", 1, 1, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralSmokestack, "neutral_smokestack", 1, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralCoolingTower, "neutral_cooling_tower", 2, 2, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralFactoryHall, "neutral_factory_hall", 4, 3, MaxHp: 800, Garrison: 6, Capturable: false),
        new(BuildingKind.NeutralBoilerHouse, "neutral_boiler_house", 2, 2, MaxHp: 800, Garrison: 2, Capturable: false),
        // RA2 bridge repair hut: cannot be hurt (Immune) nor taken; an engineer going in mends its bridge.
        new(BuildingKind.TechBridgeRepairHut, "tech_bridge_repair_hut", 1, 1, MaxHp: 2000, Garrison: 0, Capturable: false),

        // The element documents (2026-10-25): footprints, hit points by the class of the document (low ≈ 300, medium ≈ 800, high
        // ≈ 1500 and more), garrisons where the document has one; nothing is taken by an engineer.
        new(BuildingKind.NeutralSchool, "neutral_school", 3, 2, MaxHp: 1500, Garrison: 8, Capturable: false),
        new(BuildingKind.NeutralOffice, "neutral_office", 2, 2, MaxHp: 1500, Garrison: 6, Capturable: false),
        new(BuildingKind.NeutralChurch, "neutral_church", 2, 3, MaxHp: 1000, Garrison: 5, Capturable: false),
        new(BuildingKind.NeutralShop, "neutral_shop", 2, 2, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralKiosk, "neutral_kiosk", 1, 1, MaxHp: 200, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralSkyscraper, "neutral_skyscraper", 2, 2, MaxHp: 2500, Garrison: 10, Capturable: false),
        new(BuildingKind.NeutralCafe, "neutral_cafe", 2, 1, MaxHp: 600, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralBank, "neutral_bank", 3, 2, MaxHp: 2000, Garrison: 6, Capturable: false),
        new(BuildingKind.NeutralHotel, "neutral_hotel", 3, 2, MaxHp: 1800, Garrison: 8, Capturable: false),
        new(BuildingKind.NeutralFireStation, "neutral_fire_station", 3, 2, MaxHp: 1500, Garrison: 6, Capturable: false),
        new(BuildingKind.NeutralAutoRepair, "neutral_auto_repair", 2, 2, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralFountain, "neutral_fountain", 2, 2, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralBeachCabin, "neutral_beach_cabin", 1, 1, MaxHp: 300, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralBeachBar, "neutral_beach_bar", 3, 2, MaxHp: 600, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralLifeguardTower, "neutral_lifeguard_tower", 2, 2, MaxHp: 500, Garrison: 2, Capturable: false),
        new(BuildingKind.NeutralLighthouse, "neutral_lighthouse", 2, 2, MaxHp: 2000, Garrison: 4, Capturable: false),
        new(BuildingKind.NeutralBoatShed, "neutral_boat_shed", 2, 2, MaxHp: 600, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralAdobeHouse, "neutral_adobe_house", 2, 2, MaxHp: 600, Garrison: 2, Capturable: false),
        new(BuildingKind.NeutralAncientRuins, "neutral_ancient_ruins", 2, 2, MaxHp: 2500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralRockMesa, "neutral_rock_mesa", 2, 2, MaxHp: 4000, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralRockArch, "neutral_rock_arch", 2, 1, MaxHp: 3000, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralPortCrane, "neutral_port_crane", 1, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralContainerStack, "neutral_container_stack", 2, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralPortWarehouse, "neutral_port_warehouse", 3, 3, MaxHp: 1200, Garrison: 4, Capturable: false),
        new(BuildingKind.NeutralWaterTower, "neutral_water_tower", 1, 1, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralFarmhouse, "neutral_farmhouse", 2, 2, MaxHp: 700, Garrison: 3, Capturable: false),
        new(BuildingKind.NeutralBarn, "neutral_barn", 3, 2, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralSilo, "neutral_silo", 1, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralWindmill, "neutral_windmill", 2, 2, MaxHp: 700, Garrison: 0, Capturable: false),

        // M11 a5 (docs/map_elements/12_tech_buildings/README.md): the machine shop mends the owner's vehicles, the power plant
        // powers its base (TechBuildingSystem). The footprint and the hit points of the hospital.
        new(BuildingKind.TechMachineShop, "tech_machine_shop", 3, 3, MaxHp: 1500, Garrison: 0, Capturable: true),
        new(BuildingKind.TechPowerPlant, "tech_power_plant", 3, 3, MaxHp: 1500, Garrison: 0, Capturable: true),
    ];

    public static IReadOnlyList<BuildingType> All => Types;

    public static bool IsKnown(BuildingKind kind) => kind >= BuildingKind.NeutralShed && (int)kind <= Types.Length;

    public static BuildingType Get(BuildingKind kind) =>
        IsKnown(kind) ? Types[(int)kind - 1] : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building kind.");

    /// <summary>The type of a map file id such as <c>neutral_house_a</c>.</summary>
    public static bool TryGetById(string id, out BuildingType type)
    {
        foreach (var candidate in Types)
        {
            if (candidate.Id == id)
            {
                type = candidate;
                return true;
            }
        }

        type = null!;
        return false;
    }
}
