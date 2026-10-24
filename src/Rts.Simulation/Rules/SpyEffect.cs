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
}
