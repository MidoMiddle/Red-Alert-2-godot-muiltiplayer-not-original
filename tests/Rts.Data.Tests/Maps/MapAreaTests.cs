using Rts.Client.Maps;
using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>
/// The playable area of a map, a rectangle on the screen as in RA2 (docs/implementation/M7_maps.md, 4.9; <see cref="MapArea"/>):
/// the format keeps it, the rules block what is outside, the checks and the menus know it.
/// </summary>
public sealed class MapAreaTests
{
    private static string MapPath(string id) => Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", id + ".rtsmap");

    [Fact]
    public void The_area_is_the_rectangle_of_the_screen_axes_of_the_cells()
    {
        var area = MapArea.Inscribed(90);

        Assert.Equal(new MapArea(-45, 45, 45, 135), area);
        Assert.True(area.Contains(new CellPos(45, 44)));    // the middle: u 90, v 1
        Assert.True(area.Contains(new CellPos(22, 22)));    // u 45, v 0: on the top side
        Assert.False(area.Contains(new CellPos(21, 22)));   // u 44
        Assert.False(area.Contains(new CellPos(0, 0)));     // a corner of the grid
        Assert.Equal(0, area.Margin(new CellPos(22, 22)));
        Assert.True(area.Margin(new CellPos(0, 0)) < 0);
    }

    [Fact]
    public void The_area_survives_a_write_and_a_read_and_without_it_nothing_changes()
    {
        var map = RtsMapReader.Read(File.ReadAllText(MapPath("frozen-crossing")));
        var text = RtsMapWriter.Write(map);

        Assert.Equal(MapArea.Inscribed(90), map.Playable);
        Assert.Contains("\nplayable: { left: -45, top: 45, right: 45, bottom: 135 }\n", text);
        Assert.True(map.ContentEquals(RtsMapReader.Read(text)));
        Assert.Null(RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath)).Playable);
        Assert.DoesNotContain("playable", RtsMapWriter.Write(SampleMaps.Full()));
    }

    [Fact]
    public void An_area_beyond_the_map_points_at_its_value()
    {
        var text = File.ReadAllText(MapPath("frozen-crossing")).Replace("right: 45,", "right: 95,");

        var error = Assert.Throws<RtsMapFormatException>(() => RtsMapReader.Read(text));

        Assert.Contains("right 95 is outside", error.Reason);
    }

    [Fact]
    public void The_rules_block_every_cell_outside_the_area_and_no_other()
    {
        var map = RtsMapReader.Read(File.ReadAllText(MapPath("frozen-crossing")));
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);

        for (var i = 0; i < map.Width * map.Height; i++)
        {
            var cell = new CellPos(i % map.Width, i / map.Width);
            if (!map.Playable!.Contains(cell))
            {
                Assert.True(grid.IsBlocked(cell), $"{cell} outside");
                Assert.False(grid.CanEnter(cell, Locomotor.Foot), $"{cell} outside");
            }
        }

        Assert.All(map.Starts, start => Assert.True(grid.CanEnter(start.Cell, Locomotor.Track)));
    }

    [Fact]
    public void A_start_or_a_building_outside_the_area_is_an_error()
    {
        var map = RtsMapReader.Read(File.ReadAllText(MapPath("frozen-crossing")));
        var outside = new MapDefinition(map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players,
            [map.Starts[0], new MapStart(2, new CellPos(3, 3))], map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore,
            map.Objects, map.Bridges, [.. map.Structures, new StructurePlacement("neutral_shed", new CellPos(80, 2), 0, 0)], map.Lighting, map.Playable);

        var problems = MapValidator.Check(outside, GameDataFiles.Rules.MapTypes);

        Assert.Contains(problems, p => p.Level == ProblemLevel.Error && p.Message.Contains("Start 2"));
        Assert.Contains(problems, p => p.Level == ProblemLevel.Error && p.Message.Contains("neutral_shed") && p.Message.Contains("outside the playable area"));
        Assert.DoesNotContain(MapValidator.Check(map, GameDataFiles.Rules.MapTypes), p => p.Level == ProblemLevel.Error);
    }

    [Fact]
    public void A_new_map_of_the_editor_plays_on_a_rectangle_of_about_its_size()
    {
        var map = EditableMap.NewWithArea(64, 2).ToDefinition();

        Assert.Equal((90, 90, MapArea.Inscribed(90)), (map.Width, map.Height, map.Playable));
    }

    [Fact]
    public void The_square_maps_waiting_to_be_made_again_are_not_offered()
    {
        var files = new[] { "frozen-crossing", "twin-rivers", "taiga-front", "four-corners", "sandbox", "relief_test" }
            .Select(id => ($"{MapCatalog.Directory}/{id}.rtsmap", File.ReadAllText(MapPath(id))));

        var (maps, errors) = MapCatalog.Load(files, GameDataFiles.Rules.MapTypes);

        Assert.Empty(errors);
        Assert.Equal(["frozen-crossing"], maps.Select(m => m.Map.Id));
        Assert.False(MapCatalog.IsHidden($"{MapCatalog.UserDirectory}/sandbox.rtsmap"));
    }
}
