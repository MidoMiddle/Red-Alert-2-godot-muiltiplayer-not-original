using Rts.Data.Tests;
using Rts.Simulation.Events;
using Rts.Simulation.Replays;
using Xunit.Abstractions;

namespace Rts.AI.Tests;

/// <summary>
/// «Medium» against «Medium» (docs/implementation/M5_first_bot.md, section 6, step 14), the person's seat played by a bot too — no
/// window, as fast as the processor goes. On every ready map for 12 minutes in every run — the first waves and fights, short enough for
/// Debug; 20 matches of up to 40 minutes, by seed, in the
/// <c>Arena</c> category, which CI runs in Release only.
/// <para>
/// What must hold: no exception, no refused command, the replay plays back with the bot run again. Whether a match ends with a winner is
/// written down, not required: the maps hold 9 850–32 500 credits of ore for both, it never grows back on cells mined out, and two
/// equal bots wear each other down to their bases with a tesla coil. The owner's decision of step 14: the bot is tuned, the maps and the
/// economy are not; the winners come with the ore sources of the maps (docs/map_elements/06_ore).
/// </para>
/// </summary>
public sealed class ArenaTests(ITestOutputHelper output)
{
    private static readonly string[] MapIds = ["frozen-crossing", "twin-rivers", "taiga-front", "four-corners"];

    public static readonly TheoryData<string> Maps = new(MapIds);

    /// <summary>Plays bot against bot until a winner or <paramref name="minutes"/>; the longest time with no death, in ticks.</summary>
    private static (BotMatch Match, int Quiet) Play(string map, ulong seed, int minutes)
    {
        var match = new BotMatch(map, seed, bothBots: true);
        var (lastDeath, quiet) = (0, 0);
        while (match.World.Result is null && match.Simulation.CurrentTick < minutes * 900)
        {
            match.Step();
            if (match.Simulation.LastTickEvents.Any(simEvent => simEvent is EntityDestroyed))
            {
                quiet = Math.Max(quiet, match.Simulation.CurrentTick - lastDeath);
                lastDeath = match.Simulation.CurrentTick;
            }
        }

        return (match, quiet);
    }

    private static string Clock(int tick) => $"{tick / 900}:{tick % 900 / 15:00}";

    [Theory]
    [MemberData(nameof(Maps))]
    public void Bot_against_bot_plays_twelve_minutes_with_no_refused_command_and_its_replay_with_the_bot_run_again(string map)
    {
        var (match, _) = Play(map, seed: 3, minutes: 12);
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Empty(match.RefusedBotCommands);
        // The person's seat plays back its recorded commands; the bot of the other seat runs again and must give the same.
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player, GameDataFiles.Rules)).Status);
    }

    [Fact]
    [Trait("Category", "Arena")]
    public void Twenty_matches_of_bot_against_bot_up_to_forty_minutes_give_no_error_and_no_refused_command()
    {
        var winners = 0;
        foreach (var map in MapIds)
        {
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var (match, quiet) = Play(map, seed, minutes: 40);
                Assert.Empty(match.RefusedBotCommands);
                var result = match.World.Result;
                winners += result is null ? 0 : 1;
                output.WriteLine(
                    $"{map} seed {seed}: " + (result is null ? "no winner in 40:00" : $"P{result.Winners[0].Value} won at {Clock(result.Tick)}")
                    + $", longest time with no death {Clock(quiet)}");
            }
        }

        output.WriteLine($"winners: {winners} of 20");
    }
}
