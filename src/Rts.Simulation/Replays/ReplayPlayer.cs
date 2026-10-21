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
