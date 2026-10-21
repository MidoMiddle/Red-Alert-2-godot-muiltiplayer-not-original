using Rts.Data.Maps;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>The <c>.rtsmap</c> format (docs/implementation/M7_maps.md, 4.9–4.10): round trips, canonical form, hash.</summary>
public sealed class RtsMapFormatTests
{
    [Fact]
    public void Every_section_survives_a_write_and_a_read()
    {
        var map = SampleMaps.Full();

        var text = RtsMapWriter.Write(map);
        var read = RtsMapReader.Read(text);

        Assert.True(map.ContentEquals(read));
        Assert.Equal(text, RtsMapWriter.Write(read));
        Assert.Equal(14, read.CornerHeight(5, 1));
        Assert.Equal(LandType.Ice, read.Land[7]);
        Assert.Equal(0, read.Structures.Single(s => s.Type == "neutral_house_a").Owner);
    }

    [Fact]
    public void The_largest_map_survives_a_write_and_a_read()
    {
        var size = MapDefinition.MaxSize;
        var map = new MapDefinition("big", "Big", "", "", size, size, "winter", 2, [], [new('s', "snow")],
            [.. new byte[(size + 1) * (size + 1)]], [.. Enumerable.Repeat(LandType.Clear, size * size)],
            [.. Enumerable.Repeat('s', size * size)], [.. new byte[size * size]], [], [], []);

        Assert.True(map.ContentEquals(RtsMapReader.Read(RtsMapWriter.Write(map))));
    }

    [Fact]
    public void The_canonical_text_sorts_lists_and_quotes_names()
    {
        var text = RtsMapWriter.Write(SampleMaps.Full());

        Assert.Contains("name: \"Full \\\"test\\\" map: all of it\"\n", text);
        Assert.Contains("author: \"Tests \\\\ M7\"\n", text);
        var objects = text.Split('\n').Where(line => line.StartsWith("  - { type: ", StringComparison.Ordinal) && line.Contains("variant")).ToList();
        Assert.Equal(
            ["  - { type: fence_wood, cell: [1, 1], variant: 0 }", "  - { type: pine_tree, cell: [1, 3], variant: 255 }", "  - { type: pine_tree, cell: [2, 3], variant: 2 }"],
            objects);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public void Comments_spaces_line_ends_and_order_do_not_change_the_canonical_form()
    {
        var map = SampleMaps.Full();
        var canonical = RtsMapWriter.Write(map);
        var lines = canonical.Split('\n').ToList();

        // A hand-edited file: comments, CRLF, keys in another order, extra spaces, objects swapped, plain strings.
        var playersLine = lines.First(l => l.StartsWith("players:", StringComparison.Ordinal));
        lines.Remove(playersLine);
        lines.Insert(1, "players:    8   # eight of them");
        lines.Insert(0, "# hand-written map");
        var objectsAt = lines.IndexOf("objects:");
        (lines[objectsAt + 1], lines[objectsAt + 3]) = (lines[objectsAt + 3], lines[objectsAt + 1]);
        lines[objectsAt + 2] = "  - {cell: [1,3],type: pine_tree,   variant: 255}";
        var climate = lines.FindIndex(l => l.StartsWith("climate:", StringComparison.Ordinal));
        lines[climate] = "climate: 'winter'";
        var edited = string.Join("\r\n", lines);

        var read = RtsMapReader.Read(edited);

        Assert.Equal(canonical, RtsMapWriter.Write(read));
        Assert.Equal(MapHasher.Hash(map), MapHasher.Hash(read));
    }

    [Fact]
    public void Optional_parts_get_their_defaults()
    {
        const string text = "format: rtsmap/1\nid: tiny\nsize: [8, 8]\nplayers: 2\nlayers:\n  height: |\n"
            + "    000000000\n    000000000\n    000000000\n    000000000\n    000000000\n    000000000\n    000000000\n    000000000\n    000000000\n"
            + "  land: |\n    ........\n    ........\n    ........\n    ........\n    ........\n    ........\n    ........\n    ........\n";

        var map = RtsMapReader.Read(text);

        Assert.Equal(("tiny", "", "winter"), (map.Name, map.Author, map.Climate));
        Assert.Equal([new TextureEntry('a', "default")], map.TextureLegend.ToArray());
        Assert.All(map.Texture, c => Assert.Equal('a', c));
        Assert.All(map.Ore, o => Assert.Equal(0, o));
        Assert.Empty(map.Objects);
        Assert.Empty(map.Starts);
    }

    [Fact]
    public void The_hash_is_sha256_of_the_canonical_text()
    {
        var map = SampleMaps.Full();
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(RtsMapWriter.Write(map))));

        Assert.Equal(expected.ToLowerInvariant(), MapHasher.Hash(map));
        Assert.Equal(64, MapHasher.Hash(map).Length);
    }

    [Fact]
    public void Any_change_gives_another_hash()
    {
        var map = SampleMaps.Full();
        var text = RtsMapWriter.Write(map);
        var changed = RtsMapReader.Read(text.Replace("variant: 255", "variant: 254"));

        Assert.NotEqual(MapHasher.Hash(map), MapHasher.Hash(changed));
    }
}
