using System;
using System.Collections.Generic;
using Rts.Simulation.Ai;
using Rts.Simulation.Commands;
using Rts.Simulation.Hashing;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;

namespace Rts.Simulation.Replays;

/// <summary>
/// Rebuilds a recorded match from its settings and commands, by the rules it was played with (the replay keeps only their hash,
/// <see cref="Replay.DataHash"/>). Runs without Godot: used by tests,
/// by CI and later by the desync tools (docs/13_tools.md).
/// </summary>
public static class ReplayPlayer
{
    /// <summary>
    /// Simulation with the replay's settings and all its commands scheduled. Step it to watch the match;
    /// it ends after <see cref="Replay.TickCount"/> steps.
    /// </summary>
    public static GameSimulation CreateSimulation(Replay replay, GameRules rules)
    {
        var simulation = new GameSimulation(replay.Settings, rules);
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

    /// <summary>
    /// Whether <paramref name="command"/> of <paramref name="replay"/> was given by a bot (docs/implementation/M5_first_bot.md, 4.4): its
    /// player is a bot of the match setup and it is numbered as bots number. A person who played that player with the debug switch
    /// numbers from 0.
    /// </summary>
    public static bool IsBotCommand(Replay replay, Command command) =>
        command.Sequence >= AiTurn.FirstSequence && replay.Settings.Match?.PlayerOf(command.Player)?.Kind == PlayerKind.Ai;

    /// <summary>Plays the whole replay headless and compares every recorded checksum.</summary>
    public static ReplayVerification Verify(Replay replay, GameRules rules) =>
        replay.SimVersion != SimulationVersion.Current ? new ReplayVerification(ReplayStatus.IncompatibleVersion) : Play(replay, CreateSimulation(replay, rules));

    /// <summary>
    /// The replay with its bots run again (docs/implementation/M5_first_bot.md, 4.4, the mode «re-run the bots»): every recorded command
    /// but those of the bots is scheduled, <paramref name="createBot"/> makes a bot for every bot of the match setup, and the match is
    /// played; the checksums must hold and the bots must give the recorded commands, byte for byte. For the tests of determinism: a
    /// replay never needs its bots to play.
    /// </summary>
    public static ReplayVerification VerifyWithBots(Replay replay, GameRules rules, Func<MatchPlayer, IAiController> createBot)
    {
        if (replay.SimVersion != SimulationVersion.Current)
        {
            return new ReplayVerification(ReplayStatus.IncompatibleVersion);
        }

        var simulation = new GameSimulation(replay.Settings, rules);
        var recorded = new List<Command>();
        foreach (var command in replay.Commands)
        {
            if (IsBotCommand(replay, command))
            {
                recorded.Add(command);
            }
            else
            {
                simulation.Enqueue(command);
            }
        }

        foreach (var player in replay.Settings.Match?.Players ?? [])
        {
            if (player.Kind == PlayerKind.Ai)
            {
                simulation.AttachAi(createBot(player));
            }
        }

        var result = Play(replay, simulation);
        if (result.Status != ReplayStatus.Match)
        {
            return result;
        }

        var given = new List<Command>();
        foreach (var command in simulation.ExecutedCommands)
        {
            if (IsBotCommand(replay, command))
            {
                given.Add(command);
            }
        }

        return CommandSerializer.WriteAll(given).AsSpan().SequenceEqual(CommandSerializer.WriteAll(recorded))
            ? result
            : new ReplayVerification(ReplayStatus.BotsDiffer, Final: result.Final);
    }

    /// <summary>Steps <paramref name="simulation"/> through the replay and compares every recorded checksum.</summary>
    private static ReplayVerification Play(Replay replay, GameSimulation simulation)
    {
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
