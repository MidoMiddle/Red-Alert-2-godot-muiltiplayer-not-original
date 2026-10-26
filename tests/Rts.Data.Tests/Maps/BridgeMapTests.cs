using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;

namespace Rts.Data.Tests.Maps;

/// <summary>Bridges of a <c>.rtsmap</c> file in the simulation (docs/implementation/M7_maps.md, 4.6, step 6).</summary>
public sealed class BridgeMapTests
{
    // 8 x 8: a river down columns 3 and 4, a bridge across it on row 2.
    private const string River = """
        . . . ~ W . . .
        . . . ~ W . . .
        . . . ~ W . . .
        . . . ~ W . . .
        . . . ~ W . . .
        . . . ~ W . . .
        . . . ~ W . . .
        . . . ~ W . . .
        bridge bridge_wood_low 3 2 4 2
        """;

    private static string RiverText() => RtsMapWriter.Write(TestMapConverter.Convert(TestMapParser.Parse(River), "river", "River"));

    [Fact]
    public void A_bridge_of_the_file_becomes_a_bridge_of_the_map_by_the_table()
    {
        var text = RiverText();
        Assert.Contains("  - { type: bridge_wood_low, from: [3, 2], to: [4, 2] }", text);

        var grid = RtsMapReader.Read(text).ToMapGrid();

        var bridge = Assert.Single(grid.Bridges);
        Assert.Equal(BridgeKind.WoodLow, bridge.Kind);
        Assert.Equal(new[] { new CellPos(3, 2), new CellPos(4, 2) }, bridge.Segments.Select(s => s.Cell));
        Assert.All(bridge.Segments, s => Assert.Equal(600, s.Hp));
        Assert.True(grid.CanEnter(new CellPos(4, 2), Locomotor.Wheel));
        Assert.False(grid.CanEnter(new CellPos(4, 2), Locomotor.Naval));
        Assert.True(grid.ContentEquals(TestMapParser.Parse(River).Grid));
    }

    [Fact]
    public void A_bridge_type_the_table_does_not_know_is_an_error()
    {
        var map = RtsMapReader.Read(RiverText().Replace("type: bridge_wood_low", "type: bridge_stone_high"));

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid());

        Assert.Contains("bridge_stone_high", error.Message);
    }

    [Fact]
    public void Facing_corners_has_the_high_bridges_of_RA2_over_its_valleys()
    {
        var map = RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "facing-corners.rtsmap")));
        var grid = map.ToMapGrid();

        var high = grid.Bridges.Where(b => b.Type.High).ToList();
        Assert.Equal(8, high.Count);
        Assert.Equal(4, grid.Bridges.Count(b => !b.Type.High));
        Assert.All(high, b => Assert.Equal(4, grid.DeckLevel(b.Segments[0].Cell)));
        Assert.All(high, b => Assert.All(b.Segments, s => Assert.Equal(3, s.Cells.Length)));
        // Under a deck lies the valley RA2 has there, not water poured in by the old import.
        Assert.Contains(high, b => b.Segments.Any(s => grid.CanEnter(s.Cell, Locomotor.Track)));

        // The bases reach each other over the bridges, and a tank of a base reaches the valley under one.
        var from = map.Starts[0].Cell;
        foreach (var start in map.Starts.Skip(1))
        {
            Assert.Equal(PathStatus.Found, AStar.FindPath(grid, from, start.Cell, Locomotor.Track, AStar.DefaultMaxExpansions * 20).Status);
        }

        var valley = high.SelectMany(b => b.Segments).First(s => grid.CanEnter(s.Cell, Locomotor.Track)).Cell;
        Assert.Equal(PathStatus.Found, AStar.FindPath(grid, from, valley, Locomotor.Track, AStar.DefaultMaxExpansions * 20).Status);
    }

    [Fact]
    public void A_bridge_without_a_shore_at_both_ends_is_an_error()
    {
        // One segment on the shallows: deep water on the east, shallows north and south, so no shore along X or along Y.
        var map = RtsMapReader.Read(RiverText().Replace("from: [3, 2], to: [4, 2]", "from: [3, 2], to: [3, 2]"));

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid());

        Assert.Contains("no shore", error.Message);
    }
}
