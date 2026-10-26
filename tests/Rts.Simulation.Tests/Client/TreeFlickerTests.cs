using Rts.Client.Units;

namespace Rts.Simulation.Tests.Client;

/// <summary>The holo tank shows its tree to its own side about a second in four (RA2), each tank in its own phase.</summary>
public sealed class TreeFlickerTests
{
    [Fact]
    public void A_still_holo_tank_shows_its_tree_a_second_in_four()
    {
        var shown = Enumerable.Range(0, TreeFlicker.CycleTicks * 10).Count(tick => TreeFlicker.Shown(tick, 5));

        Assert.Equal(TreeFlicker.ShownTicks * 10, shown);
        Assert.Equal(15, TreeFlicker.ShownTicks);
        Assert.Equal(60, TreeFlicker.CycleTicks);
    }

    [Fact]
    public void Two_tanks_do_not_flicker_together()
    {
        var together = Enumerable.Range(0, TreeFlicker.CycleTicks).Count(tick => TreeFlicker.Shown(tick, 1) && TreeFlicker.Shown(tick, 2));

        Assert.True(together < TreeFlicker.ShownTicks);
    }
}
