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
    IReadOnlyList<StateChecksum> Checksums);
