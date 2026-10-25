namespace Rts.Simulation.World;

/// <summary>
/// The MVP units of the first faction, in the order of docs/implementation/M3_base_economy.md, 4.6.
/// Values are part of the command and replay format: never renumber, only append.
/// </summary>
public enum UnitType : byte
{
    Conscript = 1,
    Rocketeer = 2,
    Engineer = 3,
    Mcv = 4,
    Harvester = 5,
    SupportVehicle = 6,
    HeavyTank = 7,
    TeslaTank = 8,

    // The Alliance (docs/content/names.md): its MCV, harvester and first infantry and tank.
    AllianceMcv = 9,
    AllianceHarvester = 10,
    AllianceRifleman = 11,
    AllianceLightTank = 12,

    // The late units of both factions (docs/content/names.md): ships and aircraft among them, built from the second stage on.
    AttackDog = 13,
    TeslaTrooper = 14,
    CrazyIvan = 15,
    Terrorist = 16,
    Desolator = 17,
    RocketArtillery = 18,
    ApocalypseTank = 19,
    TerrorDrone = 20,
    DemolitionTruck = 21,
    KirovAirship = 22,
    HoverTransport = 23,
    TyphoonSub = 24,
    SeaScorpion = 25,
    GiantSquid = 26,
    Dreadnought = 27,
    AllianceEngineer = 28,
    AllianceAttackDog = 29,
    AllianceRocketeer = 30,
    AllianceSpy = 31,
    AllianceChronoLegionnaire = 32,
    AllianceTanya = 33,
    AllianceSniper = 34,
    AllianceIfv = 35,
    AlliancePrismTank = 36,
    AllianceMirageTank = 37,
    AllianceTankDestroyer = 38,
    AllianceNighthawk = 39,
    AllianceHarrier = 40,
    AllianceBlackEagle = 41,
    AllianceAmphibiousTransport = 42,
    AllianceDolphin = 43,
    AllianceDestroyer = 44,
    AllianceAegisCruiser = 45,
    AllianceAircraftCarrier = 46,

    // 47 was the robot tank of Yuri's Revenge (stage 3d), taken out on 2026-10-25 to keep to RA2: never reuse the number.

    // The aircraft carrier's jet, launched and never built.
    AllianceHornet = 48,

    // The plane of a paradrop (loose ends, stage 1), sent and never built.
    ParadropPlane = 49,

    // The destroyer's anti-submarine plane (loose ends, stage 4), launched and never built.
    AllianceOsprey = 50,

    // Yuri of RA2's Soviets (2026-10-25): mind control and the psychic wave.
    Yuri = 51,

    // The units of stolen technology of RA2 (2026-10-25): a spy in a battle lab opens them.
    AllianceChronoCommando = 52,
    ChronoIvan = 53,
    AlliancePsiCommando = 54,
    YuriPrime = 55,
}
