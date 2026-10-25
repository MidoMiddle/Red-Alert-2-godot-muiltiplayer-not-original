using System.Collections.Generic;
using Rts.Simulation.Commands;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Replays;

/// <summary>
/// A recorded match: everything needed to rebuild it tick by tick, plus checksums to prove the rebuild
/// matches the original. A replay stores input (commands), never state.
/// </summary>
/// <param name="SimVersion">Rules version the match was played with (<see cref="SimulationVersion"/>).</param>
/// <param name="DataHash">Hash of the data files the match was played with (<see cref="Rules.DataHasher"/>).</param>
/// <param name="Settings">Match settings: seed, tick rate, map size, hash interval.</param>
/// <param name="TickCount">Number of ticks played.</param>
/// <param name="Commands">Executed commands, in execution order.</param>
/// <param name="Checksums">Checksums taken during the match, in tick order.</param>
public sealed record Replay(
    uint SimVersion,
    ulong DataHash,
    SimulationSettings Settings,
    int TickCount,
    IReadOnlyList<Command> Commands,
    IReadOnlyList<StateChecksum> Checksums)
{
    /// <summary>Who played a network match and with what (M9 step 11); null for a match on one computer.</summary>
    public ReplayNetwork? Network { get; init; }
}

/// <summary>
/// What a replay of a network match adds (docs/implementation/M9_network.md, 4.13): the version of the game, the hash of the map file
/// the host checked everyone against, and the name of every person by player. Not needed to play it back; for the screen and for
/// finding the right build.
/// </summary>
/// <param name="GameVersion">The version string of the game the match was played with.</param>
/// <param name="MapHash">The hash of the map file (the lobby's).</param>
/// <param name="Names">The name of every person of the match, by player id.</param>
public sealed record ReplayNetwork(string GameVersion, ulong MapHash, IReadOnlyDictionary<int, string> Names);
