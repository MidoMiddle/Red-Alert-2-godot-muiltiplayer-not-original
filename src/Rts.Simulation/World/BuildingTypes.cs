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
}

/// <summary>
/// What a kind of building is (docs/implementation/M7_maps.md, 4.8): its footprint of <see cref="Width"/> × <see cref="Height"/>
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
/// The neutral buildings and tech buildings of the rules so far — winter and industrial sets, and the town, beach, desert, harbour and
/// farm buildings of the element documents (docs/city_elements … docs/temperate_elements) — kept in code until the data files of
/// M3 (game/data/neutral/).
/// Hit points are a rough guide until the balance of M4.
/// </summary>
public static class BuildingTypes
{
    // The soldiers a civilian building holds go by its size, from 3 in a hut to 12 in the biggest (the owner's wish, 2026-10-26; RA2's
    // MaxNumberOccupants runs about so): a 2 x 2 footprint holds 3, a 2 x 3 or 3 x 2 one 6, 3 x 3 and more 12; a stone building of
    // several storeys (1500 hit points and more) one step of 3 up, the skyscraper (2500) the most.
    private static readonly BuildingType[] Types =
    [
        new(BuildingKind.NeutralShed, "neutral_shed", 1, 1, MaxHp: 300, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralHouseA, "neutral_house_a", 2, 2, MaxHp: 800, Garrison: 3, Capturable: false),
        new(BuildingKind.NeutralApartment, "neutral_apartment", 3, 3, MaxHp: 1500, Garrison: 12, Capturable: false),
        new(BuildingKind.TechOilDerrick, "tech_oil_derrick", 2, 2, MaxHp: 1000, Garrison: 0, Capturable: true, ExtendsBase: true, DeathWeapon: "oil_explosion"),
        new(BuildingKind.TechHospital, "tech_hospital", 6, 4, MaxHp: 800, Garrison: 0, Capturable: true, ExtendsBase: true), // RA2: 6 x 4, 800
        // RA2 ParaDropSpecial: a paradrop every 4 minutes.
        new(BuildingKind.TechAirfield, "tech_airfield", 3, 3, MaxHp: 800, Garrison: 0, Capturable: true, Rules.SuperweaponKind.Paradrop, 240 * SimulationSettings.DefaultTicksPerSecond, ExtendsBase: true),
        new(BuildingKind.NeutralFuelTank, "neutral_fuel_tank", 1, 1, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralSmokestack, "neutral_smokestack", 1, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralCoolingTower, "neutral_cooling_tower", 2, 2, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralFactoryHall, "neutral_factory_hall", 4, 3, MaxHp: 800, Garrison: 12, Capturable: false),
        new(BuildingKind.NeutralBoilerHouse, "neutral_boiler_house", 2, 2, MaxHp: 800, Garrison: 3, Capturable: false),
        // RA2 bridge repair hut: cannot be hurt (Immune) nor taken; an engineer going in mends its bridge.
        new(BuildingKind.TechBridgeRepairHut, "tech_bridge_repair_hut", 1, 1, MaxHp: 2000, Garrison: 0, Capturable: false),

        // The element documents (2026-10-25): footprints, hit points by the class of the document (low ≈ 300, medium ≈ 800, high
        // ≈ 1500 and more), garrisons where the document has one; nothing is taken by an engineer.
        new(BuildingKind.NeutralSchool, "neutral_school", 3, 2, MaxHp: 1500, Garrison: 9, Capturable: false),
        new(BuildingKind.NeutralOffice, "neutral_office", 2, 2, MaxHp: 1500, Garrison: 6, Capturable: false),
        new(BuildingKind.NeutralChurch, "neutral_church", 2, 3, MaxHp: 1000, Garrison: 6, Capturable: false),
        new(BuildingKind.NeutralShop, "neutral_shop", 2, 2, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralKiosk, "neutral_kiosk", 1, 1, MaxHp: 200, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralSkyscraper, "neutral_skyscraper", 2, 2, MaxHp: 2500, Garrison: 12, Capturable: false),
        new(BuildingKind.NeutralCafe, "neutral_cafe", 2, 1, MaxHp: 600, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralBank, "neutral_bank", 3, 2, MaxHp: 2000, Garrison: 9, Capturable: false),
        new(BuildingKind.NeutralHotel, "neutral_hotel", 3, 2, MaxHp: 1800, Garrison: 9, Capturable: false),
        new(BuildingKind.NeutralFireStation, "neutral_fire_station", 3, 2, MaxHp: 1500, Garrison: 9, Capturable: false),
        new(BuildingKind.NeutralAutoRepair, "neutral_auto_repair", 2, 2, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralFountain, "neutral_fountain", 2, 2, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralBeachCabin, "neutral_beach_cabin", 1, 1, MaxHp: 300, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralBeachBar, "neutral_beach_bar", 3, 2, MaxHp: 600, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralLifeguardTower, "neutral_lifeguard_tower", 2, 2, MaxHp: 500, Garrison: 3, Capturable: false),
        new(BuildingKind.NeutralLighthouse, "neutral_lighthouse", 2, 2, MaxHp: 2000, Garrison: 6, Capturable: false),
        new(BuildingKind.NeutralBoatShed, "neutral_boat_shed", 2, 2, MaxHp: 600, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralAdobeHouse, "neutral_adobe_house", 2, 2, MaxHp: 600, Garrison: 3, Capturable: false),
        new(BuildingKind.NeutralAncientRuins, "neutral_ancient_ruins", 2, 2, MaxHp: 2500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralRockMesa, "neutral_rock_mesa", 2, 2, MaxHp: 4000, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralRockArch, "neutral_rock_arch", 2, 1, MaxHp: 3000, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralPortCrane, "neutral_port_crane", 1, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralContainerStack, "neutral_container_stack", 2, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralPortWarehouse, "neutral_port_warehouse", 3, 3, MaxHp: 1200, Garrison: 12, Capturable: false),
        new(BuildingKind.NeutralWaterTower, "neutral_water_tower", 1, 1, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralFarmhouse, "neutral_farmhouse", 2, 2, MaxHp: 700, Garrison: 3, Capturable: false),
        new(BuildingKind.NeutralBarn, "neutral_barn", 3, 2, MaxHp: 800, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralSilo, "neutral_silo", 1, 1, MaxHp: 1500, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralWindmill, "neutral_windmill", 2, 2, MaxHp: 700, Garrison: 0, Capturable: false),

        // The outpost of RA2 (docs/ra2_buildings/tech/outpost): 4 x 3, 2000 hit points, concrete, sight 6; the IFV's HoverMissile, a pad
        // that mends vehicles, a base of its owner's. A neutral one does not fire.
        new(BuildingKind.TechOutpost, "tech_outpost", 4, 3, MaxHp: 2000, Garrison: 0, Capturable: true, Weapon: "alliance_ifv_missiles",
            Armor: "concrete", Sight: 6, RepairsVehicles: true, ExtendsBase: true),
    ];

    // The rows by kind; a number that was taken out has none.
    private static readonly BuildingType?[] ByKind = Index();

    public static IReadOnlyList<BuildingType> All => Types;

    /// <summary>The highest number of a kind the engine knows.</summary>
    public static int Highest => ByKind.Length;

    /// <summary>The kind the engine knows by the id <paramref name="id"/>.</summary>
    public static bool TryGetKind(string id, out BuildingKind kind)
    {
        if (TryGetById(id, out var type))
        {
            kind = type.Kind;
            return true;
        }

        kind = default;
        return false;
    }

    public static bool IsKnown(BuildingKind kind) => kind >= BuildingKind.NeutralShed && (int)kind <= ByKind.Length && ByKind[(int)kind - 1] is not null;

    public static BuildingType Get(BuildingKind kind) =>
        IsKnown(kind) ? ByKind[(int)kind - 1]! : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building kind.");

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

    private static BuildingType?[] Index()
    {
        var highest = 0;
        foreach (var type in Types)
        {
            highest = Math.IntMath.Max(highest, (int)type.Kind);
        }

        var byKind = new BuildingType?[highest];
        foreach (var type in Types)
        {
            byKind[(int)type.Kind - 1] = type;
        }

        return byKind;
    }
}
