using Rts.Data.Tests;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The «Medium» bot wins a match by the real data on Frozen Crossing against a person who does nothing — the MCV not deployed
/// (docs/implementation/M5_first_bot.md, section 6, step 10): before the 15th minute, in the normal game and in the short one, where the
/// person's MCV is their last «building».
/// </summary>
public sealed class VictoryScenarioTests
{
    private const int Limit = 15 * 900;

    private static readonly Lazy<BotMatch> Normal = new(() => Play(shortGame: false));

    private static readonly Lazy<BotMatch> Short = new(() => Play(shortGame: true));

    /// <summary>Plays until the match ends or the limit passes.</summary>
    private static BotMatch Play(bool shortGame)
    {
        var match = new BotMatch(shortGame: shortGame);
        while (match.World.Result is null && match.Simulation.CurrentTick < Limit)
        {
            match.Step();
        }

        return match;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_bot_wins_against_a_passive_person_before_the_fifteenth_minute(bool shortGame)
    {
        var match = (shortGame ? Short : Normal).Value;
        var result = match.World.Result;

        Assert.NotNull(result);
        Assert.True(result.Tick < Limit, $"ended on tick {result.Tick}");
        Assert.Equal([BotMatch.Bot], result.Winners.AsEnumerable());
        Assert.Equal(result.Tick, match.World.Player(new PlayerId(1)).DefeatedTick);
        Assert.Null(match.World.Player(BotMatch.Bot).DefeatedTick);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void A_match_won_by_the_bot_plays_back_with_the_bot_run_again()
    {
        var match = Normal.Value;
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player)).Status);
    }
}
