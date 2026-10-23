using Rts.Data.Tests;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Arena.Tests;

/// <summary>
/// The full Arena (docs/implementation/M8_ai_difficulty.md, 4.10, step 9): a matrix read from YAML or refused with its place, run in
/// parallel with the same results as one by one, its replays kept by the choice of the matrix and checked with the journals of their
/// bots, its summary; the interval of Wilson; the harvesters destroyed counted from the events.
/// </summary>
public sealed class MatrixTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rts-arena-matrix-" + Guid.NewGuid().ToString("N"));

    public MatrixTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private const string Small = """
        games: 2
        swapSides: true
        maxGameTime: 3m
        baseSeed: 70
        maps: [frozen-crossing, random:size=64,players=2]
        pairs:
          - { a: medium:balanced, b: easy }
          - { a: hard:aggressor, b: hard:builder }
        saveReplays: [draws]
        """;

    private static ArenaConfig Read(string text) =>
        ArenaConfig.Read("matrix.yaml", text, GameDataFiles.Current.Database, GameDataFiles.Folder);

    /// <summary>Runs the matrix <paramref name="text"/> into <paramref name="folder"/>; the code and what it printed.</summary>
    private static (int Code, string Output) Run(string text, string folder, int jobs)
    {
        var file = Path.Combine(folder, "matrix.yaml");
        Directory.CreateDirectory(folder);
        File.WriteAllText(file, text);
        var (output, error) = (new StringWriter(), new StringWriter());
        var code = ArenaCommand.Execute(["run", "--config", file, "--out", folder, "--jobs", jobs.ToString(), "--data", GameDataFiles.Folder], output, error);
        Assert.Equal(string.Empty, error.ToString());
        return (code, output.ToString());
    }

    [Fact]
    public void A_matrix_is_read_whole()
    {
        var config = Read(Small);

        Assert.Equal((2, true, 3 * 900, 70UL, 8), (config.Games, config.SwapSides, config.MaxTicks, config.BaseSeed, config.Total));
        Assert.Equal(new[] { "frozen-crossing", "random:size=64,players=2" }, config.Maps.Select(map => map.Text));
        Assert.NotNull(config.Maps[0].Ready);
        Assert.Equal((64, 2), (config.Maps[1].Random!.Size, config.Maps[1].Random!.Players));
        Assert.Equal("hard:builder:union", config.Pairs[1].B.ToString());
        Assert.Equal(new ReplaySelection(false, true, false, 0), config.Save);
    }

    [Theory]
    [InlineData("games: 2", "gamez: 2", "matrix.yaml:1:1: unknown key 'gamez'")]
    [InlineData("games: 2", "games: 0", "matrix.yaml:1:8: 'games' must be a whole number above zero, not '0'")]
    [InlineData("b: easy }", "b: insane }", "matrix.yaml:7:30: 'insane' in 'insane' is not a level")]
    [InlineData("frozen-crossing,", "atlantis,", "matrix.yaml:5:8: no map 'atlantis'")]
    [InlineData("random:size=64,players=2", "random:size=70,players=2", "'size=70' is not a setting of a random map")]
    [InlineData("[draws]", "[maybe]", "matrix.yaml:9:15: 'maybe' is not a choice of replays: all, draws, upsets or longest:N")]
    [InlineData("maxGameTime: 3m", "maxGameTime: soon", "'soon' must be a duration such as 60m or 90s")]
    public void A_mistake_in_a_matrix_is_told_with_its_place(string text, string replacement, string reason)
    {
        var error = Assert.Throws<ArenaException>(() => Read(Small.Replace(text, replacement)));

        Assert.Contains(reason, error.Message);
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void A_matrix_runs_every_match_in_order_and_writes_its_summary_its_replays_and_journals()
    {
        var (code, output) = Run(Small, _folder, jobs: 4);

        Assert.Equal(0, code);
        Assert.Contains("8 matches in", output);
        var columns = ArenaCommand.ResultColumns.ToList();
        var rows = File.ReadAllLines(Path.Combine(_folder, "results.csv")).Skip(1).Select(line => line.Split(',')).ToList();
        Assert.Equal(Enumerable.Range(1, 8).Select(n => n.ToString()), rows.Select(row => row[columns.IndexOf("game")]));
        Assert.Equal(Enumerable.Range(70, 8).Select(n => n.ToString()), rows.Select(row => row[columns.IndexOf("seed")]));
        Assert.Equal(
            new[] { "frozen-crossing", "frozen-crossing", "random:size=64;players=2", "random:size=64;players=2" },
            rows.Take(4).Select(row => row[columns.IndexOf("map")]));
        // The second match of a pair on a map swaps the starts.
        Assert.Equal((rows[0][columns.IndexOf("a_start")], rows[0][columns.IndexOf("b_start")]), (rows[1][columns.IndexOf("b_start")], rows[1][columns.IndexOf("a_start")]));

        // Three minutes: draws all, all kept, every one checked, with a journal a bot, a line every minute.
        Assert.All(rows, row => Assert.Equal("draw", row[columns.IndexOf("winner")]));
        Assert.All(rows, row => Assert.Equal("match", row[^1]));
        Assert.Equal(8, Directory.GetFiles(_folder, "game-*.rtsreplay").Length);
        Assert.Equal(3, File.ReadAllText(Path.Combine(_folder, "game-0001-p1.log")).Split("== tick ").Length - 1);
        // Between the snapshots the decisions with their reasons (M8 step 12).
        Assert.Contains("BuildPlanner: places", File.ReadAllText(Path.Combine(_folder, "game-0001-p1.log")));

        var summary = File.ReadAllText(Path.Combine(_folder, "summary.md"));
        foreach (var section in new[] { "## Wins", "## Profiles", "## Personalities", "## Replays kept", "## Time of the bots" })
        {
            Assert.Contains(section, summary);
        }

        Assert.Contains("| medium:balanced:union | easy:balanced:union | all | 4 | 0 | 0 | 4 |", summary);
        Assert.Contains("| hard:aggressor:union | hard:builder:union | random:size=64;players=2 | 2 |", summary);
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void One_at_a_time_or_four_at_once_the_results_are_the_same()
    {
        var keepOne = Small.Replace("[draws]", "[longest:1]");
        var (one, four) = (Path.Combine(_folder, "one"), Path.Combine(_folder, "four"));

        Run(keepOne, one, jobs: 1);
        Run(keepOne, four, jobs: 4);

        Assert.Equal(File.ReadAllBytes(Path.Combine(one, "results.csv")), File.ReadAllBytes(Path.Combine(four, "results.csv")));
        // Only the longest is kept — of eight draws of three minutes the first.
        Assert.Equal(new[] { "game-0001.rtsreplay" }, Directory.GetFiles(one, "*.rtsreplay").Select(Path.GetFileName));
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void The_smoke_matrix_of_ci_runs()
    {
        var text = File.ReadAllText(Path.Combine(GameDataFiles.Folder, "..", "..", "tools", "Rts.Arena", "configs", "smoke.yaml"));

        Assert.Equal(0, Run(text, _folder, jobs: 4).Code);
    }

    [Fact]
    public void The_ready_matrices_read()
    {
        foreach (var name in new[] { "acceptance", "personalities", "smoke" })
        {
            var file = Path.Combine(GameDataFiles.Folder, "..", "..", "tools", "Rts.Arena", "configs", name + ".yaml");
            Assert.True(ArenaConfig.Read(file, File.ReadAllText(file), GameDataFiles.Current.Database, GameDataFiles.Folder).Total > 0, name);
        }
    }

    [Theory]
    [InlineData(40, 50, 0.670, 0.888)]
    [InlineData(0, 10, 0.0, 0.278)]
    [InlineData(10, 10, 0.722, 1.0)]
    public void The_interval_of_wilson_holds_for_few_games_too(int wins, int games, double low, double high)
    {
        var (from, to) = ArenaSummary.Wilson(wins, games);

        Assert.Equal(low, from, 3);
        Assert.Equal(high, to, 3);
    }

    [Fact]
    public void A_harvester_of_the_enemy_destroyed_counts_for_the_side_that_destroyed_it()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(32, 32, GameDataFiles.Rules.Land)), GameDataFiles.Rules);
        simulation.Enqueue(new SpawnTestUnitCommand(new PlayerId(2), 0, 0, UnitType.Harvester, new CellPos(10, 10).Center));
        for (var i = 0; i < 3; i++)
        {
            simulation.Enqueue(new SpawnTestUnitCommand(new PlayerId(1), 0, (uint)(1 + i), UnitType.HeavyTank, new CellPos(13, 9 + i).Center));
        }

        var (built, killed, harvesters) = (new[] { new SortedDictionary<string, int>(), new SortedDictionary<string, int>() }, new int[2], new SortedDictionary<int, PlayerId>());
        var destroyed = 0;
        for (var tick = 0; tick < 60 * 15 && destroyed == 0; tick++)
        {
            ArenaMatch.Harvesters(simulation.World, harvesters);
            simulation.Step();
            ArenaMatch.Count(simulation, built, killed, harvesters);
            destroyed += simulation.LastTickEvents.OfType<EntityDestroyed>().Count();
        }

        Assert.Equal(1, destroyed);
        Assert.Equal(new[] { 1, 0 }, killed);
    }

    [Fact]
    public void A_win_of_the_lower_level_is_an_upset()
    {
        var easy = BotProfile.Parse("easy", GameDataFiles.Current.Database);
        var hard = BotProfile.Parse("hard", GameDataFiles.Current.Database);
        SideResult Side(BotProfile profile) => new(profile, 1, 0, 0, 0, 0, 0, 0, -1, System.Collections.Immutable.ImmutableSortedDictionary<string, int>.Empty, 0, TimeSpan.Zero, TimeSpan.Zero, 0, 0);
        MatchOutcome Outcome(string winner) => new(0, "m", 1, winner, 0, Side(easy), Side(hard), null!, TimeSpan.Zero);

        Assert.True(Outcome("a").Upset);
        Assert.False(Outcome("b").Upset);
        Assert.False(Outcome("draw").Upset);
    }
}
