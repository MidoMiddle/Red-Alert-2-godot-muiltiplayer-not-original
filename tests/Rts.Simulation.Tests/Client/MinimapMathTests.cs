using Rts.Client.Hud;

namespace Rts.Simulation.Tests.Client;

/// <summary>The minimap shows the whole map on its square (docs/implementation/M6_menus_skirmish.md, 4.7).</summary>
public sealed class MinimapMathTests
{
    [Fact]
    public void TheMapCornersLandOnTheMinimapCorners()
    {
        Assert.Equal((0f, 0f), MinimapMath.ToMinimap(0f, 0f, 64f, 64f, 180f));
        Assert.Equal((180f, 180f), MinimapMath.ToMinimap(64f, 64f, 64f, 64f, 180f));
    }

    [Fact]
    public void AClickOutsideTheMinimapStaysOnTheMap()
    {
        Assert.Equal((0f, 64f), MinimapMath.FromMinimap(-10f, 200f, 64f, 64f, 180f));
    }

    [Fact]
    public void AMinimapPixelMapsBackToTheSameCell()
    {
        var (x, y) = MinimapMath.FromMinimap(90f, 45f, 64f, 64f, 180f);
        var (px, py) = MinimapMath.ToMinimap(x, y, 64f, 64f, 180f);

        Assert.Equal(90f, px, 3);
        Assert.Equal(45f, py, 3);
    }
}
