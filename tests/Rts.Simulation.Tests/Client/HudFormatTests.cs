using Rts.Client.Hud;

namespace Rts.Simulation.Tests.Client;

/// <summary>The money line of the sidebar (docs/08_ui_ux.md, section 4).</summary>
public sealed class HudFormatTests
{
    [Theory]
    [InlineData(0, "$ 0")]
    [InlineData(999, "$ 999")]
    [InlineData(10000, "$ 10 000")]
    [InlineData(1234567, "$ 1 234 567")]
    [InlineData(-5, "$ -5")]
    public void CreditsAreGroupedInThrees(int amount, string expected)
    {
        Assert.Equal(expected, HudFormat.Credits(amount));
    }
}
