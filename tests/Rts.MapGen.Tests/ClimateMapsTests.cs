using Rts.Data.Maps;

namespace Rts.MapGen.Tests;

/// <summary>The generator in the climates of RA2: the ground of the climate, a town in the urban one.</summary>
public sealed class ClimateMapsTests
{
    [Theory]
    [InlineData("temperate", 'g')]
    [InlineData("urban", 'g')]
    [InlineData("desert", 'n')]
    [InlineData("coast", 'g')]
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
    [InlineData("coast", new[] { "beach_umbrella", "sun_lounger" }, new[] { "neutral_lighthouse", "neutral_boat_shed", "neutral_hotel", "neutral_beach_cabin" })]
    public void The_generator_dresses_a_map_of_its_climate(string climate, string[] objects, string[] buildings)
    {
        // 2026-10-25: the objects of the element documents by the climate (Stages/Dressing), every one with its images.
        var water = climate == "coast" ? WaterKind.Sea : WaterKind.Rivers;
        var settings = new MapGenSettings { Seed = 2101, Size = 96, Climate = climate, Water = water };
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

    [Theory]
    [InlineData(MapSymmetry.Mirror, 311u, "coast", 2)]
    [InlineData(MapSymmetry.Rotate90, 313u, "coast", 4)]
    [InlineData(MapSymmetry.Mirror, 341u, "urban", 2)]
    public void The_sea_runs_round_the_playable_area_and_every_base_lies_near_it(MapSymmetry symmetry, uint seed, string climate, int isletDerricks)
    {
        var settings = new MapGenSettings { Seed = seed, Size = symmetry == MapSymmetry.Rotate90 ? 128 : 96, Symmetry = symmetry,
                                            Players = symmetry == MapSymmetry.Rotate90 ? 4 : 2, Climate = climate, Water = WaterKind.Sea };
        var (map, _) = MapGenerator.Generate(settings);
        var grid = map.ToMapGrid();

        // Water on every side of the area, near its edge; the bases stay reachable from each other by land.
        Assert.Empty(MapValidator.Check(map));
        var area = map.Playable!;
        var edge = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new Rts.Simulation.Geometry.CellPos(x, y)))
            .Where(cell => area.Margin(cell) == 2).ToList();
        Assert.True(edge.Count(cell => grid.LandAt(cell) is Rts.Simulation.Map.LandType.Shallow or Rts.Simulation.Map.LandType.Deep) * 10 > edge.Count * 7);
        Assert.All(map.Starts, start => Assert.Contains(edge.Concat(Water(grid)), cell =>
            grid.LandAt(cell) is Rts.Simulation.Map.LandType.Shallow or Rts.Simulation.Map.LandType.Deep
            && (cell.X - start.Cell.X) * (cell.X - start.Cell.X) + (cell.Y - start.Cell.Y) * (cell.Y - start.Cell.Y) <= 28 * 28));

