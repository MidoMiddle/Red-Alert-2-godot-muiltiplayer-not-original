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

    [Theory]
    [InlineData("urban", new[] { "bench", "traffic_light" }, new[] { "neutral_shop", "neutral_beach_cabin" })]
    [InlineData("temperate", new[] { "hedge", "hay_bale", "well" }, new[] { "neutral_farmhouse" })]
    [InlineData("desert", new[] { "dry_shrub", "well", "barbed_wire" }, new[] { "neutral_adobe_house", "neutral_rock_mesa" })]
    [InlineData("winter", new[] { "barbed_wire" }, new[] { "neutral_farmhouse" })]
    public void The_generator_dresses_a_map_of_its_climate(string climate, string[] objects, string[] buildings)
    {
        // 2026-10-25: the objects of the element documents by the climate (Stages/Dressing), every one with its images.
        var settings = new MapGenSettings { Seed = 2101, Size = 96, Climate = climate, Water = WaterKind.Rivers };
        var (map, _) = MapGenerator.Generate(settings);

        Assert.DoesNotContain(MapValidator.Check(map), problem => problem.Level == ProblemLevel.Error);
        Assert.All(objects, id => Assert.Contains(map.Objects, o => o.Type == id));
        Assert.All(buildings, id => Assert.Contains(map.Structures, s => s.Type == id));
        var symmetry = new Symmetry(settings.Symmetry, map.Width);
        foreach (var placed in map.Objects.Where(o => objects.Contains(o.Type)))
        {
            Assert.Contains(map.Objects, o => o.Type == placed.Type && o.Cell == symmetry.Cell(1, placed.Cell));
        }

        // Nothing of the dressing in a base.
        var dressing = map.Objects.Where(o => objects.Contains(o.Type)).Select(o => o.Cell)
            .Concat(map.Structures.Where(s => buildings.Contains(s.Type)).Select(s => s.Cell));
        Assert.All(dressing, cell => Assert.All(map.Starts, start =>
            Assert.True((cell.X - start.Cell.X) * (cell.X - start.Cell.X) + (cell.Y - start.Cell.Y) * (cell.Y - start.Cell.Y) > 14 * 14)));
    }

    [Fact]
    public void An_unknown_climate_is_refused_by_the_generator()
    {
        Assert.Throws<ArgumentException>(() => MapGenerator.Generate(new MapGenSettings { Seed = 7, Climate = "lunar" }));
    }
}
