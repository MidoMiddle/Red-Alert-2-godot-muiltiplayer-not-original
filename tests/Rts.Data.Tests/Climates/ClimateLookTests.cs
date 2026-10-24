using Rts.Client.Climates;
using Rts.Data.Maps;
using Rts.Data.Tests.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Climates;

/// <summary>
/// The looks of the climates (docs/implementation/M7_maps.md, 4.12; docs/map_elements/18_lighting): <c>look.yaml</c> reads with
/// its errors at their place, covers every map of the game, and the ground materials of a map's cells come out of it.
/// </summary>
public sealed class ClimateLookTests
{
    private static string ClimateFolder => Path.Combine(SampleMaps.RepoRoot, "game", "data", "climates");

    private static ClimateLook Winter() => ClimateLookReader.Read(File.ReadAllText(Path.Combine(ClimateFolder, "winter", "look.yaml")));

    [Fact]
    public void Winter_has_every_time_of_day_and_its_day_is_the_light_the_game_had()
    {
        var winter = Winter();

        Assert.Equal("winter", winter.Id);
        Assert.Equal(MapLighting.Ids, winter.Lighting.Select(l => l.Id));
        Assert.Equal("rock", winter.Cliff);
        // Main.SunDirection, the sun of M1 step 2: (1, -1.25, 0.25), normalised.
        var (x, y, z) = winter.Light("day").SunDirection();
        var length = MathF.Sqrt(1f + 1.25f * 1.25f + 0.25f * 0.25f);
        Assert.Equal(1f / length, x, 3);
        Assert.Equal(-1.25f / length, y, 3);
        Assert.Equal(0.25f / length, z, 3);
        Assert.Equal(new LookColor(1f, 247 / 255f, 235 / 255f), winter.Light("day").SunColor);
        Assert.Same(winter.Light("day"), winter.Light("noon"));
    }

    [Fact]
    public void Every_material_of_every_map_of_the_game_is_in_its_climate_with_a_texture()
    {
        var textures = Path.Combine(SampleMaps.RepoRoot, "game", "assets", "textures", "terrain");
        foreach (var path in Directory.GetFiles(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps"), "*.rtsmap"))
        {
            var map = RtsMapReader.Read(File.ReadAllText(path));
            var climate = ClimateLookReader.Read(File.ReadAllText(Path.Combine(ClimateFolder, map.Climate, "look.yaml")));
            Assert.All(map.TextureLegend, entry => Assert.True(climate.SlotOf(entry.Material) >= 0, $"{Path.GetFileName(path)}: {entry.Material}"));
            Assert.True(MapLighting.IsKnown(map.Lighting));
        }

        var winter = Winter();
        Assert.All(winter.Materials, material => Assert.True(File.Exists(Path.Combine(textures, material.Albedo)), material.Albedo));
        Assert.True(File.Exists(Path.Combine(textures, winter.WaterNormal)));
        Assert.Contains(winter.Materials, material => material.Water);
    }

    [Fact]
    public void The_rules_never_read_the_climates()
    {
        Assert.DoesNotContain(RulesLoader.FilesBelow(GameDataFiles.Folder), file => file.FileName.StartsWith("climates/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("materials:", "materiels:", 1, "unknown key 'materiels' in the climate")]
    [InlineData("deep_water: \"#1a3d8f\"", "deep_water: \"#1a3d8\"", 13, "colour '#1a3d8' must be \"#rrggbb\"")]
    [InlineData("cliff: rock", "cliff: lava", 8, "the cliff material 'lava' is none of the materials")]
    [InlineData("  night:", "  noon:", 3, "unknown key 'noon' in lighting")]
    public void A_mistake_in_a_climate_points_at_its_place(string line, string replacement, int column, string reason)
    {
        var lines = File.ReadAllText(Path.Combine(ClimateFolder, "winter", "look.yaml")).Replace("\r\n", "\n").Split('\n');
        var index = Array.IndexOf(lines, line);
        Assert.True(index >= 0, line);
        lines[index] = replacement;

        var error = Assert.Throws<ClimateFormatException>(() => ClimateLookReader.Read(string.Join('\n', lines)));

        Assert.Equal((index + 1, column), (error.Line, error.Column));
        Assert.Contains(reason, error.Reason);
    }

    [Fact]
    public void Cells_take_the_slot_of_their_material_and_cliffs_the_cliff_material()
    {
        // 8 x 8: corners from x = 4 at level 2, so the cells of column 3 are cliffs; a pond of deep water at (6, 1).
        const int size = 8;
        var heights = new byte[(size + 1) * (size + 1)];
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = (byte)(i % (size + 1) >= 4 ? 2 : 0);
        }

        var land = Enumerable.Repeat(LandType.Clear, size * size).ToArray();
        land[1 * size + 6] = LandType.Deep;
        var texture = Enumerable.Repeat('s', size * size).ToArray();
        texture[0] = 'g';
        texture[1] = 'x';
        texture[1 * size + 6] = 'w';
        var map = new MapDefinition("slots", "Slots", "", "", size, size, "winter", 2, [],
            [new('s', "snow"), new('g', "grass"), new('w', "water"), new('x', "lava")],
            [.. heights], [.. land], [.. texture], [.. new byte[size * size]], [], [], []);
        var winter = Winter();

        var cells = MaterialSlots.Build(MapLook.FromDefinition(map), winter, map.ToMapGrid());

        byte Slot(int x, int y) => cells[(y * size + x) * MaterialSlots.BytesPerCell];
        Assert.Equal(winter.SlotOf("grass"), Slot(0, 0));
        Assert.Equal(winter.SlotOf("snow"), Slot(1, 0));
        Assert.Equal(winter.SlotOf("snow"), Slot(2, 0));
        Assert.Equal(winter.SlotOf("rock"), Slot(3, 5));
        Assert.Equal(winter.SlotOf("water"), Slot(6, 1));
        Assert.Equal(2, cells[(1 * size + 6) * MaterialSlots.BytesPerCell + 1]);
        Assert.Equal(0, cells[1]);
    }

    [Fact]
    public void A_map_without_a_look_of_its_own_is_a_winter_day_textured_by_its_land()
    {
        var grid = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath)).ToMapGrid();

        var look = MapLook.FromLand(grid);

        Assert.Equal(("winter", "day"), (look.Climate, look.Lighting));
        var road = Enumerable.Range(0, grid.Width * grid.Height).First(i => grid.LandAt(new CellPos(i % grid.Width, i / grid.Width)) == LandType.Road);
        Assert.Equal("asphalt", look.MaterialAt(road));
    }
}
