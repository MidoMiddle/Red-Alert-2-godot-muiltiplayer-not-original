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

    [Fact]
    public void A_playable_area_fills_the_minimap_as_a_rectangle_twice_as_wide_as_tall()
    {
        // The area of a 90 x 90 grid: v from -45 to 45, u from 45 to 135 (u = x + z, v = x - z).
        var view = MinimapMath.For(90f, 90f, (-45, 45, 45, 135), 180f);

        Assert.Equal((0f, 45f, 180f, 90f), view.Frame);
        Assert.Equal((0f, 45f), Round(view.ToMinimap(0f, 45f)));      // u 45, v -45: the top-left corner
        Assert.Equal((180f, 45f), Round(view.ToMinimap(45f, 0f)));    // u 45, v 45: the top-right corner
        Assert.Equal((0f, 135f), Round(view.ToMinimap(45f, 90f)));    // u 135, v -45: the bottom-left corner
        Assert.Equal((180f, 135f), Round(view.ToMinimap(90f, 45f)));  // u 135, v 45: the bottom-right corner
        var (x, z) = view.FromMinimap(120f, 70f);
        Assert.Equal((120f, 70f), Round(view.ToMinimap(x, z)));
    }

    private static (float, float) Round((float A, float B) point) => (MathF.Round(point.A, 3), MathF.Round(point.B, 3));
}