        // The owner's wish (2026-10-25): out at sea an islet with oil derricks — one with a pair for a mirror, one on each of four.
        var atSea = map.Structures.Where(s => s.Type == "tech_oil_derrick" && grid.Heights.CornerLevel(s.Cell.X, s.Cell.Y) == 0
                                              && Enumerable.Range(-3, 8).Any(d => grid.LandAt(new Rts.Simulation.Geometry.CellPos(s.Cell.X + d, s.Cell.Y)) is Rts.Simulation.Map.LandType.Shallow or Rts.Simulation.Map.LandType.Deep));
        Assert.Equal(isletDerricks, atSea.Count());
    }

    [Fact]
    public void A_sea_of_a_fifth_leaves_four_fifths_land_with_a_resort_of_two_hotels_and_small_houses()
    {
        // The owner's wish (2026-10-25): 80 % land, 20 % water; a couple of big buildings, the rest small resort houses.
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Seed = 351, Size = 96, Climate = "coast", Water = WaterKind.Sea, SeaPercent = 20 });
        var grid = map.ToMapGrid();
        var area = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new Rts.Simulation.Geometry.CellPos(x, y)))
            .Where(map.Playable!.Contains).ToList();
        var water = area.Count(cell => grid.LandAt(cell) is Rts.Simulation.Map.LandType.Shallow or Rts.Simulation.Map.LandType.Deep) * 100 / area.Count;
        Assert.InRange(water, 16, 24);
        Assert.Equal(2, map.Structures.Count(s => s.Type == "neutral_hotel"));
        Assert.True(map.Structures.Count(s => s.Type is "neutral_beach_cabin" or "neutral_kiosk" or "neutral_cafe") >= 12);
        Assert.Contains("water Sea 20%", map.Description, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => MapGenerator.Generate(new MapGenSettings { Seed = 1, Water = WaterKind.Rivers, SeaPercent = 20 }));
    }

    [Theory]
    [InlineData(MapSymmetry.Rotate180, 2, 326u)]
    [InlineData(MapSymmetry.Rotate90, 4, 327u)]
    public void Every_player_has_a_raised_island_with_two_ramps_a_derrick_and_islets_with_derricks_at_sea(MapSymmetry symmetry, int players, uint seed)
    {
        // The owner's wish (2026-10-25): an island for every player, raised, two ramps down to a beach; a derrick on every island
        // and a few islets with one at sea.
        var settings = new MapGenSettings { Seed = seed, Size = players == 4 ? 128 : 96, Symmetry = symmetry, Players = players, Climate = "coast",
                                            Water = WaterKind.Islands };
        var (map, _) = MapGenerator.Generate(settings);
        var grid = map.ToMapGrid();

        // No other start by land, every one over the water: a warning, not an error.
        var problems = MapValidator.Check(map);
        Assert.DoesNotContain(problems, problem => problem.Level == ProblemLevel.Error);
        Assert.Equal(players - 1, problems.Count(problem => problem.Message.Contains("only over the water", StringComparison.Ordinal)));

        // The island is raised: its start stands at the level of the top.
        Assert.All(map.Starts, start => Assert.Equal(4, grid.Heights.CornerLevel(start.Cell.X, start.Cell.Y)));

        // Two ramps an island: the land at sea level at the foot of each, a patch of its own (with its beach), as every islet.
        var low = Patches(map, cell => grid.LandAt(cell) is not (Rts.Simulation.Map.LandType.Shallow or Rts.Simulation.Map.LandType.Deep)
                                       && grid.Heights.CornerLevel(cell.X, cell.Y) == 0 && grid.Heights.CornerLevel(cell.X + 1, cell.Y) == 0
                                       && grid.Heights.CornerLevel(cell.X, cell.Y + 1) == 0 && grid.Heights.CornerLevel(cell.X + 1, cell.Y + 1) == 0);
        var derricks = map.Structures.Where(s => s.Type == "tech_oil_derrick").ToList();
        var islets = derricks.Count - players;
        Assert.True(islets >= 2, $"{islets} islets");
        Assert.Equal(2 * players + islets, low);

        // A derrick on every island: on its top.
        Assert.Equal(players, derricks.Count(d => grid.Heights.CornerLevel(d.Cell.X, d.Cell.Y) == 4));
    }

    private static IEnumerable<Rts.Simulation.Geometry.CellPos> Water(Rts.Simulation.Map.MapGrid grid) =>
        Enumerable.Range(0, grid.Width).SelectMany(x => Enumerable.Range(0, grid.Height).Select(y => new Rts.Simulation.Geometry.CellPos(x, y)))
            .Where(cell => grid.LandAt(cell) is Rts.Simulation.Map.LandType.Shallow or Rts.Simulation.Map.LandType.Deep);

    // The patches of the cells that pass the test, eight neighbours apart.
    private static int Patches(MapDefinition map, Func<Rts.Simulation.Geometry.CellPos, bool> test)
    {
        var seen = new bool[map.Width * map.Height];
        var count = 0;
        for (var i = 0; i < seen.Length; i++)
        {
            var first = new Rts.Simulation.Geometry.CellPos(i % map.Width, i / map.Width);
            if (seen[i] || !test(first))
            {
                continue;
            }

            count++;
            var queue = new Queue<Rts.Simulation.Geometry.CellPos>([first]);
            seen[i] = true;
            while (queue.TryDequeue(out var cell))
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var next = new Rts.Simulation.Geometry.CellPos(cell.X + dx, cell.Y + dy);
                        if (next.X >= 0 && next.Y >= 0 && next.X < map.Width && next.Y < map.Height && !seen[next.Y * map.Width + next.X] && test(next))
                        {
                            seen[next.Y * map.Width + next.X] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }
        }

        return count;
    }

    [Fact]
    public void An_unknown_climate_is_refused_by_the_generator()
    {
        Assert.Throws<ArgumentException>(() => MapGenerator.Generate(new MapGenSettings { Seed = 7, Climate = "lunar" }));
    }
}
