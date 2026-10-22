using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.MapGen.Tests;

/// <summary>
/// The random map generator (docs/implementation/M7_maps.md, 4.14, step 13): determinism, symmetry, and the properties of
/// 100 seeds in a row for every set of settings (section 6).
/// </summary>
public sealed class MapGeneratorTests
{
    public static readonly TheoryData<MapGenSettings> Sets = new()
    {
        new MapGenSettings { Symmetry = MapSymmetry.Mirror, Relief = ReliefKind.Hilly, Water = WaterKind.Rivers },
        new MapGenSettings { Symmetry = MapSymmetry.Rotate180, Relief = ReliefKind.Mountainous, Water = WaterKind.Lakes },
        new MapGenSettings { Symmetry = MapSymmetry.Rotate90, Players = 4, Relief = ReliefKind.Hilly, Water = WaterKind.Rivers },
        new MapGenSettings { Symmetry = MapSymmetry.Mirror, Relief = ReliefKind.Flat, Water = WaterKind.None },
    };

    public static readonly TheoryData<MapGenSettings> BigSets = new()
    {
        new MapGenSettings { Size = 160, Symmetry = MapSymmetry.Mirror, Relief = ReliefKind.Mountainous, Water = WaterKind.Rivers, Forest = Amount.High },
        new MapGenSettings { Size = 160, Symmetry = MapSymmetry.Rotate180, Relief = ReliefKind.Hilly, Water = WaterKind.Lakes, Resources = Amount.High },
        new MapGenSettings { Size = 128, Symmetry = MapSymmetry.Rotate90, Players = 4, Relief = ReliefKind.Mountainous, Water = WaterKind.Rivers },
    };

    [Fact]
    public void The_same_seed_and_settings_give_the_same_map()
    {
        var settings = new MapGenSettings { Seed = 2024 };

        var (first, seed) = MapGenerator.Generate(settings);
        var (second, _) = MapGenerator.Generate(settings);

        Assert.Equal(RtsMapWriter.Write(first), RtsMapWriter.Write(second));
        Assert.Equal(2024u, seed);
        Assert.Equal("random-2024", first.Id);
        Assert.Contains("seed 2024", first.Description);
        Assert.NotEqual(RtsMapWriter.Write(first), RtsMapWriter.Write(MapGenerator.Generate(settings with { Seed = 2025 }).Map));
    }

