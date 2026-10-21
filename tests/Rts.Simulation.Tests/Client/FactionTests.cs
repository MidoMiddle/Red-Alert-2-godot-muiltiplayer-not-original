using Rts.Client.App;

namespace Rts.Simulation.Tests.Client;

/// <summary>The sidebar colour follows the faction of the player (docs/08_ui_ux.md, section 4).</summary>
public sealed class FactionTests
{
    [Fact]
    public void UnionIsRedLikeTheMenuAccent()
    {
        Assert.Equal("#B3121C", FactionPalette.AccentHex(Faction.Union));
    }

    [Fact]
    public void AllianceIsBlue()
    {
        var (red, green, blue) = Channels(FactionPalette.AccentHex(Faction.Alliance));

        Assert.True(blue > red && blue > green);
    }

    [Fact]
    public void CultIsGreyUntilItHasAColour()
    {
        Assert.Equal("#7D838C", FactionPalette.AccentHex(Faction.Cult));
    }

    private static (int Red, int Green, int Blue) Channels(string hex) => (
        Convert.ToInt32(hex.Substring(1, 2), 16),
        Convert.ToInt32(hex.Substring(3, 2), 16),
        Convert.ToInt32(hex.Substring(5, 2), 16));
}
