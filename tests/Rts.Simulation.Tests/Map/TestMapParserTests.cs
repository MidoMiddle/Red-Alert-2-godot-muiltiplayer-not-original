using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

public sealed class TestMapParserTests
{
    /// <summary>Reads a map from tests/Rts.Simulation.Tests/Maps, copied next to the test binary.</summary>
    internal static TestMap Load(string name) =>
        TestMapParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Maps", name)));

    [Fact]
    public void Every_code_maps_to_its_land_type()
    {
        var map = TestMapParser.Parse(". , = b ~ W # i T");

        LandType[] expected =
        [
            LandType.Clear, LandType.Rough, LandType.Road, LandType.Beach,
            LandType.Shallow, LandType.Deep, LandType.Rock, LandType.Ice, LandType.Clear,
        ];
        Assert.Equal((9, 1), (map.Grid.Width, map.Grid.Height));
        Assert.Equal(expected, Enumerable.Range(0, 9).Select(x => map.Grid.LandAt(new CellPos(x, 0))));
        Assert.Equal(
            Enumerable.Range(0, 9).Select(x => x == 8),
            Enumerable.Range(0, 9).Select(x => map.Grid.IsBlocked(new CellPos(x, 0))));
    }

    [Fact]
    public void Codes_round_trip_through_code_of()
    {
        foreach (var land in Enum.GetValues<LandType>())
        {
            var map = TestMapParser.Parse(TestMapParser.CodeOf(land).ToString());
            Assert.Equal(land, map.Grid.LandAt(new CellPos(0, 0)));
        }
    }

    [Fact]
    public void Rows_go_down_the_map_and_comments_and_blank_lines_are_skipped()
    {
        var map = TestMapParser.Parse("// header\n\n. #\r\n  // indented comment\n= .\n\n");

        Assert.Equal((2, 2), (map.Grid.Width, map.Grid.Height));
        Assert.Equal(LandType.Rock, map.Grid.LandAt(new CellPos(1, 0)));
        Assert.Equal(LandType.Road, map.Grid.LandAt(new CellPos(0, 1)));
    }

    [Fact]
    public void Units_are_read_with_type_cell_and_owner()
    {
        var map = TestMapParser.Parse(". . .\n. = .\nunit heavy_tank 1 1 player=2\nunit conscript 0 0 player=0");

        Assert.Equal(
            [
                new TestMapUnit(UnitType.HeavyTank, new CellPos(1, 1), new PlayerId(2)),
                new TestMapUnit(UnitType.Conscript, new CellPos(0, 0), PlayerId.Neutral),
            ],
            map.Units);
    }

    [Fact]
    public void Ore_and_gems_lines_fill_their_rectangles_at_the_stage()
    {
        var map = TestMapParser.Parse(". . . .\n, , . .\n. . . .\nore 0 0 2 2 5\ngems 3 1 1 2");

        var ore = Enumerable.Range(0, 3).SelectMany(y => Enumerable.Range(0, 4).Select(x => map.Grid.OreAt(new CellPos(x, y)))).ToArray();
        var o = new OreCell(OreKind.Ore, 5);
        var g = new OreCell(OreKind.Gems, OreCell.MaxStage);
        var e = OreCell.Empty;
        Assert.Equal([o, o, e, e, o, o, e, g, e, e, e, g], ore);
    }

    [Fact]
    public void All_land_types_map_has_every_element()
    {
        var map = Load("all_land_types.txt");

        Assert.Equal((24, 12), (map.Grid.Width, map.Grid.Height));
        var cells = Enumerable.Range(0, map.Grid.Height)
            .SelectMany(y => Enumerable.Range(0, map.Grid.Width).Select(x => new CellPos(x, y)))
            .ToList();
        Assert.Equal(Enum.GetValues<LandType>().Order(), cells.Select(map.Grid.LandAt).Distinct().Order());
        Assert.Contains(cells, map.Grid.IsBlocked);
        Assert.Equal(Enum.GetValues<UnitType>().Order(), map.Units.Select(u => u.Type).Order());
    }

    [Theory]
    [InlineData("corridor.txt", 16, 5, 2)]
    [InlineData("bridge_gap.txt", 12, 7, 3)]
    public void Scenario_maps_parse(string name, int width, int height, int units)
    {
        var map = Load(name);

        Assert.Equal((width, height, units), (map.Grid.Width, map.Grid.Height, map.Units.Count));
    }

    [Theory]
    [InlineData("", "no rows")]
    [InlineData("// only a comment", "no rows")]
    [InlineData(". . x", "line 1, column 5: unknown cell code 'x'")]
    [InlineData(". .\n. . .", "line 2, column 1: row has 3 cells")]
    [InlineData(". ..", "line 1, column 3: cell codes must be single characters")]
    [InlineData(". .\nunit tank 0 0 player=1", "line 2, column 6: unknown unit 'tank'")]
    [InlineData(". .\nunit conscript 2 0 player=1", "line 2, column 16: cell (2, 0) is outside")]
    [InlineData(". #\nunit conscript 1 0 player=1", "line 2, column 16: conscript (Foot) cannot stand on cell (1, 0)")]
    [InlineData(". T\nunit heavy_tank 1 0 player=1", "cannot stand on cell (1, 0)")]
    [InlineData(". .\nunit conscript 0 0 player=9", "line 2, column 20: player must be 0…8")]
    [InlineData(". .\nunit conscript a 0 player=1", "line 2, column 16: 'a' is not a number")]
    [InlineData(". .\nunit conscript 0 0", "line 2, column 1: expected 'unit <id> <x> <y> player=<n>'")]
    [InlineData(". .\nore 0 0 1 1 12", "line 2, column 13: stage must be 0…11, got 12")]
    [InlineData(". .\ngems 1 0 2 1", "line 2, column 6: cells (1, 0)…(2, 0) are outside the 2x1 map")]
    [InlineData(". .\nore 0 0 0 1", "line 2, column 9: the field must be at least 1x1, got 0x1")]
    [InlineData(". ~\nore 0 0 2 1", "line 2, column 5: cell (1, 0) cannot take ore")]
    [InlineData(". T\ngems 0 0 2 1 3", "line 2, column 6: cell (1, 0) cannot take ore")]
    [InlineData(". .\nore 0 0 1 1\ngems 0 0 1 1", "line 3, column 6: cell (0, 0) cannot take ore")]
    [InlineData(". .\nore 0 0 1 a", "line 2, column 11: 'a' is not a number")]
    [InlineData(". .\nore 0 2147483647 1 2147483647", "line 2, column 5: cells (0, 2147483647)")]
    [InlineData(". .\nore 0 0 1", "line 2, column 1: expected 'ore <x> <y> <width> <height> [stage]'")]
    public void Errors_name_the_line_and_column(string text, string message)
    {
        // With the rules, so that the parser checks where each unit stands.
        var error = Assert.Throws<InvalidDataException>(() => TestMapParser.Parse(text, TestRules.Standard));

        Assert.Contains(message, error.Message);
    }
}
