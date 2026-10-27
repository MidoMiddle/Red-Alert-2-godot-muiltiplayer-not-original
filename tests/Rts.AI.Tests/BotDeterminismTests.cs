using Rts.Simulation;
using Rts.Simulation.Ai;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.Tests;
using Rts.Simulation.Tests.Determinism;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// Bots and determinism (docs/implementation/M5_first_bot.md, step 1, section 6): the reference match of the simulation tests with bots
/// attached. Empty bots change nothing; a bot that gives commands plays the same twice, and its replay, played without bots, is the
/// same match.
/// </summary>
public sealed class BotDeterminismTests
{
    private const int Ticks = 1000;

    private static (IReadOnlyList<StateChecksum> Checksums, GameSimulation Simulation, ReplayRecorder Recorder) Run(params IAiController[] bots)
    {
        var simulation = new GameSimulation(ReferenceScenario.Settings(), TestRules.Standard);
        foreach (var bot in bots)
        {
            simulation.AttachAi(bot);
        }

        var recorder = new ReplayRecorder(simulation);
        foreach (var command in ReferenceScenario.Commands(Ticks))
        {
            simulation.Enqueue(command);
        }

        var checksums = new List<StateChecksum>(Ticks);
        for (var tick = 0; tick < Ticks; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
            checksums.Add(simulation.LastChecksum!.Value);
        }

        return (checksums, simulation, recorder);
    }

    [Fact]
    public void Empty_bots_leave_every_checksum_of_the_match_as_it_was()
    {
        var without = Run().Checksums;
        var with = Run(new AiPlayer(new PlayerId(1), AiSettings.Medium), new AiPlayer(new PlayerId(2), AiSettings.Medium)).Checksums;

        Assert.Equal(without, with);
    }

    [Fact]
    public void A_bot_that_gives_commands_plays_the_same_twice_and_changes_the_match()
    {
        var first = Run(new WalkerBot(new PlayerId(3)));
        var second = Run(new WalkerBot(new PlayerId(3)));

        Assert.Equal(first.Checksums, second.Checksums);
        Assert.NotEqual(Run().Checksums[^1], first.Checksums[^1]);
        // A spawn, then a move every 15 ticks while its tank lives: the players of the match fight it.
        var commands = first.Simulation.ExecutedCommands.Where(c => c.Player == new PlayerId(3)).ToList();
        Assert.IsType<Rts.Simulation.Commands.SpawnTestUnitCommand>(commands[0]);
        Assert.True(commands.Count > 5, $"{commands.Count} commands of the bot");
    }

    [Fact]
    public void The_replay_of_a_match_with_a_bot_played_without_bots_is_the_same_match()
    {
        var live = Run(new WalkerBot(new PlayerId(3)));
        var replay = ReplayFormat.Read(ReplayFormat.Write(live.Recorder.Build()), TestRules.Standard.MapTypes);

        var result = ReplayPlayer.Verify(replay, TestRules.Standard);

        Assert.Equal(ReplayStatus.Match, result.Status);
        Assert.Equal(new StateChecksum(Ticks, StateHasher.Compute(live.Simulation.World)), result.Final);
    }
}
