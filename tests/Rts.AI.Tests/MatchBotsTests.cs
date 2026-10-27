using Rts.Data.Tests;
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
        Assert.Throws<ArgumentException>(() => MatchBots.Create(Setup(), Setup().Players[0], TestRules.Standard));
        Assert.Equal(new PlayerId(2), MatchBots.Create(Setup(), Setup().Players[1], TestRules.Standard).Player);
    }

    [Fact]
    public void Medium_comes_from_the_data_and_is_the_medium_of_the_code()
    {
        // The modules are tested with AiSettings.Medium: the data must say the same, or those tests test another bot. Tuned in the data,
        // the code follows (M5 step 14).
        Assert.Equal(AiSettings.Medium, MatchBots.SettingsOf(AiDifficulty.Medium, "union", GameDataFiles.Rules));
        Assert.Equal(AiSettings.Medium, MatchBots.SettingsOf(AiDifficulty.Medium, "union", TestRules.Standard));
    }

    [Fact]
    public void Without_a_match_no_bot_is_attached()
    {
        var simulation = new GameSimulation(new SimulationSettings(1), TestRules.Standard);

        MatchBots.AttachAll(simulation);

        simulation.AttachAi(new LambdaBot(new PlayerId(2), _ => { }));
    }

    [Fact]
    public void A_random_personality_is_drawn_from_the_seed_and_the_slot_among_those_of_the_data()
    {
        var database = GameDataFiles.Rules.Database;
        MatchPlayer Bot(int slot, string personality) => Player(slot, PlayerKind.Ai, 50, 50) with { Personality = personality };
        string Drawn(ulong seed, int slot) => MatchBots.PersonalityOf(Setup() with { Seed = seed }, Bot(slot, "random"), database);

        Assert.Equal("builder", MatchBots.PersonalityOf(Setup(), Bot(2, "builder"), database));
        Assert.Equal(Drawn(5, 2), Drawn(5, 2));
        var drawn = Enumerable.Range(1, 100).Select(seed => Drawn((ulong)seed, 2)).ToList();
        Assert.Equal(database.Personalities.Select(personality => personality.Id).Order(), drawn.Distinct().Order());
        Assert.Contains(Enumerable.Range(1, 100), seed => Drawn((ulong)seed, 2) != Drawn((ulong)seed, 3));
    }

    [Fact]
    public void A_bot_plays_the_personality_of_its_setup_and_a_random_one_as_drawn()
    {
        var setup = Setup();
        setup = setup with
        {
            Players = [setup.Players[0], setup.Players[1] with { Personality = "aggressor" }, setup.Players[2] with { Personality = "random" }],
        };

        Assert.Equal("ai_medium:aggressor", ((AiPlayer)MatchBots.Create(setup, setup.Players[1], GameDataFiles.Rules)).Profile);
        var drawn = MatchBots.PersonalityOf(setup, setup.Players[2], GameDataFiles.Rules.Database);
        Assert.Equal($"ai_medium:{drawn}", ((AiPlayer)MatchBots.For(setup, GameDataFiles.Rules)(setup.Players[2])).Profile);
    }

    [Fact]
    public void A_match_of_random_personalities_replays_with_its_bots_run_again_from_the_replay_alone()
    {
        var setup = Setup();
        setup = setup with { Players = [.. setup.Players.Select(player => player with { Personality = "random" })] };
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), GameDataFiles.Rules);
        MatchBots.AttachAll(simulation);
        var recorder = new ReplayRecorder(simulation);
        for (var tick = 0; tick < Ticks; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes);

        Assert.Equal(ReplayStatus.Match, ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, MatchBots.For(replay.Settings.Match!, GameDataFiles.Rules)).Status);
    }

    [Fact]
    public void A_match_against_medium_bots_plays_the_same_twice_and_its_replay_with_or_without_them()
    {
        var first = Play();
        var second = Play();
        var replay = ReplayFormat.Read(ReplayFormat.Write(first.Recorder.Build()), TestRules.Standard.MapTypes);

        Assert.Equal(StateHasher.Compute(first.Simulation.World), StateHasher.Compute(second.Simulation.World));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.VerifyWithBots(replay, TestRules.Standard, player => MatchBots.Create(replay.Settings.Match!, player, TestRules.Standard)).Status);
    }
}
