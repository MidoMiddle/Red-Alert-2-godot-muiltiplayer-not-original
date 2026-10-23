using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>The description of a match and what can be played (docs/implementation/M5_first_bot.md, 4.11).</summary>
public sealed class MatchSetupTests
{
    internal static MatchPlayer Player(int slot, PlayerKind kind, int x, int y, int color = -1, string faction = "union") =>
        new(slot, kind, AiDifficulty.Medium, faction, color < 0 ? slot - 1 : color, Team: slot, StartIndex: slot, new CellPos(x, y));

    internal static MatchSetup OneOnOne(ulong seed = 7, int credits = 10_000) =>
        new("clear", seed, new MatchOptions(credits), [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 50, 50)], Starts);

    /// <summary>The starts of the clear map of these tests: four corners, two of them taken.</summary>
    internal static readonly ImmutableArray<CellPos> Starts = [new(10, 10), new(50, 50), new(50, 10), new(10, 50)];

    private static string? Problem(MatchSetup setup) => setup.Problem(64, 64, TestRules.Standard.Database);

    [Fact]
    public void One_against_one_can_be_played()
    {
        Assert.Null(Problem(OneOnOne()));
    }

    [Fact]
    public void Bots_alone_can_play_a_match()
    {
        // The matches of the Arena (docs/implementation/M8_ai_difficulty.md, 4.10, step 4): every seat a bot.
        Assert.Null(Problem(OneOnOne() with { Players = [Player(1, PlayerKind.Ai, 10, 10), Player(2, PlayerKind.Ai, 50, 50)] }));
    }

    [Theory]
    [InlineData("one player")]
    [InlineData("slot taken twice")]
    [InlineData("slot 9")]
    [InlineData("shared start")]
    [InlineData("shared colour")]
    [InlineData("colour 8")]
    [InlineData("unknown faction")]
    [InlineData("start off the map")]
    [InlineData("negative credits")]
    [InlineData("speed 7")]
    [InlineData("start not of the map")]
    [InlineData("fewer starts than players")]
    public void A_match_that_cannot_be_played_says_why(string mistake)
    {
        var good = OneOnOne();
        var setup = mistake switch
        {
            "one player" => good with { Players = [Player(1, PlayerKind.Human, 10, 10)] },
            "slot taken twice" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(1, PlayerKind.Ai, 50, 50, color: 1)] },
            "slot 9" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(9, PlayerKind.Ai, 50, 50, color: 1)] },
            "shared start" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 10, 10) with { StartIndex = 1 }] },
            "shared colour" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 50, 50, color: 0)] },
            "colour 8" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 50, 50, color: 8)] },
            "unknown faction" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 50, 50, faction: "atlantis")] },
            "start off the map" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 64, 50)] },
            "negative credits" => good with { Options = new MatchOptions(-1) },
            "speed 7" => good with { Options = new MatchOptions(GameSpeed: 7) },
            "start not of the map" => good with { Players = [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Ai, 30, 30)] },
            "fewer starts than players" => good with { MapStarts = [new CellPos(10, 10)] },
            _ => throw new ArgumentException(mistake),
        };

        Assert.NotNull(Problem(setup));
    }

    [Fact]
    public void A_simulation_refuses_a_match_that_cannot_be_played()
    {
        var setup = OneOnOne() with { Players = [Player(1, PlayerKind.Human, 10, 10)] };

        var error = Assert.Throws<ArgumentException>(() => new GameSimulation(SimulationSettings.ForMatch(setup, new Rts.Simulation.Map.MapGrid(64, 64)), TestRules.Standard));
        Assert.Contains("2–8 players", error.Message);
    }

    [Fact]
    public void The_settings_of_a_match_take_its_seed_and_credits_and_compare_by_its_players()
    {
        var map = new Rts.Simulation.Map.MapGrid(64, 64);
        var settings = SimulationSettings.ForMatch(OneOnOne(seed: 99, credits: 5000), map);

        Assert.Equal((99UL, 5000), (settings.Seed, settings.StartingCredits));
        Assert.Equal(settings, SimulationSettings.ForMatch(OneOnOne(seed: 99, credits: 5000), map));
        Assert.NotEqual(settings, SimulationSettings.ForMatch(OneOnOne(seed: 99, credits: 5000) with
        {
            Players = [Player(1, PlayerKind.Human, 10, 10), Player(2, PlayerKind.Human, 50, 50)],
        }, map));
        Assert.Equal(new PlayerId(2), OneOnOne().PlayerOf(new PlayerId(2))!.Id);
        Assert.Null(OneOnOne().PlayerOf(new PlayerId(3)));
    }
}