    [Fact]
    public void The_reference_map_has_the_same_hash_on_every_system()
    {
        // Golden value: 64 x 64, two players mirrored, hills, rivers, seed 1. Changes only with the generator's rules.
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Seed = 1 });

        Assert.Equal("7b1c56d0d4108f6182f7a3978ff35d72b18784390ec511e3016f086a8137657f", MapHasher.Hash(map));
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Every_map_is_symmetric(MapGenSettings settings)
    {
        foreach (var seed in new uint[] { 3, 17, 99 })
        {
            var (map, _) = MapGenerator.Generate(settings with { Seed = seed });
            var symmetry = new Symmetry(settings.Symmetry, map.Width);
            var objects = map.Objects.Select(o => (o.Type, o.Cell)).ToHashSet();
            var bridgeCells = map.Bridges.SelectMany(MapChecks.BridgeCells).ToHashSet();
            var buildingCells = map.Structures.SelectMany(MapChecks.Footprint).ToHashSet();
            var starts = map.Starts.Select(s => s.Cell).ToHashSet();
            for (var k = 1; k < symmetry.Order; k++)
            {
                for (var y = 0; y <= map.Height; y++)
                {
                    for (var x = 0; x <= map.Width; x++)
                    {
                        var (ix, iy) = symmetry.Corner(k, x, y);
                        Assert.Equal(map.CornerHeight(x, y), map.CornerHeight(ix, iy));
                    }
                }

                for (var y = 0; y < map.Height; y++)
                {
                    for (var x = 0; x < map.Width; x++)
                    {
                        var cell = new CellPos(x, y);
                        var image = symmetry.Cell(k, cell);
                        Assert.Equal(map.LandAt(cell), map.LandAt(image));
                        Assert.Equal(map.Ore[y * map.Width + x], map.Ore[image.Y * map.Width + image.X]);
                        Assert.Equal(objects.Contains(("pine_tree", cell)), objects.Contains(("pine_tree", image)));
                        Assert.Equal(bridgeCells.Contains(cell), bridgeCells.Contains(image));
                        Assert.Equal(buildingCells.Contains(cell), buildingCells.Contains(image));
                    }
                }

                Assert.Equal(map.Starts[k].Cell, symmetry.Cell(k, map.Starts[0].Cell));
                Assert.All(starts, start => Assert.Contains(symmetry.Cell(k, start), starts));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void A_hundred_seeds_in_a_row_give_maps_that_pass_the_checks(MapGenSettings settings)
    {
        for (uint seed = 1; seed <= 100; seed++)
        {
            var (map, used) = MapGenerator.Generate(settings with { Seed = seed });
            AssertGoodMap(map, settings, $"seed {seed} (used {used})");
        }
    }

    [Theory]
    [MemberData(nameof(BigSets))]
    public void Large_maps_pass_the_checks_too(MapGenSettings settings)
    {
        foreach (var seed in new uint[] { 5, 6 })
        {
            var (map, used) = MapGenerator.Generate(settings with { Seed = seed });
            AssertGoodMap(map, settings, $"seed {seed} (used {used})");
            Assert.Equal(settings.Size, map.Width);
        }
    }

    [Fact]
    public void A_failed_check_makes_the_map_again_from_the_next_seed_up_to_ten_times()
    {
        var settings = new MapGenSettings { Seed = 40 };
        var calls = 0;
        var problem = new MapProblem(ProblemLevel.Error, "made up", null);

        var (map, seed) = MapGenerator.Generate(settings, map => ++calls <= 3 ? [problem] : []);

        Assert.Equal(43u, seed);
        Assert.Equal("random-43", map.Id);
        Assert.Contains("seed 43", map.Description);

        var error = Assert.Throws<MapGenException>(() => MapGenerator.Generate(settings, _ => [problem]));
        Assert.Contains("10 seeds from 40", error.Message);
        Assert.Equal(problem, Assert.Single(error.Problems));
    }

    [Theory]
    [InlineData(70, 2, MapSymmetry.Mirror)]
    [InlineData(64, 4, MapSymmetry.Mirror)]
    [InlineData(64, 2, MapSymmetry.Rotate90)]
    public void Settings_that_do_not_go_together_are_refused(int size, int players, MapSymmetry symmetry)
    {
        Assert.Throws<ArgumentException>(() => MapGenerator.Generate(new MapGenSettings { Size = size, Players = players, Symmetry = symmetry }));
    }

    [Fact]
    public void A_generated_map_plays()
    {
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Seed = 8, Symmetry = MapSymmetry.Rotate90, Players = 4 });
        var grid = map.ToMapGrid();
        var simulation = new GameSimulation(new SimulationSettings(seed: 1, grid));
        var sequences = new Dictionary<PlayerId, uint>();
        foreach (var unit in StartSquads.Place(map, grid))
        {
            sequences.TryGetValue(unit.Owner, out var sequence);
            simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequence, unit.Type, unit.Cell.Center));
            sequences[unit.Owner] = sequence + 1;
        }

        simulation.Step();
        var blue = simulation.World.Entities.OfType<Unit>().Where(u => u.Owner == new PlayerId(1)).Select(u => u.Id).ToImmutableArray();
        simulation.Enqueue(new MoveCommand(new PlayerId(1), simulation.CurrentTick, sequences[new PlayerId(1)], blue, new CellPos(32, 32).Center));
        for (var tick = 0; tick < 50; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(4, simulation.World.Entities.OfType<Unit>().Select(u => u.Owner).Distinct().Count());
        Assert.Contains(simulation.World.Entities.OfType<Unit>(), u => u.Owner == new PlayerId(1) && u.MoveTarget is not null);
    }

    private static void AssertGoodMap(MapDefinition map, MapGenSettings settings, string what)
    {
        var problems = MapValidator.Check(map);
        Assert.False(MapValidator.HasErrors(problems), $"{what}: {string.Join("; ", problems.Select(p => p.Message))}");
        Assert.Equal(settings.Players, map.Starts.Length);

        // Ore within 12 cells of every start: exactly the same, the map is symmetric.
        var ore = map.Starts.Select(s => OreNear(map, s.Cell)).ToList();
        Assert.True(ore.All(o => o == ore[0] && o > 0), $"{what}: ore {string.Join(", ", ore)}");

        foreach (var start in map.Starts)
        {
            var c = start.Cell;
            Assert.True(c.X >= 8 && c.Y >= 8 && c.X < map.Width - 8 && c.Y < map.Height - 8, $"{what}: start {start.Slot} at {c} near the edge");
            for (var y = c.Y - 8; y <= c.Y + 8; y++)
            {
                for (var x = c.X - 8; x <= c.X + 8; x++)
                {
                    var (dx, dy) = (x - c.X, y - c.Y);
                    if (map.Contains(new CellPos(x, y)) && dx * dx + dy * dy < 64)
                    {
                        Assert.False(map.LandAt(new CellPos(x, y)) is LandType.Shallow or LandType.Deep, $"{what}: water at {x},{y} near start {start.Slot}");
                    }
                }
            }
        }

        if (settings.Water == WaterKind.Rivers)
        {
            Assert.True(map.Bridges.Length >= 2, $"{what}: {map.Bridges.Length} bridges");
        }
    }

    private static int OreNear(MapDefinition map, CellPos start)
    {
        var sum = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var (dx, dy) = (x - start.X, y - start.Y);
                sum += dx * dx + dy * dy <= MapValidator.OreRadius * MapValidator.OreRadius ? map.Ore[y * map.Width + x] : 0;
            }
        }

        return sum;
    }
}
