using Rts.Simulation.Hashing;

namespace Rts.Simulation.Replays;

/// <summary>
/// Rebuilds a recorded match from its settings and commands. Runs without Godot: used by tests,
/// by CI and later by the desync tools (docs/13_tools.md).
/// </summary>
public static class ReplayPlayer
{
    /// <summary>
    /// Simulation with the replay's settings and all its commands scheduled. Step it to watch the match;
    /// it ends after <see cref="Replay.TickCount"/> steps.
    /// </summary>
    public static GameSimulation CreateSimulation(Replay replay)
    {
        var simulation = new GameSimulation(replay.Settings);
        foreach (var command in replay.Commands)
        {
            simulation.Enqueue(command);
        }

        return simulation;
    }

    /// <summary>
    /// A warning when the replay was recorded with other data files than <paramref name="currentDataHash"/>, null when they match
    /// (docs/implementation/M3_base_economy.md, 4.4). The replay still plays: the warning explains a desync before it happens.
    /// </summary>
    public static string? DataWarning(Replay replay, ulong currentDataHash) =>
        replay.DataHash == currentDataHash
            ? null
            : $"recorded with other data (hash {replay.DataHash:x16}, here {currentDataHash:x16}): it may not play the same";

    /// <summary>Plays the whole replay headless and compares every recorded checksum.</summary>
    public static ReplayVerification Verify(Replay replay)
    {
        if (replay.SimVersion != SimulationVersion.Current)
        {
            return new ReplayVerification(ReplayStatus.IncompatibleVersion);
        }

        var simulation = CreateSimulation(replay);
        var nextChecksum = 0;
        for (var i = 0; i < replay.TickCount; i++)
        {
            simulation.Step();

            while (nextChecksum < replay.Checksums.Count && replay.Checksums[nextChecksum].Tick == simulation.CurrentTick)
            {
                var expected = replay.Checksums[nextChecksum++];
                var actual = new StateChecksum(simulation.CurrentTick, StateHasher.Compute(simulation.World));
                if (actual != expected)
                {
                    return new ReplayVerification(ReplayStatus.Desync, expected, actual);
                }
            }
        }

        if (nextChecksum != replay.Checksums.Count)
        {
            // Checksums out of order or beyond the recorded tick count: the file does not describe this match.
            var unmatched = replay.Checksums[nextChecksum];
            return new ReplayVerification(ReplayStatus.Desync, unmatched, null);
        }

        var final = new StateChecksum(simulation.CurrentTick, StateHasher.Compute(simulation.World));
        return new ReplayVerification(ReplayStatus.Match, Final: final);
    }
}
