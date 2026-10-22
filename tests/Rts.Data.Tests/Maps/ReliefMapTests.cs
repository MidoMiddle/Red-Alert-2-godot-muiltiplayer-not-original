using Rts.Data.Maps;
using Rts.MapTool;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>Heights from the file to the simulation (docs/implementation/M7_maps.md, 4.1, step 2), on the relief test map.</summary>
public sealed class ReliefMapTests
{
    private static string ReliefPath => Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "relief_test.rtsmap");

    private static MapGrid Relief() => RtsMapReader.Read(File.ReadAllText(ReliefPath)).ToMapGrid();

    [Fact]
    public void The_committed_relief_map_is_what_the_generator_writes()
    {
        Assert.Equal(RtsMapWriter.Write(ReliefTestMap.Build()), File.ReadAllText(ReliefPath).Replace("\r\n", "\n"));
    }

    [Fact]
    public void Heights_reach_the_simulation_corner_by_corner()
    {
        var map = RtsMapReader.Read(File.ReadAllText(ReliefPath));
        var grid = map.ToMapGrid();

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
    public void Map_objects_reach_the_simulation_by_the_table_of_types()
    {
        var map = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath));

        var grid = map.ToMapGrid();

        Assert.Equal(map.Objects.Select(o => (o.Type, o.Cell, o.Variant)), grid.Objects.Select(o => (o.Type.Id, o.Cell, o.Variant)));
        Assert.All(grid.Objects, o => Assert.Equal(o.Type.MaxHp, o.Hp));
        Assert.All(grid.Objects.Where(o => o.Kind == MapObjectKind.PineTree), tree => Assert.False(grid.CanEnter(tree.Cell, Locomotor.Track)));
        Assert.All(grid.Objects.Where(o => o.Kind == MapObjectKind.FenceWood), fence =>
        {
            Assert.True(grid.CanEnter(fence.Cell, Locomotor.Track));
            Assert.False(grid.CanEnter(fence.Cell, Locomotor.Foot));
        });
        Assert.Equal(map.LandAt(new CellPos(33, 32)), grid.LandAt(new CellPos(33, 32)));
    }

    [Fact]
    public void An_object_type_the_table_does_not_know_is_an_error()
    {
        var text = RtsMapWriter.Write(SampleMaps.Full()).Replace("type: pine_tree, cell: [1, 3]", "type: palm_tree, cell: [1, 3]");
        var map = RtsMapReader.Read(text);

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid());

        Assert.Contains("palm_tree", error.Message);
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

    [Theory]
    [InlineData(4, 4, 2)]   // the level-4 plateau
    [InlineData(18, 4, 1)]  // the level-2 shelf
    [InlineData(17, 16, 2)] // the top of the stepped hill: its corners are 5 and 6, a slope counts by its foot
    [InlineData(4, 16, 0)]  // the flat bottom of the pit
    public void On_the_relief_map_height_adds_to_sight(int x, int y, int bonus)
    {
        var grid = Relief();

        Assert.Equal(8 + bonus, Rts.Simulation.Visibility.SightRules.SightAt(grid, new CellPos(x, y), 8));
    }
}
