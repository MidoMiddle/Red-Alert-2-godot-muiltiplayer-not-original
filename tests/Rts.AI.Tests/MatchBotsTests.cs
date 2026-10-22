using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>The bots of a match setup (docs/implementation/M5_first_bot.md, step 2): attached to its bot slots, replayed without them or run again.</summary>
public sealed class MatchBotsTests
{
    private const int Ticks = 450;

    private static MatchPlayer Player(int slot, PlayerKind kind, int x, int y) =>
        new(slot, kind, AiDifficulty.Medium, "union", slot - 1, slot, slot, new CellPos(x, y));

    /// <summary>A person against two «Medium» bots on clear land.</summary>
    private static MatchSetup Setup() =>
        new("clear", 11, new MatchOptions(), [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 50, 50), Player(4, PlayerKind.Ai, 50, 10)],
            [new CellPos(10, 10), new CellPos(50, 50), new CellPos(50, 10), new CellPos(10, 50)]);

    private static (GameSimulation Simulation, ReplayRecorder Recorder) Play()
    {
        var simulation = new GameSimulation(SimulationSettings.ForMatch(Setup(), new MapGrid(64, 64)), TestRules.Standard);
        MatchBots.AttachAll(simulation);
        var recorder = new ReplayRecorder(simulation);
        for (var tick = 0; tick < Ticks; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        return (simulation, recorder);
    }

    [Fact]
    public void The_bots_go_to_the_bot_slots_only()
    {
        var simulation = new GameSimulation(SimulationSettings.ForMatch(Setup(), new MapGrid(64, 64)), TestRules.Standard);

        MatchBots.AttachAll(simulation);

        // The bot slots are taken now; the person's and the empty ones are free.
        Assert.Throws<InvalidOperationException>(() => simulation.AttachAi(new LambdaBot(new PlayerId(2), _ => { })));
        Assert.Throws<InvalidOperationException>(() => simulation.AttachAi(new LambdaBot(new PlayerId(4), _ => { })));
        simulation.AttachAi(new LambdaBot(new PlayerId(1), _ => { }));
        simulation.AttachAi(new LambdaBot(new PlayerId(3), _ => { }));
        Assert.Throws<ArgumentException>(() => MatchBots.Create(Setup(), Setup().Players[0]));
        Assert.Equal(new PlayerId(2), MatchBots.Create(Setup(), Setup().Players[1]).Player);
    }

    [Fact]
    public void Without_a_match_no_bot_is_attached()
    {
        var simulation = new GameSimulation(new SimulationSettings(1), TestRules.Standard);

        MatchBots.AttachAll(simulation);

        simulation.AttachAi(new LambdaBot(new PlayerId(2), _ => { }));
    }

    [Fact]
    public void A_match_against_medium_bots_plays_the_same_twice_and_its_replay_with_or_without_them()
    {
        var first = Play();
        var second = Play();
        var replay = ReplayFormat.Read(ReplayFormat.Write(first.Recorder.Build()));

        Assert.Equal(StateHasher.Compute(first.Simulation.World), StateHasher.Compute(second.Simulation.World));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.VerifyWithBots(replay, TestRules.Standard, player => MatchBots.Create(replay.Settings.Match!, player)).Status);
    }
}
