namespace Rts.Simulation.Rules;

/// <summary>What an infiltrator does to a building it goes into (stage 3b, RA2 <c>Spyable</c>). Values are hashed with the data.</summary>
public enum SpyEffect : byte
{
    /// <summary>The building takes no infiltrator.</summary>
    None = 0,

    /// <summary>A power plant: its owner has no power for a while (RA2 <c>SpyPowerBlackout</c>).</summary>
    Blackout = 1,

    /// <summary>A radar: its owner forgets the map it explored.</summary>
    ResetMap = 2,

    /// <summary>A refinery: half of its owner's credits go to the infiltrator's player (RA2 <c>SpyMoneyStealPercent</c>).</summary>
    StealCredits = 3,

    /// <summary>A superweapon: its charge starts over (RA2).</summary>
    ResetCharge = 4,

    /// <summary>The infantry of the owner of the barracks comes out as veterans from now on — for the spy's player (RA2; loose ends, stage 3).</summary>
    VeteranInfantry = 5,

    /// <summary>The vehicles of the owner of the war factory come out as veterans from now on — for the spy's player.</summary>
    VeteranVehicles = 6,

    /// <summary>
    /// A battle lab: the spy's player steals the technology of the lab's faction for good (RA2): the units that need it
    /// (<see cref="UnitDef.RequiresStolen"/>: the Chrono Commando, the Chrono Ivan, the Psi Commando, Yuri Prime) open at its barracks.
    /// </summary>
    StealTech = 7,
}
