using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

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
    public void A_bridge_without_a_shore_at_both_ends_is_an_error()
    {
        // One segment on the shallows: deep water on the east, shallows north and south, so no shore along X or along Y.
        var map = RtsMapReader.Read(RiverText().Replace("from: [3, 2], to: [4, 2]", "from: [3, 2], to: [3, 2]"));

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid());

        Assert.Contains("no shore", error.Message);
    }
}
