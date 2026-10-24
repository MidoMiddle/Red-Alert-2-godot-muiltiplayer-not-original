using Rts.Data.Maps;

namespace Rts.Data.Tests.Maps;

/// <summary>Every format error names the line and column in the file (docs/implementation/M7_maps.md, step 1).</summary>
public sealed class RtsMapErrorTests
{
    private static readonly string[] Canonical = RtsMapWriter.Write(SampleMaps.Full()).Split('\n');

    /// <summary>The canonical text of the full sample with one line replaced; returns the text and the 1-based line number.</summary>
    private static (string Text, int Line) WithLine(Func<string, bool> find, Func<string, string> change)
    {
        var lines = Canonical.ToArray();
        var index = Array.FindIndex(lines, l => find(l));
        Assert.True(index >= 0, "The line to change is in the sample.");
        lines[index] = change(lines[index]);
        return (string.Join('\n', lines), index + 1);
    }

    private static int LineOf(Func<string, bool> find) => Array.FindIndex(Canonical, l => find(l)) + 1;

    private static RtsMapFormatException ReadFails(string text) => Assert.Throws<RtsMapFormatException>(() => RtsMapReader.Read(text));

    [Fact]
    public void An_unknown_land_code_points_at_its_cell()
    {
        var landLine = LineOf(l => l == "  land: |") + 3;
        var lines = Canonical.ToArray();
        lines[landLine - 1] = lines[landLine - 1][..9] + "?" + lines[landLine - 1][10..];

        var error = ReadFails(string.Join('\n', lines));

        // Four spaces of indentation, then the sixth cell of the row.
        Assert.Equal((landLine, 10), (error.Line, error.Column));
        Assert.Contains("unknown code '?' in layer 'land'", error.Reason);
    }

    [Fact]
    public void A_short_row_points_at_where_it_ends()
    {
        var heightRow = LineOf(l => l == "  height: |") + 2;
        var lines = Canonical.ToArray();
        lines[heightRow - 1] = lines[heightRow - 1][..^2];

        var error = ReadFails(string.Join('\n', lines));

        Assert.Equal((heightRow, 4 + 7 + 1), (error.Line, error.Column));
        Assert.Contains("has 7 cells, expected 9", error.Reason);
    }

    [Fact]
    public void A_missing_row_is_reported_where_the_layer_ends()
    {
        var oreHeader = LineOf(l => l == "  ore: |");
        var lines = Canonical.ToList();
        lines.RemoveAt(oreHeader);

        var error = ReadFails(string.Join('\n', lines));

        Assert.Equal(oreHeader + 1 + 7, error.Line);
        Assert.Contains("layer 'ore' has 7 lines, expected 8", error.Reason);
    }

    [Fact]
    public void A_texture_code_outside_the_legend_is_an_error()
    {
        var textureRow = LineOf(l => l == "  texture: |") + 1;
        var lines = Canonical.ToArray();
        lines[textureRow - 1] = "    x" + lines[textureRow - 1][5..];

        var error = ReadFails(string.Join('\n', lines));

        Assert.Equal((textureRow, 5), (error.Line, error.Column));
    }

    [Fact]
    public void A_cell_outside_the_map_points_at_the_cell()
    {
        var (text, line) = WithLine(l => l.Contains("fence_wood"), l => l.Replace("[1, 1]", "[8, 1]"));

        var error = ReadFails(text);

        Assert.Equal(line, error.Line);
        Assert.Equal(Canonical[line - 1].IndexOf("[1, 1]", StringComparison.Ordinal) + 1, error.Column);
        Assert.Contains("cell 8,1 is outside the 8x8 map", error.Reason);
    }

    [Fact]
    public void Two_starts_of_one_slot_are_an_error()
    {
        var (text, line) = WithLine(l => l.Contains("slot: 2,"), l => l.Replace("slot: 2,", "slot: 1,"));

        var error = ReadFails(text);

        Assert.Equal(line, error.Line);
        Assert.Contains("slot 1 has two starts", error.Reason);
    }

    [Fact]
    public void A_bridge_must_run_along_one_axis()
    {
        var (text, line) = WithLine(l => l.Contains("from: [4, 1]"), l => l.Replace("to: [4, 5]", "to: [5, 5]"));

        var error = ReadFails(text);

        Assert.Equal(line, error.Line);
        Assert.Contains("runs along X or Y", error.Reason);
    }

    [Fact]
    public void An_unknown_format_version_is_an_error()
    {
        var (text, line) = WithLine(l => l.StartsWith("format:", StringComparison.Ordinal), _ => "format: rtsmap/2");

        var error = ReadFails(text);

        Assert.Equal((line, 9), (error.Line, error.Column));
        Assert.Contains("unknown format 'rtsmap/2'", error.Reason);
    }

    [Fact]
    public void An_unknown_time_of_day_points_at_its_value()
    {
        var (text, line) = WithLine(l => l.StartsWith("lighting:", StringComparison.Ordinal), _ => "lighting: noon");

        var error = ReadFails(text);

        Assert.Equal((line, 11), (error.Line, error.Column));
        Assert.Contains("lighting 'noon' is none of dawn, day, dusk, night", error.Reason);
    }

    [Fact]
    public void A_misspelt_key_is_not_ignored()
    {
        var (text, line) = WithLine(l => l == "bridges:", _ => "bridgez:");

        var error = ReadFails(text);

        Assert.Equal((line, 1), (error.Line, error.Column));
        Assert.Contains("unknown key 'bridgez'", error.Reason);
    }

    [Fact]
    public void A_missing_key_is_reported()
    {
        var lines = Canonical.Where(l => !l.StartsWith("players:", StringComparison.Ordinal));

        var error = ReadFails(string.Join('\n', lines));

        Assert.Contains("missing key 'players'", error.Reason);
    }

    [Fact]
    public void Numbers_are_whole()
    {
        var (text, line) = WithLine(l => l.Contains("variant: 255"), l => l.Replace("variant: 255", "variant: 2.5"));

        var error = ReadFails(text);

        Assert.Equal(line, error.Line);
        Assert.Contains("variant '2.5' is not a whole number", error.Reason);
    }

    [Fact]
    public void Broken_yaml_points_at_the_problem()
    {
        var (text, line) = WithLine(l => l.StartsWith("size:", StringComparison.Ordinal), _ => "size: [8, 8");

        var error = ReadFails(text);

        Assert.InRange(error.Line, line, line + 1);
        Assert.Contains("not valid YAML", error.Reason);
    }
}
