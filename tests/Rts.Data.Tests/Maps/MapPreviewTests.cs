using Rts.Client.Maps;
using Rts.Client.Terrain;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>Map previews and the map catalog of the skirmish screen (docs/implementation/M7_maps.md, 4.11, step 10).</summary>
public sealed class MapPreviewTests
{
    // 10 x 8: a pond, a tree, a fence, a boulder, a bridge over the pond's channel, a shed, and a flat square of level 2.
    private const string Village = """
        . . . . . . . . . .
        . ~ ~ . . . . . . .
        . ~ ~ . . . . . . .
        . . . . T . . . . .
        . . ~ . . . . . . .
        . . . . . . . . . .
        . . . . . . . . . .
        . . . . . . . . . .
        object fence_wood 5 3
        object boulder 6 3
        bridge bridge_wood_low 2 4 2 4
        building neutral_shed 8 1
        """;

    private static MapDefinition Map(Func<string, string>? edit = null)
    {
        var text = RtsMapWriter.Write(TestMapConverter.Convert(TestMapParser.Parse(Village), "village", "Village"));
        return RtsMapReader.Read((edit ?? (t => t))(text));
    }

    /// <summary>The colour of the top-left pixel of a cell.</summary>
    private static (byte, byte, byte) Pixel(byte[] pixels, MapDefinition map, int x, int y)
    {
        var index = (y * MapPreview.PixelsPerCell * map.Width * MapPreview.PixelsPerCell + x * MapPreview.PixelsPerCell) * 3;
        return (pixels[index], pixels[index + 1], pixels[index + 2]);
    }

    private static (byte, byte, byte) Bytes((float R, float G, float B) color) =>
        (MapPreview.ToByte(color.R), MapPreview.ToByte(color.G), MapPreview.ToByte(color.B));

    private static (byte, byte, byte) Shaded((float R, float G, float B) color, int level)
    {
        var shade = MapPreview.HeightShade(level);
        return Bytes((color.R * shade, color.G * shade, color.B * shade));
    }

    private static MapDefinition With(
        MapDefinition map, System.Collections.Immutable.ImmutableArray<byte>? heights = null, System.Collections.Immutable.ImmutableArray<byte>? ore = null,
        System.Collections.Immutable.ImmutableArray<MapStart>? starts = null) =>
        new(map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, starts ?? map.Starts, map.TextureLegend,
            heights ?? map.Heights, map.Land, map.Texture, ore ?? map.Ore, map.Objects, map.Bridges, map.Structures);

    [Fact]
    public void A_preview_has_two_pixels_per_cell_and_is_the_same_every_time()
    {
        var map = Map();

        var pixels = MapPreview.Render(map);

        Assert.Equal(20 * 16 * 3, pixels.Length);
        Assert.Equal(pixels, MapPreview.Render(Map()));
    }

    [Fact]
    public void Cells_take_the_colours_of_the_game()
    {
        var map = Map();
        var pixels = MapPreview.Render(map);

        Assert.Equal(Shaded(LandColors.Of(LandType.Shallow), 0), Pixel(pixels, map, 1, 1));
        Assert.Equal(Shaded(LandColors.Of(LandType.Clear), 0), Pixel(pixels, map, 0, 0));
        Assert.Equal(Bytes(MapPreview.Tree), Pixel(pixels, map, 4, 3));
        Assert.Equal(Bytes(MapPreview.Fence), Pixel(pixels, map, 5, 3));
        Assert.Equal(Bytes(MapPreview.Stone), Pixel(pixels, map, 6, 3));
        Assert.Equal(Bytes(LandColors.Of(LandType.Road)), Pixel(pixels, map, 2, 4));
        Assert.Equal(Bytes(MapPreview.Building), Pixel(pixels, map, 8, 1));
    }

    [Fact]
    public void Higher_ground_is_lighter_and_a_cliff_is_dark_rock()
    {
        // Raise the corners 5…8 × 5…7 to level 2: a flat square of level 2 inside a ring of cliff cells.
        var flat = Map();
        var heights = flat.Heights.ToArray();
        for (var y = 5; y <= 7; y++)
        {
            for (var x = 5; x <= 8; x++)
            {
                heights[y * 11 + x] = 2;
            }
        }

        var raised = With(flat, heights: [.. heights]);
        var pixels = MapPreview.Render(raised);

        Assert.Equal(Shaded(LandColors.Of(LandType.Clear), 2), Pixel(pixels, raised, 6, 5));
        Assert.True(Pixel(pixels, raised, 6, 5).Item1 > Pixel(pixels, raised, 0, 0).Item1);
        Assert.Equal(Bytes(LandColors.Cliff), Pixel(pixels, raised, 4, 5));
    }

