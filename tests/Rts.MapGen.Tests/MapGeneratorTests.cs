using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.Data.Tests;
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
        // Golden value: 64 x 64, two players mirrored, hills, rivers, seed 1. Changes only with the generator's rules. M8 step 10b:
        // the drills of the fields; without them the map is the one of before, byte for byte. 2026-10-24: the playable area of the
        // screen — a 90 x 90 grid with the inscribed square, the starts and rivers in the screen's axes. Loose ends, stage 2: a bridge
        // repair hut by every bridge. Later on 2026-10-24: a base of radius 12, its home field by its edge, the contested field three
        // fifths of the way to the centre. 2026-10-25: the dressing of the climate (a farm and barbed wire in the snow); no building within
        // two cells of the ore zone, the oil derrick out of it.
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Seed = 1 });
        var (none, _) = MapGenerator.Generate(new MapGenSettings { Seed = 1, Drills = DrillKind.None });

        Assert.Equal("527a6a35b04b92d0875aeb17b6400309ba8ea6d9ecdb98a7bfdfa927f7ca952f", MapHasher.Hash(map));
        Assert.Equal("b668d92dc451bcccd6176ba02242c5ef7ad208ec1221c2f8d423903be0cd539c", MapHasher.Hash(none));
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void Every_start_gets_an_ore_drill_at_home_and_a_gem_drill_in_its_contested_field(MapGenSettings settings)
    {
        foreach (var seed in new uint[] { 3, 17, 99 })
        {
            var (map, _) = MapGenerator.Generate(settings with { Seed = seed });
            AssertDrills(map, settings.Players, gems: true, $"seed {seed}");
            Assert.EndsWith("drills OreAndGems.", map.Description);
        }

        var (ore, _) = MapGenerator.Generate(settings with { Seed = 3, Drills = DrillKind.Ore });
        AssertDrills(ore, settings.Players, gems: false, "ore only");
        Assert.DoesNotContain(ore.Objects, o => o.Type == Drills.GemSource);
        Assert.DoesNotContain(MapGenerator.Generate(settings with { Seed = 3, Drills = DrillKind.None }).Map.Objects, o => o.Type is Drills.OreSource or Drills.GemSource);
    }

    [Theory]
    [MemberData(nameof(Sets))]
    public void A_ready_map_made_without_drills_gets_them_in_the_same_fields_and_only_them(MapGenSettings settings)
    {
        var (bare, _) = MapGenerator.Generate(settings with { Seed = 17, Drills = DrillKind.None });

        var drilled = Drills.AddTo(bare, settings.Symmetry, DrillKind.OreAndGems);

        AssertDrills(drilled, settings.Players, gems: true, "added");
        Assert.Equal(bare.Description.TrimEnd('.') + ", drills OreAndGems.", drilled.Description);
        Assert.Equal(bare.Land, drilled.Land);
        Assert.Equal(bare.Heights, drilled.Heights);
        Assert.Equal(bare.Objects.Length + 2 * settings.Players, drilled.Objects.Length);
        var cleared = Enumerable.Range(0, bare.Ore.Length).Where(i => bare.Ore[i] != drilled.Ore[i]).ToList();
        Assert.All(cleared, i => Assert.Contains(drilled.Objects, o => o.Cell.Y * bare.Width + o.Cell.X == i && o.Type is Drills.OreSource or Drills.GemSource));
        Assert.DoesNotContain(MapValidator.Check(drilled), p => p.Message.Contains("drill", StringComparison.OrdinalIgnoreCase) || p.Message.Contains("source", StringComparison.Ordinal));

        var again = Assert.Throws<ArgumentException>(() => Drills.AddTo(drilled, settings.Symmetry, DrillKind.OreAndGems));
        Assert.Contains("has drills already", again.Message);
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
                        foreach (var type in new[] { "pine_tree", Drills.OreSource, Drills.GemSource })
                        {
                            Assert.Equal(objects.Contains((type, cell)), objects.Contains((type, image)));
                        }

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

            // Every map on the first attempt (M7 step 13); the drills of M8 step 10b block no path.
            Assert.Equal(seed, used);
            AssertDrills(map, settings.Players, gems: true, $"seed {seed}");
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
            Assert.Equal(MapGenSettings.GridSize(settings.Size), map.Width);
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
        var simulation = new GameSimulation(new SimulationSettings(seed: 1, grid), GameDataFiles.Rules);
        var sequences = new Dictionary<PlayerId, uint>();
        foreach (var unit in StartSquads.Place(map, grid, GameDataFiles.Rules))
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
            Assert.True(map.Playable!.Margin(c) >= 12, $"{what}: start {start.Slot} at {c} near the edge of the playable area");
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

            // Loose ends, stage 2: a repair hut by every bridge that has a free cell for one in reach; every hut in reach of a bridge.
            Assert.Empty(BridgeHuts.For(map));
            Assert.All(map.Structures.Where(s => s.Type == BridgeHuts.Hut), hut => Assert.Contains(map.Bridges, bridge => MapChecks.BridgeCells(bridge).Exists(cell =>
                Math.Max(Math.Abs(cell.X - hut.Cell.X), Math.Abs(cell.Y - hut.Cell.Y)) <= Rts.Simulation.Systems.BridgeRepair.HutReach)));
        }
    }

    /// <summary>One ore drill in the home field of every start — and a gem drill in its contested field — with no ore under any.</summary>
    private static void AssertDrills(MapDefinition map, int players, bool gems, string what)
    {
        var ore = map.Objects.Where(o => o.Type == Drills.OreSource).ToList();
        var gem = map.Objects.Where(o => o.Type == Drills.GemSource).ToList();
        Assert.True(ore.Count == players && gem.Count == (gems ? players : 0), $"{what}: {ore.Count} ore drills, {gem.Count} gem drills");
        Assert.All(ore.Concat(gem), drill => Assert.Equal(0, map.Ore[drill.Cell.Y * map.Width + drill.Cell.X]));
        foreach (var start in map.Starts)
        {
            // The home field is eight cells from the start diagonally (radius 3), by the edge of the base: its drill within 15 cells.
            Assert.Contains(ore, drill => Distance2(drill.Cell, start.Cell) <= 15 * 15);
        }
    }

    private static int Distance2(CellPos a, CellPos b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

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
