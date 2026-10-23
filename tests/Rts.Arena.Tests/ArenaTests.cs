using Rts.Data.Tests;
using Rts.Simulation.Matches;

namespace Rts.Arena.Tests;

/// <summary>
/// The minimal Arena (docs/implementation/M8_ai_difficulty.md, 4.10, step 4): profiles read or refused in plain words, two short matches
/// of bots against bots with their CSV and replays, the same results on every run, and a replay checked again from its file.
/// </summary>
public sealed class ArenaTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rts-arena-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>Runs the Arena with <paramref name="args"/> and the data of the game; its code, what it wrote and what it complained of.</summary>
    private static (int Code, string Output, string Error) Arena(params string[] args)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var code = ArenaCommand.Execute([.. args, "--data", GameDataFiles.Folder], output, error);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>Two matches of «Medium» against «Easy» on Frozen Crossing, 3 minutes each, into <paramref name="folder"/>.</summary>
    private static (int Code, string Output, string Error) TwoShortMatches(string folder) =>
        Arena("run", "--a", "medium:balanced", "--b", "easy", "--map", "frozen-crossing", "--games", "2", "--minutes", "3", "--out", folder);

    [Theory]
    [InlineData("hard", AiDifficulty.Hard, "balanced", "union")]
    [InlineData("easy:aggressor", AiDifficulty.Easy, "aggressor", "union")]
    [InlineData("brutal:balanced:union", AiDifficulty.Brutal, "balanced", "union")]
    public void A_profile_reads_as_level_personality_and_faction(string text, AiDifficulty level, string personality, string faction)
    {
        Assert.Equal(new BotProfile(level, personality, faction), BotProfile.Parse(text, GameDataFiles.Current.Database));
    }

    [Theory]
    [InlineData("insane", "'insane' in 'insane' is not a level: one of easy, medium, hard, brutal")]
    [InlineData("medium:grumpy", "'grumpy' in 'medium:grumpy' is not a personality of the data: one of aggressor, balanced, builder, saboteur")]
    [InlineData("medium:balanced:allies", "'allies' in 'medium:balanced:allies' is not a faction of the data: one of union")]
    [InlineData("", "'' is not a profile")]
    [InlineData("a:b:c:d", "'a:b:c:d' is not a profile")]
    public void A_wrong_profile_is_refused_in_plain_words(string text, string reason)
    {
        var error = Assert.Throws<ArenaException>(() => BotProfile.Parse(text, GameDataFiles.Current.Database));

        Assert.StartsWith(reason, error.Message);
    }

    [Fact]
    public void A_wrong_command_line_stops_with_code_2_and_says_why()
    {
        Assert.Contains("--b is missing", Arena("run", "--a", "medium", "--map", "frozen-crossing").Error);
        Assert.Contains("no map 'atlantis'", Arena("run", "--a", "medium", "--b", "easy", "--map", "atlantis").Error);
        Assert.Contains("--games must be a whole number above zero, not 'two'", Arena("run", "--a", "medium", "--b", "easy", "--map", "frozen-crossing", "--games", "two").Error);
        Assert.Equal(2, Arena("dance").Code);
    }

    [Fact]
    public void Two_matches_give_their_results_and_replays_that_match()
    {
        var (code, output, error) = TwoShortMatches(_folder);

        Assert.Equal(0, code);
        Assert.Empty(error);
        Assert.Contains("game 2, seed 1001", output);
        var lines = File.ReadAllLines(Path.Combine(_folder, "results.csv"));
        Assert.Equal(3, lines.Length);
        Assert.Equal(string.Join(",", ArenaCommand.ResultColumns), lines[0]);
        var rows = lines.Skip(1).Select(line => line.Split(',')).ToList();
        Assert.All(rows, row => Assert.Equal(ArenaCommand.ResultColumns.Length, row.Length));
        Assert.All(rows, row => Assert.Equal("match", row[^1]));
        Assert.All(rows, row => Assert.Equal("medium:balanced:union", row[3]));
        // The sides swap starts on the second match.
        Assert.Equal((rows[0][5], rows[0][6]), (rows[1][6], rows[1][5]));
        Assert.True(File.Exists(Path.Combine(_folder, "game-0001.rtsreplay")));
        Assert.True(File.Exists(Path.Combine(_folder, "game-0002.rtsreplay")));
        Assert.Equal(3, File.ReadAllLines(Path.Combine(_folder, "timings.csv")).Length);
    }

    [Fact]
    public void The_same_matches_give_the_same_results_byte_for_byte()
    {
        var other = _folder + "-again";
        try
        {
            TwoShortMatches(_folder);
            TwoShortMatches(other);

            Assert.Equal(File.ReadAllBytes(Path.Combine(_folder, "results.csv")), File.ReadAllBytes(Path.Combine(other, "results.csv")));
            Assert.Equal(File.ReadAllBytes(Path.Combine(_folder, "game-0002.rtsreplay")), File.ReadAllBytes(Path.Combine(other, "game-0002.rtsreplay")));
        }
        finally
        {
            Directory.Delete(other, recursive: true);
        }
    }

    [Fact]
    public void The_aggressor_attacks_first_at_least_three_minutes_before_the_builder()
    {
        // M8 step 8, the step is done when: «Hard» leaves the first attack to the personality — 3–5 minutes for the aggressor, 10–12 for
        // the builder. Two matches of 15 minutes, the sides swapping starts.
        var (code, output, error) = Arena(
            "run", "--a", "hard:aggressor", "--b", "hard:builder", "--map", "frozen-crossing", "--games", "2", "--minutes", "15", "--out", _folder);
        Assert.Equal(0, code);
        Assert.Empty(error);

        var columns = ArenaCommand.ResultColumns.ToList();
        var rows = File.ReadAllLines(Path.Combine(_folder, "results.csv")).Skip(1).Select(line => line.Split(',')).ToList();
        var aggressor = rows.Select(row => int.Parse(row[columns.IndexOf("a_first_attack")])).ToList();
        var builder = rows.Select(row => int.Parse(row[columns.IndexOf("b_first_attack")])).ToList();
        Assert.All(aggressor.Concat(builder), tick => Assert.True(tick >= 0, output));
        Assert.True(builder.Average() - aggressor.Average() >= 3 * 900, $"aggressor {string.Join(", ", aggressor)}, builder {string.Join(", ", builder)}");
    }

    [Fact]
    public void A_replay_is_checked_again_from_its_file_and_a_spoilt_one_does_not_match()
    {
        TwoShortMatches(_folder);
        var file = Path.Combine(_folder, "game-0001.rtsreplay");

        var (code, output, _) = Arena("replay", file, "--a", "medium:balanced", "--b", "easy");
        Assert.Equal(0, code);
        Assert.Contains(": match", output);

        // The last 8 bytes are the last checksum recorded.
        var bytes = File.ReadAllBytes(file);
        bytes[^1] ^= 0xFF;
        var spoilt = Path.Combine(_folder, "spoilt.rtsreplay");
        File.WriteAllBytes(spoilt, bytes);
        var (spoiltCode, spoiltOutput, _) = Arena("replay", spoilt, "--a", "medium:balanced", "--b", "easy");
        Assert.Equal(1, spoiltCode);
        Assert.Contains("without-bots:desync", spoiltOutput);
    }
}
