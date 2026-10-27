using System.Collections.Immutable;
using Rts.Simulation.Ai;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>
/// A match in its replay (docs/implementation/M5_first_bot.md, 4.4, 4.11): the setup is kept, the replay plays without bots, and with the
/// bots run again they give the recorded commands.
/// </summary>
public sealed class MatchReplayTests
{
    private const int Ticks = 300;
    private static readonly PlayerId Bot = new(2);

    /// <summary>A bot that sends its units to <paramref name="target"/> every 30 ticks.</summary>
    private sealed class MarchingBot(PlayerId player, CellPos target) : IAiController
    {
        public PlayerId Player { get; } = player;

        public void Think(AiTurn turn)
        {
            if (turn.Tick % 30 != 0)
            {
                return;
            }

            var units = turn.World.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Owner == Player).Select(unit => unit.Id).ToImmutableArray();
            turn.Issue(new MoveCommand(Player, turn.ExecuteTick, turn.NextSequence(), units, target.Center));
        }
    }

    /// <summary>A match against a marching bot, with a move of the person on tick 10 and one of a person playing the bot's player (F11).</summary>
    private static Replay Play()
    {
        var setup = MatchSetupTests.OneOnOne();
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard);
        simulation.AttachAi(new MarchingBot(Bot, new CellPos(30, 30)));
        var recorder = new ReplayRecorder(simulation);
        var people = simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == new PlayerId(1)).Select(unit => unit.Id).ToImmutableArray();
        simulation.Enqueue(new MoveCommand(new PlayerId(1), 10, 0, people, new CellPos(20, 20).Center));
        var bots = simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == Bot).Select(unit => unit.Id).ToImmutableArray();
        simulation.Enqueue(new StopCommand(Bot, 45, 0, bots));
        for (var tick = 0; tick < Ticks; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        return ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes);
    }

    [Fact]
    public void The_setup_of_the_match_comes_back_from_the_replay()
    {
        var replay = Play();

        Assert.Equal(MatchSetupTests.OneOnOne(), replay.Settings.Match);
        Assert.Equal(replay.Settings, ReplayFormat.Read(ReplayFormat.Write(replay), TestRules.Standard.MapTypes).Settings);
    }

    [Fact]
    public void The_replay_of_a_match_plays_without_its_bots()
    {
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(Play(), TestRules.Standard).Status);
    }

    [Fact]
    public void The_commands_of_the_bot_are_told_from_those_of_a_person_playing_its_player()
    {
        var replay = Play();

        var ofBot = replay.Commands.Where(command => command.Player == Bot).ToList();
        Assert.Equal(Ticks / 30, ofBot.Count(command => ReplayPlayer.IsBotCommand(replay, command)));
        Assert.Single(ofBot, command => !ReplayPlayer.IsBotCommand(replay, command));
        Assert.DoesNotContain(replay.Commands, command => command.Player == new PlayerId(1) && ReplayPlayer.IsBotCommand(replay, command));
    }

    [Fact]
    public void Run_again_the_bot_gives_the_recorded_commands_and_another_bot_does_not()
    {
        var replay = Play();

        var same = ReplayPlayer.VerifyWithBots(replay, TestRules.Standard, player => new MarchingBot(player.Id, new CellPos(30, 30)));
        var other = ReplayPlayer.VerifyWithBots(replay, TestRules.Standard, player => new MarchingBot(player.Id, new CellPos(31, 30)));

        Assert.Equal(ReplayStatus.Match, same.Status);
        Assert.Equal(new StateChecksum(Ticks, ReplayPlayer.Verify(replay, TestRules.Standard).Final!.Value.Value), same.Final);
        Assert.NotEqual(ReplayStatus.Match, other.Status);
    }

    [Theory]
    [InlineData(AiDifficulty.Easy)]
    [InlineData(AiDifficulty.Medium)]
    [InlineData(AiDifficulty.Hard)]
    [InlineData(AiDifficulty.Brutal)]
    public void The_level_of_a_bot_comes_back_from_the_replay(AiDifficulty difficulty)
    {
        // M8 step 1: the four levels in the byte of the level; «Medium» keeps the byte of M5.
        var setup = MatchSetupTests.OneOnOne();
        setup = setup with { Players = [setup.Players[0], setup.Players[1] with { Difficulty = difficulty }] };
        var recorder = new ReplayRecorder(new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard));

        Assert.Equal(difficulty, ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes).Settings.Match!.Players[1].Difficulty);
    }

    [Fact]
    public void A_replay_with_a_level_there_is_not_is_refused()
    {
        var setup = MatchSetupTests.OneOnOne();
        setup = setup with { Players = [setup.Players[0], setup.Players[1] with { Difficulty = (AiDifficulty)5 }] };
        var recorder = new ReplayRecorder(new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard));

        var error = Assert.Throws<InvalidDataException>(() => ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes));
        Assert.Contains("difficulty 5", error.Message);
    }

    [Fact]
    public void A_replay_without_a_match_has_no_bot_commands()
    {
        var simulation = new GameSimulation(new SimulationSettings(1), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new SpawnTestUnitCommand(Bot, 0, AiTurn.FirstSequence, UnitType.Conscript, new CellPos(5, 5).Center));
        simulation.Step();
        recorder.AfterStep();
        var replay = recorder.Build();

        Assert.Null(replay.Settings.Match);
        Assert.False(ReplayPlayer.IsBotCommand(replay, Assert.Single(replay.Commands)));
    }
}
