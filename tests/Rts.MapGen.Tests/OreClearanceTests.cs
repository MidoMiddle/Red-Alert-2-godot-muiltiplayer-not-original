using Rts.Data.Maps;
using Rts.Data.Tests;
using Rts.Simulation.Geometry;

namespace Rts.MapGen.Tests;

/// <summary>
/// The owner's rule (2026-10-25): no house on the field where ore is mined, no oil derrick right by it. On every map of the generator
/// — those it makes and the ready ones in game/data/maps — no building stands within <see cref="GenMap.OreClearance"/> cells of the
/// ore zone (the ore and gems and what the drills throw), and no thing of the dressing in it.
/// </summary>
public sealed class OreClearanceTests
{
    [Theory]
    [InlineData(1u, 96, "winter", WaterKind.Rivers)]
    [InlineData(7u, 96, "urban", WaterKind.Rivers)]
    [InlineData(2101u, 96, "temperate", WaterKind.Lakes)]
    [InlineData(351u, 96, "coast", WaterKind.Sea)]
    [InlineData(342u, 128, "urban", WaterKind.Sea)]
    public void No_building_by_the_ore_and_no_thing_in_it(uint seed, int size, string climate, WaterKind water)
    {
        var rotate = size == 128;
        var (map, _) = MapGenerator.Generate(new MapGenSettings { Types = GameDataFiles.Rules.MapTypes,
            Seed = seed, Size = size, Climate = climate, Water = water,
            Symmetry = rotate ? MapSymmetry.Rotate90 : MapSymmetry.Mirror, Players = rotate ? 4 : 2,
        });
        Check(map);
    }

    [Fact]
    public void The_generated_maps_of_the_game_keep_the_rule()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "game", "data", "maps");
        var generated = Directory.GetFiles(folder, "*.rtsmap").Select(file => RtsMapReader.Read(File.ReadAllText(file)))
            .Where(map => map.Author == "Map generator").ToList();
        Assert.NotEmpty(generated);
        Assert.All(generated, Check);
    }

    private static void Check(MapDefinition map)
    {
        var zone = new bool[map.Width * map.Height];
        for (var i = 0; i < zone.Length; i++)
        {
            zone[i] = map.Ore[i] > 0;
        }

        foreach (var drill in map.Objects.Where(o => o.Type is Drills.OreSource or Drills.GemSource))
        {
            for (var dy = -GenMap.DrillThrow; dy <= GenMap.DrillThrow; dy++)
            {
                for (var dx = -GenMap.DrillThrow; dx <= GenMap.DrillThrow; dx++)
                {
                    var (x, y) = (drill.Cell.X + dx, drill.Cell.Y + dy);
                    if (dx * dx + dy * dy <= GenMap.DrillThrow * GenMap.DrillThrow && x >= 0 && y >= 0 && x < map.Width && y < map.Height)
                    {
                        zone[y * map.Width + x] = true;
                    }
                }
            }
        }

        bool Near(CellPos cell, int clearance)
        {
            for (var y = Math.Max(0, cell.Y - clearance); y <= Math.Min(map.Height - 1, cell.Y + clearance); y++)
            {
                for (var x = Math.Max(0, cell.X - clearance); x <= Math.Min(map.Width - 1, cell.X + clearance); x++)
                {
                    if (zone[y * map.Width + x])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        Assert.All(map.Structures, structure => Assert.False(MapChecks.Footprint(structure, GameDataFiles.Rules.MapTypes).Any(cell => Near(cell, GenMap.OreClearance)),
            $"{map.Id}: {structure.Type} at {structure.Cell.X},{structure.Cell.Y} by the ore"));
        Assert.All(map.Objects.Where(o => o.Type is not (Drills.OreSource or Drills.GemSource) && o.Type != "pine_tree"),
            thing => Assert.False(Near(thing.Cell, 0), $"{map.Id}: {thing.Type} at {thing.Cell.X},{thing.Cell.Y} in the ore"));
    }
}
