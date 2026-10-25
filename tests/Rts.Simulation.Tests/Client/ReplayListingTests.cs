using System.Collections.Immutable;
using Rts.Client.Matches;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;

namespace Rts.Simulation.Tests.Client;

/// <summary>The rows of the list of replays (M6 step 15): the players by team, the length, the version.</summary>
public sealed class ReplayListingTests
{
    private static MatchPlayer Player(int slot, PlayerKind kind, int team) =>
        new(slot, kind, AiDifficulty.Hard, "union", slot - 1, team, slot - 1, new CellPos(slot * 10, 10));

    private static MatchSetup Setup(params MatchPlayer[] players) =>
        new("map", 1, new MatchOptions(), [.. players], [.. players.Select(player => player.Start)]);

    [Fact]
    public void The_players_go_by_team_people_by_name_bots_by_level()
    {
        var setup = Setup(Player(1, PlayerKind.Human, 1), Player(2, PlayerKind.Ai, 2), Player(3, PlayerKind.Ai, 1), Player(4, PlayerKind.Ai, 2));

        var text = ReplayListing.Players(setup, null, _ => "You", bot => $"Bot {bot.Slot}");

        Assert.Equal("You, Bot 3 vs Bot 2, Bot 4", text);
    }

    [Fact]
    public void A_network_match_names_its_people()
    {
        var setup = Setup(Player(1, PlayerKind.Human, 1), Player(2, PlayerKind.Human, 2));
        var network = new ReplayNetwork("0.9.0", 1, new Dictionary<int, string> { [1] = "Host", [2] = "Guest" });

        Assert.Equal("Host vs Guest", ReplayListing.Players(setup, network, _ => "You", _ => "Bot"));
        Assert.Equal("", ReplayListing.Players(null, network, _ => "You", _ => "Bot"));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(15 * 59, "0:59")]
    [InlineData(15 * 760, "12:40")]
    [InlineData(15 * 3725, "1:02:05")]
    [InlineData(-1, "—")]
    public void The_length_is_minutes_and_seconds(int ticks, string text)
    {
        Assert.Equal(text, ReplayListing.Length(ticks, 15));
    }

    [Fact]
    public void An_old_replay_says_so()
    {
        var header = new ReplayHeader(ReplayFormat.FormatVersion, SimulationVersion.Current, 15, 100);

        Assert.Equal($"v{SimulationVersion.Current}", ReplayListing.Version(header, "(old)"));
        Assert.Equal("v3 (old)", ReplayListing.Version(header with { SimVersion = 3 }, "(old)"));
    }
}
