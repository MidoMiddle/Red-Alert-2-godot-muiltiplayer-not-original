using System.Linq;
using Rts.Data.Maps;
using Rts.Data.Tests;
using Rts.Simulation.Map;

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
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = 7, Climate = climate });

        Assert.Equal(climate, map.Climate);
        Assert.Equal(ClimateGrounds.Of(climate).Legend.OrderBy(entry => entry.Code), map.TextureLegend);
        Assert.Contains(plain, map.Texture);
        Assert.DoesNotContain(MapValidator.Check(map, GameDataFiles.Rules.MapTypes), problem => problem.Level == ProblemLevel.Error);
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
        var settings = new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = 2101, Size = 96, Climate = climate, Water = water };
        var (map, _) = MapGenerator.Generate(settings);

        Assert.DoesNotContain(MapValidator.Check(map, GameDataFiles.Rules.MapTypes), problem => problem.Level == ProblemLevel.Error);
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
        var settings = new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = seed, Size = symmetry == MapSymmetry.Rotate90 ? 128 : 96, Symmetry = symmetry,
                                            Players = symmetry == MapSymmetry.Rotate90 ? 4 : 2, Climate = climate, Water = WaterKind.Sea };
        var (map, _) = MapGenerator.Generate(settings);
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);

        // Water on every side of the area, near its edge; the bases stay reachable from each other by land.
        Assert.Empty(MapValidator.Check(map, GameDataFiles.Rules.MapTypes));
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
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = 351, Size = 96, Climate = "coast", Water = WaterKind.Sea, SeaPercent = 20 });
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);
        var area = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new Rts.Simulation.Geometry.CellPos(x, y)))
            .Where(map.Playable!.Contains).ToList();
        var water = area.Count(cell => grid.LandAt(cell) is Rts.Simulation.Map.LandType.Shallow or Rts.Simulation.Map.LandType.Deep) * 100 / area.Count;
        Assert.InRange(water, 16, 24);
        Assert.Equal(2, map.Structures.Count(s => s.Type == "neutral_hotel"));
        Assert.True(map.Structures.Count(s => s.Type is "neutral_beach_cabin" or "neutral_kiosk" or "neutral_cafe") >= 12);
        Assert.Contains("water Sea 20%", map.Description, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => MapGenerator.Generate(new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = 1, Water = WaterKind.Rivers, SeaPercent = 20 }));
    }

    [Theory]
    [InlineData(MapSymmetry.Rotate180, 2, 326u)]
    [InlineData(MapSymmetry.Rotate90, 4, 327u)]
    public void Every_player_has_a_raised_island_with_two_ramps_a_derrick_and_islets_with_derricks_at_sea(MapSymmetry symmetry, int players, uint seed)
    {
        // The owner's wish (2026-10-25): an island for every player, raised, two ramps down to a beach; a derrick on every island
        // and a few islets with one at sea.
        var settings = new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = seed, Size = players == 4 ? 128 : 96, Symmetry = symmetry, Players = players, Climate = "coast",
                                            Water = WaterKind.Islands };
        var (map, _) = MapGenerator.Generate(settings);
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);

        // No other start by land, every one over the water: a warning, not an error.
        var problems = MapValidator.Check(map, GameDataFiles.Rules.MapTypes);
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
        Assert.Throws<ArgumentException>(() => MapGenerator.Generate(new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = 7, Climate = "lunar" }));
    }

    [Theory]
    [InlineData("urban", LandType.Road, LandType.Track)]
    [InlineData("winter", LandType.Track, LandType.Road)]
    [InlineData("temperate", LandType.Track, LandType.Road)]
    [InlineData("desert", LandType.Track, LandType.Road)]
    [InlineData("coast", LandType.Track, LandType.Road)]
    public void The_main_road_is_asphalt_in_a_town_and_the_unpaved_road_elsewhere(string climate, LandType expected, LandType other)
    {
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Types = GameDataFiles.Rules.MapTypes, Seed = 7, Climate = climate });

        Assert.Contains(expected, map.Land);
        Assert.DoesNotContain(other, map.Land);
    }

    [Theory]
    [InlineData("coast", 311u)]
    [InlineData("urban", 341u)]
    public void A_harbour_on_the_sea_has_a_quay_wall_two_levels_over_the_water_and_its_docks_on_the_paving(string climate, uint seed)
    {
        // The owner (2026-11-01): a harbour on the coast (docs/naval_elements/09_quay_wall) — the wall a cliff of two levels straight into
        // the water, a quay edge on every cell of it, cranes, containers and a warehouse on the paved apron behind.
        var types = GameDataFiles.Rules.MapTypes;
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Types = types, Seed = seed, Size = 64, Climate = climate, Water = WaterKind.Sea });
        var grid = map.ToMapGrid(types);
        var quay = map.TextureLegend.Single(e => e.Material == "quay").Code;
        var edges = map.Objects.Where(o => o.Type == "quay_edge").ToList();

        Assert.Empty(MapValidator.Check(map, types));
        Assert.Equal(2 * Stages.Harbour.Length, edges.Count);
        var symmetry = new Symmetry(MapSymmetry.Mirror, map.Width);
        Assert.All(edges, edge =>
        {
            Assert.Equal(quay, map.Texture[edge.Cell.Y * map.Width + edge.Cell.X]);
            Assert.Equal(CellShape.Cliff, grid.Heights.ShapeAt(edge.Cell));
            var levels = new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.Select(c => grid.Heights.CornerLevel(edge.Cell.X + c.Item1, edge.Cell.Y + c.Item2)).ToList();
            Assert.Equal((0, Stages.Harbour.Level), (levels.Min(), levels.Max()));
            Assert.Contains(new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }, d => Wet(grid, new Rts.Simulation.Geometry.CellPos(edge.Cell.X + d.Item1, edge.Cell.Y + d.Item2)));
            Assert.Contains(edges, o => o.Cell == symmetry.Cell(1, edge.Cell));
        });

        foreach (var id in new[] { "neutral_port_crane", "neutral_container_stack", "neutral_port_warehouse" })
        {
            var docks = map.Structures.Where(s => s.Type == id).ToList();
            Assert.True(docks.Count >= 2, id);
            Assert.All(docks, dock => Assert.All(MapChecks.Footprint(dock, types), cell => Assert.Equal(quay, map.Texture[cell.Y * map.Width + cell.X])));
        }

        // Nothing else on the paving: no resort, no trees.
        Assert.DoesNotContain(map.Structures, s => s.Type.StartsWith("neutral_beach", StringComparison.Ordinal)
                                                   && MapChecks.Footprint(s, types).Exists(cell => map.Texture[cell.Y * map.Width + cell.X] == quay));
        Assert.DoesNotContain(map.Objects, o => o.Type != "quay_edge" && map.Texture[o.Cell.Y * map.Width + o.Cell.X] == quay);
    }

    [Theory]
    [InlineData("coast", WaterKind.Sea, MapSymmetry.Mirror, 311u)]
    [InlineData("coast", WaterKind.Islands, MapSymmetry.Rotate180, 326u)]
    [InlineData("winter", WaterKind.Sea, MapSymmetry.Mirror, 9u)]
    [InlineData("urban", WaterKind.Sea, MapSymmetry.Rotate90, 313u)]
    public void The_sea_gets_platforms_ships_reefs_and_floes_that_close_no_way(string climate, WaterKind water, MapSymmetry symmetry, uint seed)
    {
        // The owner (2026-11-01): reefs on the coast, floes in the snow, platforms out at sea and ships off the quays and beaches. Each on
        // the water with a ring of open water round it, so a fleet goes round, and with its images.
        var types = GameDataFiles.Rules.MapTypes;
        var players = symmetry == MapSymmetry.Rotate90 ? 4 : 2;
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Types = types, Seed = seed, Size = players == 4 ? 96 : 64, Players = players,
                                                                    Symmetry = symmetry, Climate = climate, Water = water });
        var grid = map.ToMapGrid(types);
        var sym = new Symmetry(symmetry, map.Width);
        var pieces = map.Structures.Where(s => s.Type is "tech_offshore_platform" or "neutral_cargo_ship" or "neutral_ferry" or "neutral_fishing_boat")
            .Select(s => (s.Type, Cells: MapChecks.Footprint(s, types)))
            .Concat(map.Objects.Where(o => o.Type is "reef_rocks" or "ice_floe").Select(o => (o.Type, Cells: new List<Rts.Simulation.Geometry.CellPos> { o.Cell })))
            .ToList();
        var blocked = pieces.SelectMany(p => p.Cells).ToHashSet();

        Assert.Contains(pieces, p => p.Type == "tech_offshore_platform");
        Assert.Equal(climate == "coast", pieces.Any(p => p.Type == "reef_rocks"));
        Assert.Equal(climate == "winter", pieces.Any(p => p.Type == "ice_floe"));
        Assert.Equal(climate != "winter", pieces.Any(p => p.Type == "neutral_fishing_boat"));
        Assert.All(pieces, piece =>
        {
            foreach (var cell in piece.Cells)
            {
                Assert.True(Wet(grid, cell), $"{piece.Type} at {cell.X},{cell.Y} is not on the water");
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var ring = new Rts.Simulation.Geometry.CellPos(cell.X + dx, cell.Y + dy);
                        Assert.True(Wet(grid, ring) && (piece.Cells.Contains(ring) || !blocked.Contains(ring)), $"{piece.Type} at {cell.X},{cell.Y}: its ring is closed");
                    }
                }

                Assert.Contains(sym.Cell(1, cell), blocked);
            }
        });
        Assert.DoesNotContain(MapValidator.Check(map, types), p => p.Level == ProblemLevel.Error);
    }

    [Fact]
    public void The_sea_added_to_a_map_made_before_keeps_the_rest_of_it()
    {
        // The ready maps get the harbour and the sea pieces too (the owner, 2026-11-01; the map tool's add-sea): a map with no sea
        // stays as it was, one of islands keeps all it had and gains only pieces of the sea.
        var types = GameDataFiles.Rules.MapTypes;
        var rivers = new MapGenSettings { Types = types, Seed = 1 };
        var (plain, _) = MapGenerator.Generate(rivers);
        Assert.Equal(RtsMapWriter.Write(plain), RtsMapWriter.Write(MapGenerator.AddSea(plain, rivers)));

        var islands = new MapGenSettings { Types = types, Seed = 326, Size = 64, Symmetry = MapSymmetry.Rotate180, Climate = "coast", Water = WaterKind.Islands };
        var (isles, _) = MapGenerator.Generate(islands);
        var more = MapGenerator.AddSea(isles, islands);
        var sea = new[] { "reef_rocks", "tech_offshore_platform", "neutral_ferry", "neutral_fishing_boat" };
        Assert.True(isles.Land.SequenceEqual(more.Land) && isles.Heights.SequenceEqual(more.Heights) && isles.Texture.SequenceEqual(more.Texture));
        Assert.All(isles.Objects, o => Assert.Contains(o, more.Objects));
        Assert.All(isles.Structures, s => Assert.Contains(s, more.Structures));
        Assert.All(more.Objects.Except(isles.Objects), o => Assert.Contains(o.Type, sea));
        Assert.All(more.Structures.Except(isles.Structures), s => Assert.Contains(s.Type, sea));
        Assert.DoesNotContain(MapValidator.Check(more, types), p => p.Level == ProblemLevel.Error);
    }

    private static bool Wet(MapGrid grid, Rts.Simulation.Geometry.CellPos cell) =>
        cell.X >= 0 && cell.Y >= 0 && cell.X < grid.Width && cell.Y < grid.Height && grid.LandAt(cell) is LandType.Shallow or LandType.Deep;
}
