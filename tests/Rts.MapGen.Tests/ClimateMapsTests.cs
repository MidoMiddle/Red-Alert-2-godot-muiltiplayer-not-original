using Rts.Data.Maps;

namespace Rts.MapGen.Tests;

/// <summary>The generator in the climates of RA2: the ground of the climate, a town in the urban one.</summary>
public sealed class ClimateMapsTests
{
    [Theory]
    [InlineData("temperate", 'g')]
    [InlineData("urban", 'g')]
    [InlineData("desert", 'n')]
    public void The_generator_paints_the_ground_of_the_climate(string climate, char plain)
    {
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Seed = 7, Climate = climate });

        Assert.Equal(climate, map.Climate);
        Assert.Equal(ClimateGrounds.Of(climate).Legend, map.TextureLegend);
        Assert.Contains(plain, map.Texture);
        Assert.DoesNotContain(MapValidator.Check(map), problem => problem.Level == ProblemLevel.Error);
        if (climate == "urban")
        {
            // A town: houses by the roads and the pavement along them.
            Assert.True(map.Structures.Count(s => s.Type.StartsWith("neutral_", StringComparison.Ordinal)) >= 4);
            Assert.Contains('l', map.Texture);
        }
    }

    [Fact]
    public void An_unknown_climate_is_refused_by_the_generator()
    {
        Assert.Throws<ArgumentException>(() => MapGenerator.Generate(new MapGenSettings { Seed = 7, Climate = "lunar" }));
    }
}
