using Rts.Simulation.Hashing;

namespace Rts.Simulation.Replays;

public enum ReplayStatus
{
    /// <summary>Every recorded checksum was reproduced.</summary>
    Match,

    /// <summary>A recorded checksum differs: the rebuild diverged from the original match.</summary>
    Desync,

    /// <summary>The replay was recorded with other simulation rules and cannot be checked.</summary>
    IncompatibleVersion,

    /// <summary>
    /// The bots, run again (<see cref="ReplayPlayer.VerifyWithBots"/>), gave other commands than those recorded, while every checksum
    /// held (M5 step 2).
    /// </summary>
    BotsDiffer,
}

/// <summary>Outcome of <see cref="ReplayPlayer.Verify"/> and <see cref="ReplayPlayer.VerifyWithBots"/>.</summary>
/// <param name="Expected">For a desync: the recorded checksum that was not reproduced.</param>
/// <param name="Actual">For a desync: the checksum the rebuild produced on the same tick.</param>
/// <param name="Final">For a match: checksum of the final state.</param>
public sealed record ReplayVerification(
    ReplayStatus Status,
    StateChecksum? Expected = null,
    StateChecksum? Actual = null,
    StateChecksum? Final = null)
{
    public override string ToString() => Status switch
    {
        ReplayStatus.Match => $"Match, final {Final}",
        ReplayStatus.Desync => $"Desync on tick {Expected?.Tick}: expected {Expected}, got {Actual}",
        ReplayStatus.BotsDiffer => "The bots gave other commands than recorded",
        _ => "Incompatible simulation version",
    };
}
