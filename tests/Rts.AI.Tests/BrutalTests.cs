using Rts.Data.Tests;
using Rts.Simulation;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;

namespace Rts.AI.Tests;

/// <summary>
/// «Brutal» on the game data (docs/implementation/M8_ai_difficulty.md, 4.5, step 2): the bonuses of its player come from its level, go
/// with the replay through the setup and the data, and its bot sees the whole map.
/// </summary>
public sealed class BrutalTests
{
    [Trait("Category", "Slow")]
    [Fact]
    public void A_match_against_brutal_plays_back_from_its_replay_with_the_same_bonuses()
    {
        var match = new BotMatch(difficulty: AiDifficulty.Brutal).PlayTo(3);
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Empty(match.RefusedBotCommands);
        Assert.Equal(AiDifficulty.Brutal, replay.Settings.Match!.PlayerOf(BotMatch.Bot)!.Difficulty);
        Assert.Equal(new PlayerModifiers(40, 20, true), new GameSimulation(replay.Settings, GameDataFiles.Rules).World.Player(BotMatch.Bot).Modifiers);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, GameDataFiles.Rules).Status);
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player, GameDataFiles.Rules)).Status);
    }

    [Theory]
    [InlineData(AiDifficulty.Brutal, true)]
    [InlineData(AiDifficulty.Medium, false)]
    public void Only_brutal_sees_the_whole_map(AiDifficulty difficulty, bool sees)
    {
        var match = new BotMatch(difficulty: difficulty);
        // Past the perception of «Brutal», 3 ticks (M8 step 3): seen is not yet known.
        for (var i = 0; i < 6; i++)
        {
            match.Step();
        }

        var view = match.Brain!.Context!.View;
        var person = match.Setup.Players.Single(player => player.Kind == PlayerKind.Human);

        Assert.Equal(sees, view.RevealsMap);
        Assert.Equal(sees, view.IsVisible(person.Start) && view.IsExplored(person.Start));
        Assert.Equal(sees, view.VisibleEnemies.Any(enemy => enemy.Owner == person.Id));
    }
}
