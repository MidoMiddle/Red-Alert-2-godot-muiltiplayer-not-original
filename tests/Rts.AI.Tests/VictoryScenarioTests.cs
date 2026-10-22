using Rts.Data.Tests;
using Rts.Simulation.Replays;
using Rts.Simulation.Tests.Matches;
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

    private static readonly Lazy<(BotMatch Match, EventTally Tally)> Normal = new(() => Play(shortGame: false));

    private static readonly Lazy<(BotMatch Match, EventTally Tally)> Short = new(() => Play(shortGame: true));

    /// <summary>Plays until the match ends or the limit passes, counting its events beside its statistics (step 11).</summary>
    private static (BotMatch, EventTally) Play(bool shortGame)
    {
        var match = new BotMatch(shortGame: shortGame);
        var tally = EventTally.Attach(match.Simulation);
        while (match.World.Result is null && match.Simulation.CurrentTick < Limit)
        {
            match.Step();
        }

        return (match, tally);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_bot_wins_against_a_passive_person_before_the_fifteenth_minute(bool shortGame)
    {
        var match = (shortGame ? Short : Normal).Value.Match;
        var result = match.World.Result;

        Assert.NotNull(result);
        Assert.True(result.Tick < Limit, $"ended on tick {result.Tick}");
        Assert.Equal([BotMatch.Bot], result.Winners.AsEnumerable());
        Assert.Equal(result.Tick, match.World.Player(new PlayerId(1)).DefeatedTick);
        Assert.Null(match.World.Player(BotMatch.Bot).DefeatedTick);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void The_statistics_of_the_match_say_what_its_events_said_and_the_winner_built_and_destroyed()
    {
        var (match, tally) = Normal.Value;
        var (bot, person) = (match.World.Stats.Of(BotMatch.Bot), match.World.Stats.Of(new PlayerId(1)));

        tally.AssertAgrees(match.World);
        // And by the bot's own timeline, which writes down every building placed — the deploy too — and every unit produced.
        Assert.Equal(match.Timeline.Count(line => line.Text.StartsWith("placed ", StringComparison.Ordinal)), bot.BuildingsBuilt);
        Assert.Equal(match.Timeline.Count(line => line.Text.StartsWith("produced ", StringComparison.Ordinal)), bot.UnitsBuilt);
        Assert.True(bot.UnitsBuilt > 0 && bot.BuildingsBuilt > 0 && bot.CreditsHarvested > 0 && bot.CreditsSpent > 0);
        // The person's MCV and three conscripts, all destroyed by the bot.
        Assert.Equal((4, 4), (person.UnitsLost, bot.UnitsDestroyed));
        Assert.Equal(match.World.Result!.Tick, match.World.Stats.Duration);
        Assert.Equal(match.World.Result.Tick / Rts.Simulation.Matches.MatchStats.SampleTicks + 1, bot.Samples.Count);
    }

    [Fact]
    public void A_match_won_by_the_bot_plays_back_with_the_bot_run_again()
    {
        var match = Normal.Value.Match;
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player)).Status);
    }
}
