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

    // 42 and 43 were the machine shop and the power plant of Yuri's Revenge (M11 a5), taken out on 2026-10-25 to keep to RA2: never
    // reuse the numbers.

    // The outpost of RA2 (CAOUTP), 2026-10-25: a repair pad with a missile turret that extends the base of its owner.
    TechOutpost = 44,

    // The civilian bunker of RA2's maps (CABUNK01, CABUNK02), 2026-10-27: eight soldiers behind steel.
    NeutralCivilianBunker = 45,
}

/// <summary>
/// What a kind of building of the map is (docs/implementation/M7_maps.md, 4.8), as the data defines it (<see cref="Rules.MapBuildingDef"/>;
/// M3 step 14a): its footprint of <see cref="Width"/> × <see cref="Height"/>
/// cells from the north-west corner, its hit points, how many soldiers it would hold and whether an engineer would take it.
/// An engineer takes one that is <see cref="Capturable"/> (stage 1 of the loose ends); a tech building taken gives its owner what its
/// kind gives (<see cref="Systems.TechBuildingSystem"/>), the airfield its <see cref="Superweapon"/> every <see cref="Recharge"/> ticks.
/// Taken, the outpost of RA2 fires its <see cref="Weapon"/> (a weapon of the data, by id) at its owner's enemies, has the
/// <see cref="Armor"/> of the data, sees <see cref="Sight"/> cells, mends its owner's vehicles beside it and extends its owner's base.
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
    int Recharge = 0,
    string Weapon = "",
    string Armor = "",
    int Sight = 0,
    bool RepairsVehicles = false,
    bool ExtendsBase = false,
    string DeathWeapon = "");

/// <summary>
/// The kinds of buildings of the map the engine knows (docs/implementation/M3_base_economy.md, step 14a): each number with its data id.
/// What a kind is — footprint, hit points, garrison, what a tech building gives — is the data's (<c>game/data/neutral/</c>), held by
/// <see cref="Map.MapTypes"/>; the engine gives some kinds their own behaviour by number (the oil derrick, the hospital, the bridge
/// repair hut).
/// </summary>
public static class BuildingTypes
{
    private static readonly SortedDictionary<BuildingKind, string> Ids = new()
    {
        [BuildingKind.NeutralShed] = "neutral_shed",
        [BuildingKind.NeutralHouseA] = "neutral_house_a",
        [BuildingKind.NeutralApartment] = "neutral_apartment",
        [BuildingKind.TechOilDerrick] = "tech_oil_derrick",
        [BuildingKind.TechHospital] = "tech_hospital",
        [BuildingKind.TechAirfield] = "tech_airfield",
        [BuildingKind.NeutralFuelTank] = "neutral_fuel_tank",
        [BuildingKind.NeutralSmokestack] = "neutral_smokestack",
        [BuildingKind.NeutralCoolingTower] = "neutral_cooling_tower",
        [BuildingKind.NeutralFactoryHall] = "neutral_factory_hall",
        [BuildingKind.NeutralBoilerHouse] = "neutral_boiler_house",
        [BuildingKind.TechBridgeRepairHut] = "tech_bridge_repair_hut",
        [BuildingKind.NeutralSchool] = "neutral_school",
        [BuildingKind.NeutralOffice] = "neutral_office",
        [BuildingKind.NeutralChurch] = "neutral_church",
        [BuildingKind.NeutralShop] = "neutral_shop",
        [BuildingKind.NeutralKiosk] = "neutral_kiosk",
        [BuildingKind.NeutralSkyscraper] = "neutral_skyscraper",
        [BuildingKind.NeutralCafe] = "neutral_cafe",
        [BuildingKind.NeutralBank] = "neutral_bank",
        [BuildingKind.NeutralHotel] = "neutral_hotel",
        [BuildingKind.NeutralFireStation] = "neutral_fire_station",
        [BuildingKind.NeutralAutoRepair] = "neutral_auto_repair",
        [BuildingKind.NeutralFountain] = "neutral_fountain",
        [BuildingKind.NeutralBeachCabin] = "neutral_beach_cabin",
        [BuildingKind.NeutralBeachBar] = "neutral_beach_bar",
        [BuildingKind.NeutralLifeguardTower] = "neutral_lifeguard_tower",
        [BuildingKind.NeutralLighthouse] = "neutral_lighthouse",
        [BuildingKind.NeutralBoatShed] = "neutral_boat_shed",
        [BuildingKind.NeutralAdobeHouse] = "neutral_adobe_house",
        [BuildingKind.NeutralAncientRuins] = "neutral_ancient_ruins",
        [BuildingKind.NeutralRockMesa] = "neutral_rock_mesa",
        [BuildingKind.NeutralRockArch] = "neutral_rock_arch",
        [BuildingKind.NeutralPortCrane] = "neutral_port_crane",
        [BuildingKind.NeutralContainerStack] = "neutral_container_stack",
        [BuildingKind.NeutralPortWarehouse] = "neutral_port_warehouse",
        [BuildingKind.NeutralWaterTower] = "neutral_water_tower",
        [BuildingKind.NeutralFarmhouse] = "neutral_farmhouse",
        [BuildingKind.NeutralBarn] = "neutral_barn",
        [BuildingKind.NeutralSilo] = "neutral_silo",
        [BuildingKind.NeutralWindmill] = "neutral_windmill",
        [BuildingKind.TechOutpost] = "tech_outpost",
        [BuildingKind.NeutralCivilianBunker] = "neutral_civilian_bunker",
    };

    private static readonly BuildingKind[] Kinds = System.Linq.Enumerable.ToArray(Ids.Keys);

    /// <summary>Every kind, in the order of its number; a number taken out has none.</summary>
    public static IReadOnlyList<BuildingKind> All => Kinds;

    /// <summary>The highest number of a kind the engine knows.</summary>
    public static int Highest => (int)Kinds[^1];

    public static bool IsKnown(BuildingKind kind) => Ids.ContainsKey(kind);

    /// <summary>The data id of a kind, such as <c>neutral_house_a</c>.</summary>
    public static string IdOf(BuildingKind kind) => Ids.TryGetValue(kind, out var id)
        ? id
        : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building kind.");

    /// <summary>The kind the engine knows by the id <paramref name="id"/>.</summary>
    public static bool TryGetKind(string id, out BuildingKind kind)
    {
        foreach (var (candidate, candidateId) in Ids)
        {
            if (candidateId == id)
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }
}
