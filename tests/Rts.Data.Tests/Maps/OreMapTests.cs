using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>
/// The ore of a map file in a match (docs/implementation/M3_base_economy.md, 4.12, M3 step 9): a density of 1…9 becomes a stage of the
/// 12, gems A–C stages 3, 7 and 11, and every cell of ore of the file is ore of the match.
/// </summary>
public sealed class OreMapTests
{
    [Theory]
    [InlineData(0, OreKind.None, 0)]
    [InlineData(1, OreKind.Ore, 0)]
    [InlineData(2, OreKind.Ore, 1)]
    [InlineData(3, OreKind.Ore, 3)]
    [InlineData(5, OreKind.Ore, 6)]
    [InlineData(7, OreKind.Ore, 8)]
    [InlineData(9, OreKind.Ore, 11)]
    [InlineData(10, OreKind.Gems, 3)]
    [InlineData(11, OreKind.Gems, 7)]
    [InlineData(12, OreKind.Gems, 11)]
    public void A_density_of_the_file_is_a_stage_of_the_match(int value, OreKind kind, int stage)
    {
        Assert.Equal(new OreCell(kind, stage), MapDefinition.OreOf((byte)value));
    }

    [Theory]
    [MemberData(nameof(ReadyMapsTests.Files), MemberType = typeof(ReadyMapsTests))]
    public void Every_cell_of_ore_of_a_ready_map_is_ore_in_the_match(string file)
    {
        var map = RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", file + ".rtsmap")));

        var grid = map.ToMapGrid();

        Assert.NotEqual(0, map.Ore.Count(value => value > 0));
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Assert.Equal(MapDefinition.OreOf(map.Ore[y * map.Width + x]), grid.OreAt(new CellPos(x, y)));
            }
        }
    }
}
