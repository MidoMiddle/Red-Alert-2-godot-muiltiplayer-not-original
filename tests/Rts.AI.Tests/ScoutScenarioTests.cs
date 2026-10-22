using Rts.Data.Tests;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The scouting of the «Medium» bot in a match by the real data (docs/implementation/M5_first_bot.md, step 7): it finds the person in
/// good time, on maps of two starts and of four, explores far, and gives no refused command.
/// </summary>
public sealed class ScoutScenarioTests
{
    /// <summary>Plays <paramref name="map"/> for <paramref name="minutes"/>; returns the tick the bot first saw something of the person.</summary>
    private static (BotMatch Match, int? Found) Play(string map, int minutes)
    {
        var match = new BotMatch(map);
        int? found = null;
        while (match.Simulation.CurrentTick < minutes * 900)
        {
            match.Step();
            if (found is null && match.World.Entities.Any(e => e.IsAlive && e.Owner == new PlayerId(1) && Vision.Sees(match.World, BotMatch.Bot, e)))
            {
                found = match.Simulation.CurrentTick;
            }
        }

        return (match, found);
    }

    [Theory]
    [InlineData("frozen-crossing", 3)]
    [InlineData("taiga-front", 6)]
    [InlineData("four-corners", 6)]
    public void The_bot_finds_the_person_in_good_time_without_a_refused_command(string map, int minutes)
    {
        var (match, found) = Play(map, minutes);

        Assert.NotNull(found);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void Later_it_explores_more_than_half_of_frozen_crossing()
    {
        var (match, _) = Play("frozen-crossing", 15);
        var grid = match.World.VisionOf(BotMatch.Bot);
        var map = match.World.Map;
        var explored = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                explored += grid.IsExplored(new CellPos(x, y)) ? 1 : 0;
            }
        }

        Assert.True(explored * 2 > map.Width * map.Height, $"{explored} of {map.Width * map.Height} cells");
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void A_match_with_scouting_plays_the_same_twice_and_its_replay_with_the_bot_run_again()
    {
        var (match, _) = Play("frozen-crossing", 6);
        var (again, _) = Play("frozen-crossing", 6);
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Equal(StateHasher.Compute(match.World), StateHasher.Compute(again.World));
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player)).Status);
    }
}
