using Rts.Client.Matches;
using Rts.Simulation.Commands;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests.Matches;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>What the results screen shows (docs/implementation/M5_first_bot.md, 4.10, step 12): the outcome, the rows, the length, the graph.</summary>
public sealed class MatchSummaryTests
{
    private static readonly PlayerId Person = new(1);
    private static readonly PlayerId Bot = new(2);

    /// <summary>A match of the tests where the bot surrenders on tick 3; played to tick 20, ended on the check of tick 15.</summary>
    private static GameWorld Surrendered()
    {
        var simulation = new GameSimulation(SimulationSettings.ForMatch(MatchSetupTests.OneOnOne(), new MapGrid(64, 64)), TestRules.Standard);
        simulation.Enqueue(new SurrenderCommand(Bot, 3, 0));
        for (var tick = 0; tick < 20; tick++)
        {
            simulation.Step();
        }

        return simulation.World;
    }

    [Fact]
    public void The_winner_sees_a_victory_the_loser_a_defeat_and_the_rows_go_by_slot_with_the_statistics()
    {
        var world = Surrendered();

        var person = MatchSummary.Of(world, Person, 15)!;
        var bot = MatchSummary.Of(world, Bot, 15)!;

        Assert.Equal(MatchOutcome.Victory, person.Outcome);
        Assert.Equal(MatchOutcome.Defeat, bot.Outcome);
        Assert.Equal([Person, Bot], person.Rows.Select(row => row.Player));
        Assert.True(person.Rows[0].IsYou && person.Rows[0].Won);
        Assert.False(person.Rows[1].IsYou || person.Rows[1].Won);
        Assert.Equal(MatchSummary.Columns.Length, person.Rows[1].Numbers.Length);
        // The bot's MCV and three conscripts, lost to its surrender.
        Assert.Equal(world.Stats.Of(Bot).UnitsLost, person.Rows[1].Numbers[1]);
        Assert.Equal(4, person.Rows[1].Numbers[1]);
        Assert.Equal(2, person.Series.Length);
        Assert.Equal("0:01", person.Duration);
        Assert.Equal(15, person.DurationTicks);
    }

    [Fact]
    public void No_winner_is_a_draw_and_outside_a_match_there_is_nothing_to_sum_up()
    {
        var world = Surrendered();
        world.Result = new MatchResult(15, []);

        Assert.Equal(MatchOutcome.Draw, MatchSummary.Of(world, Person, 15)!.Outcome);

        var sandbox = new GameSimulation(new SimulationSettings(1, new MapGrid(16, 16)), TestRules.Standard).World;
        Assert.Null(MatchSummary.Of(sandbox, Person, 15));
    }

    [Theory]
    [InlineData(0, 15, "0:00")]
    [InlineData(9510, 15, "10:34")]
    [InlineData(54000, 15, "60:00")]
    public void The_length_reads_as_minutes_and_seconds(int ticks, int perSecond, string text) =>
        Assert.Equal(text, MatchSummary.Clock(ticks, perSecond));

    [Fact]
    public void The_graph_runs_from_the_bottom_left_to_the_right_edge_and_the_strongest_point_touches_the_top()
    {
        StatsSample[] samples = [new(0, 0, 0, 0), new(450, 0, 600, 1), new(900, 0, 300, 1)];

        var points = MatchSummary.GraphPoints(samples, 300, 100, 600, 900);

        Assert.Equal([(0f, 100f), (150f, 0f), (300f, 50f)], points);
        Assert.Empty(MatchSummary.GraphPoints([], 300, 100, 600, 900));
        // No army anywhere: a flat line on the bottom, no division by nothing.
        Assert.All(MatchSummary.GraphPoints(samples, 300, 100, 0, 900), point => Assert.Equal(100f, point.Y));
    }
}
