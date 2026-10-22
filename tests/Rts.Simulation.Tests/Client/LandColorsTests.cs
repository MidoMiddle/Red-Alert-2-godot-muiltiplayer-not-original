using Rts.Client.Terrain;
using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Client;

public sealed class LandColorsTests
{
    [Fact]
    public void Every_land_type_has_its_own_colour()
    {
        var colours = Enum.GetValues<LandType>().Select(LandColors.Of).ToList();

        Assert.Equal(colours.Count, colours.Distinct().Count());
        Assert.All(colours, c => Assert.True(c.R is >= 0 and <= 1 && c.G is >= 0 and <= 1 && c.B is >= 0 and <= 1));
    }

    [Theory]
    [InlineData(LandType.Shallow)]
    [InlineData(LandType.Deep)]
    public void Water_is_blue(LandType water)
    {
        var (r, g, b) = LandColors.Of(water);

        Assert.True(b > r && b > g, $"{water} should be blue, got ({r}, {g}, {b}).");
        var (cr, _, cb) = LandColors.Of(LandType.Clear);
        Assert.True(b - r > cb - cr, $"{water} should be bluer than clear land.");
    }

    [Fact]
    public void A_cliff_is_rock_whatever_its_land_and_a_slope_keeps_its_land()
    {
        Assert.All(Enum.GetValues<LandType>(), land => Assert.Equal(LandColors.Cliff, LandColors.Of(land, CellShape.Cliff)));
        Assert.All(Enum.GetValues<LandType>(), land => Assert.Equal(LandColors.Of(land), LandColors.Of(land, CellShape.Slope)));
        Assert.Equal(LandColors.Of(LandType.Clear), LandColors.Of(LandType.Clear, CellShape.Flat));
        Assert.DoesNotContain(LandColors.Cliff, Enum.GetValues<LandType>().Select(LandColors.Of));
    }
}
