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
}
