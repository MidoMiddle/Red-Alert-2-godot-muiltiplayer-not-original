using Rts.Data.Maps;
using Rts.MapTool;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>Heights from the file to the simulation (docs/implementation/M7_maps.md, 4.1, step 2), on the relief test map.</summary>
public sealed class ReliefMapTests
{
    private static string ReliefPath => Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "relief_test.rtsmap");

    private static MapGrid Relief() => RtsMapReader.Read(File.ReadAllText(ReliefPath)).ToMapGrid([]);

    [Fact]
    public void The_committed_relief_map_is_what_the_generator_writes()
    {
        Assert.Equal(RtsMapWriter.Write(ReliefTestMap.Build()), File.ReadAllText(ReliefPath).Replace("\r\n", "\n"));
    }

    [Fact]
    public void Heights_reach_the_simulation_corner_by_corner()
    {
        var map = RtsMapReader.Read(File.ReadAllText(ReliefPath));
        var grid = map.ToMapGrid([]);

        for (var y = 0; y <= map.Height; y++)
        {
            for (var x = 0; x <= map.Width; x++)
            {
                Assert.Equal(map.CornerHeight(x, y), grid.Heights.CornerLevel(x, y));
            }
        }
    }

    [Theory]
    [InlineData("plateau", CellShape.Flat, 4)]
    [InlineData("east ramp", CellShape.Slope, 2)]
    [InlineData("south ramp", CellShape.Slope, 2)]
    [InlineData("plateau wall", CellShape.Cliff, 0)]
    [InlineData("shelf", CellShape.Flat, 2)]
    [InlineData("shelf edge", CellShape.Cliff, 0)]
    [InlineData("hill top", CellShape.Slope, 5)]
    [InlineData("pit", CellShape.Flat, 0)]
    [InlineData("pit ramp", CellShape.Slope, 0)]
    [InlineData("corner ramp", CellShape.Slope, 0)]
    public void Each_feature_has_the_shape_it_was_built_with(string feature, CellShape shape, int level)
    {
        var cell = feature switch
        {
            "plateau" => ReliefTestMap.PlateauCell,
            "east ramp" => ReliefTestMap.EastRampCell,
            "south ramp" => ReliefTestMap.SouthRampCell,
            "plateau wall" => ReliefTestMap.PlateauWallCell,
            "shelf" => ReliefTestMap.ShelfCell,
            "shelf edge" => ReliefTestMap.ShelfEdgeCell,
            "hill top" => ReliefTestMap.HillTopCell,
            "pit" => ReliefTestMap.PitCell,
            "pit ramp" => ReliefTestMap.PitRampCell,
            _ => ReliefTestMap.CornerRampCell,
        };
        var heights = Relief().Heights;

        Assert.Equal((shape, level), (heights.ShapeAt(cell), heights.CellLevel(cell)));
    }

    [Fact]
    public void Trees_of_the_sandbox_become_obstacles_and_land_stays()
    {
        var map = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath));

        var grid = map.ToMapGrid([TestMapConverter.PineTree]);
        var noTrees = map.ToMapGrid([]);

        Assert.All(map.Objects, tree => Assert.True(grid.IsBlocked(tree.Cell)));
        Assert.Equal(map.Objects.Length, Enumerable.Range(0, 64 * 64).Count(i => grid.IsBlocked(new CellPos(i % 64, i / 64))));
        Assert.DoesNotContain(Enumerable.Range(0, 64 * 64), i => noTrees.IsBlocked(new CellPos(i % 64, i / 64)));
        Assert.Equal(map.LandAt(new CellPos(33, 32)), grid.LandAt(new CellPos(33, 32)));
    }

    [Fact]
    public void Point_heights_on_the_relief_map_are_the_same_on_every_system()
    {
        // Golden values: Fixed raw values (leptons × 65 536) at points across ramps, the hill and a cliff edge.
        var heights = Relief().Heights;
        (int X, int Y)[] points = [(9 * 256 + 128, 4 * 256 + 128), (5 * 256 + 77, 9 * 256 + 200), (17 * 256 + 31, 16 * 256 + 250),
            (4 * 256 + 255, 1 * 256 + 255), (11 * 256 + 3, 1 * 256 + 9), (20 * 256, 16 * 256 + 128)];

        var actual = points.Select(p => heights.HeightAt(new WorldPos(p.X, p.Y)).Raw).ToArray();

        Assert.Equal(new long[] { 17039360, 15122432, 34219120, 27156480, 6499064, 20447232 }, actual);
    }
}
