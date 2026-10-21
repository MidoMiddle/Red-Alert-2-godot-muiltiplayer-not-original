using System;
using System.Collections.Generic;
using Rts.Simulation.Commands;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Replays;

/// <summary>
/// Collects checksums while a match is played. Call <see cref="AfterStep"/> after every
/// <see cref="GameSimulation.Step"/>; <see cref="Build"/> produces the replay at any moment.
/// Commands come from <see cref="GameSimulation.ExecutedCommands"/>.
/// </summary>
public sealed class ReplayRecorder
{
    /// <summary>Once per second at the default 15 ticks per second: enough to locate a desync, small on disk.</summary>
    public const int DefaultChecksumInterval = 15;

    private readonly GameSimulation _simulation;
    private readonly int _checksumInterval;
    private readonly List<StateChecksum> _checksums = new();

    public ReplayRecorder(GameSimulation simulation, int checksumInterval = DefaultChecksumInterval)
    {
        if (checksumInterval <= 0 || checksumInterval % simulation.Settings.HashInterval != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checksumInterval),
                checksumInterval,
                $"Must be a positive multiple of the simulation hash interval ({simulation.Settings.HashInterval}).");
        }

        _simulation = simulation;
        _checksumInterval = checksumInterval;
    }

    public void AfterStep()
    {
        var tick = _simulation.CurrentTick;
        if (tick % _checksumInterval == 0 && _simulation.LastChecksum is { } checksum && checksum.Tick == tick)
        {
            _checksums.Add(checksum);
        }
    }

    public Replay Build() => new(
        SimulationVersion.Current,
        _simulation.Settings,
        _simulation.CurrentTick,
        new List<Command>(_simulation.ExecutedCommands),
        new List<StateChecksum>(_checksums));
}
