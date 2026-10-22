using Rts.Client.Matches;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests.Matches;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>The colours of the players (docs/implementation/M5_first_bot.md, step 2).</summary>
public sealed class PlayerPaletteTests
{
    [Fact]
    public void Eight_colours_and_no_two_alike()
    {
        Assert.Equal(MatchSetup.MaxColors, PlayerPalette.Colors.Length);
        Assert.Equal(PlayerPalette.Colors.Length, PlayerPalette.Colors.Distinct().Count());
    }

    [Fact]
    public void Without_a_match_player_1_is_blue_and_2_red_as_before()
    {
        Assert.Equal((0.2f, 0.45f, 0.95f), PlayerPalette.Of(new PlayerId(1), null));
        Assert.Equal((0.9f, 0.25f, 0.2f), PlayerPalette.Of(new PlayerId(2), null));
        Assert.Equal(PlayerPalette.Neutral, PlayerPalette.Of(PlayerId.Neutral, null));
    }

    [Fact]
    public void In_a_match_a_player_plays_the_colour_of_its_setup()
    {
        var setup = MatchSetupTests.OneOnOne() with
        {
            Players = [MatchSetupTests.Player(1, PlayerKind.Human, 10, 10, color: 5), MatchSetupTests.Player(2, PlayerKind.Ai, 50, 50, color: 2)],
        };

        Assert.Equal(PlayerPalette.Colors[5], PlayerPalette.Of(new PlayerId(1), setup));
        Assert.Equal(PlayerPalette.Colors[2], PlayerPalette.Of(new PlayerId(2), setup));
        // A slot that is not in the match keeps the colour of its slot.
        Assert.Equal(PlayerPalette.Colors[2], PlayerPalette.Of(new PlayerId(3), setup));
    }
}