    [Fact]
    public void Starts_are_discs_in_the_colour_of_their_slot_and_ore_is_not_drawn()
    {
        var plain = MapPreview.Render(Map());
        var map = With(Map(), starts: [new MapStart(1, new CellPos(1, 6)), new MapStart(2, new CellPos(8, 6))]);
        var ore = map.Ore.ToArray();
        ore[6 * 10 + 4] = 9;
        var withOre = With(map, ore: [.. ore]);
        var pixels = MapPreview.Render(map);

        Assert.Equal(Bytes(MapPreview.SlotColor(1)), Pixel(pixels, map, 1, 6));
        Assert.Equal(Bytes(MapPreview.SlotColor(2)), Pixel(pixels, map, 8, 6));
        Assert.NotEqual(Bytes(MapPreview.SlotColor(1)), Bytes(MapPreview.SlotColor(3)));
        Assert.Equal(Pixel(plain, map, 4, 6), Pixel(pixels, map, 4, 6));
        Assert.Equal(MapPreview.Render(withOre), pixels);
    }

    [Fact]
    public void A_changed_map_gets_a_new_hash_and_so_a_new_cached_preview()
    {
        var map = Map();
        var changed = Map(text => text.Replace("type: pine_tree, cell: [4, 3]", "type: pine_tree, cell: [4, 2]"));

        Assert.Matches("^user://cache/previews/[0-9a-f]{64}\\.png$", MapPreview.CachePath(MapHasher.Hash(map)));
        Assert.NotEqual(MapPreview.CachePath(MapHasher.Hash(map)), MapPreview.CachePath(MapHasher.Hash(changed)));
        Assert.NotEqual(MapPreview.Render(map), MapPreview.Render(changed));
    }

    [Fact]
    public void The_catalog_sorts_by_name_and_leaves_out_broken_maps()
    {
        var village = RtsMapWriter.Write(Map());
        var hamlet = village.Replace("name: \"Village\"", "name: \"Hamlet\"");

        var (maps, errors) = MapCatalog.Load(
        [
            ("res://data/maps/village.rtsmap", village),
            ("res://data/maps/broken.rtsmap", "format: rtsmap/1\nid: [oops\n"),
            ("res://data/maps/hamlet.rtsmap", hamlet),
            ("res://data/maps/wet.rtsmap", village.Replace("type: neutral_shed, cell: [8, 1]", "type: neutral_shed, cell: [1, 1]")),
        ]);

        Assert.Equal(new[] { "Hamlet", "Village" }, maps.Select(m => m.Map.Name));
        Assert.Equal(MapHasher.Hash(maps[1].Map), maps[1].Hash);
        Assert.Equal(2, errors.Count);
        Assert.StartsWith("res://data/maps/broken.rtsmap:3:1: not valid YAML", errors[0]);
        Assert.StartsWith("res://data/maps/wet.rtsmap: ", errors[1]);
    }

    [Fact]
    public void The_maps_of_the_game_have_previews_with_their_starts()
    {
        var files = Directory.GetFiles(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps"), "*.rtsmap")
            .Select(path => ($"res://data/maps/{Path.GetFileName(path)}", File.ReadAllText(path)));

        var (maps, errors) = MapCatalog.Load(files);

        Assert.Empty(errors);
        // The square maps of before the playable area are left out for now (MapCatalog.Hidden).
        Assert.Equal(new[]
        {
            "Ambush Valley", "Arctic Strip", "Bay City", "Blizzard Basin", "City Blocks", "Crate Glacier", "Dense Taiga",
            "Downtown Duel", "Dune Sea", "Facing Corners", "Four Farms", "Four Isles", "Four Ports", "Frost Arena", "Frost Gate", "Frozen Crossing",
            "Frozen Outpost", "Frozen Plateau", "Glacier Duel", "Green Valley", "Harbor District", "Heaven", "Ice City",
            "Ice Ridge", "Lagoon Isle", "Mesa Pass", "Oasis Crossing", "Palm Shore", "Pine Haven", "Polar Circle", "Resort Bay", "River Meadows",
            "Snowbound Hamlet", "Snowfall Forest", "Steel Tundra", "Steph's High Ground", "Tundra Lane", "Twin Isles", "Windswept Pass",
        }, maps.Select(m => m.Map.Name));
        var relief = RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "relief_test.rtsmap")));
        var pixels = MapPreview.Render(relief);
        Assert.All(relief.Starts, start => Assert.Equal(Bytes(MapPreview.SlotColor(start.Slot)), Pixel(pixels, relief, start.Cell.X, start.Cell.Y)));
        Assert.Equal(128 * 128 * 3, MapPreview.Render(RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath))).Length);
        // The maps with a playable area are drawn as the screen shows it: twice as wide as tall.
        Assert.All(maps, m => Assert.Equal(MapPreview.SizeOf(m.Map).Width * MapPreview.SizeOf(m.Map).Height * 3, MapPreview.Render(m.Map).Length));
        var frozen = maps.Single(m => m.Map.Name == "Frozen Crossing").Map;
        Assert.Equal((180, 90), MapPreview.SizeOf(frozen));
    }
}
