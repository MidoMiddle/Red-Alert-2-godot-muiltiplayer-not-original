using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;

namespace Rts.Data.Tests.Maps.Editing;

/// <summary>The map editor without Godot (docs/implementation/M7_maps.md, 4.13, step 11): map, brush, tools, undo, saving.</summary>
public sealed class MapEditingTests
{
    private static readonly Brush Dot = new(BrushShape.Circle, 1);

    private static MapEditSession Session(int size = 32) => new(EditableMap.New(size, size, 2));

    /// <summary>One stroke of <paramref name="tool"/> through the given corners (the cell is the one south-east of each).</summary>
    private static void Stroke(MapEditSession session, EditTool tool, Brush brush, params (int X, int Y)[] corners) =>
        Stroke(session, tool, brush, LandType.Clear, corners);

    private static void Stroke(MapEditSession session, EditTool tool, Brush brush, LandType land, params (int X, int Y)[] corners)
    {
        session.BeginStroke(tool, brush, land);
        foreach (var (x, y) in corners)
        {
            session.Paint(x, y, new CellPos(x, y));
        }

        session.EndStroke();
    }

    private static int[] Levels(EditableMap map) =>
        Enumerable.Range(0, (map.Width + 1) * (map.Height + 1)).Select(i => map.CornerLevel(i % (map.Width + 1), i / (map.Width + 1))).ToArray();

    [Theory]
    [InlineData("sandbox.rtsmap")]
    [InlineData("relief_test.rtsmap")]
    public void An_opened_map_goes_back_to_the_same_definition(string file)
    {
        var map = RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", file)));

        var back = EditableMap.FromDefinition(map).ToDefinition();

        Assert.True(back.ContentEquals(map));
        Assert.Equal(RtsMapWriter.Write(map), RtsMapWriter.Write(back));
    }

    [Fact]
    public void A_new_map_is_flat_clear_snow_of_its_size()
    {
        var map = EditableMap.New(48, 32, 4).ToDefinition();

        Assert.Equal((48, 32, 4, "winter"), (map.Width, map.Height, map.Players, map.Climate));
        Assert.All(map.Heights, h => Assert.Equal(0, h));
        Assert.All(map.Land, l => Assert.Equal(LandType.Clear, l));
        Assert.All(map.Texture, t => Assert.Equal('s', t));
        Assert.Equal(48 * 32, map.ToMapGrid().Width * map.ToMapGrid().Height);
    }

    [Theory]
    [InlineData(BrushShape.Circle, 1, 1)]
    [InlineData(BrushShape.Circle, 2, 5)]
    [InlineData(BrushShape.Circle, 3, 13)]
    [InlineData(BrushShape.Square, 1, 1)]
    [InlineData(BrushShape.Square, 2, 9)]
    [InlineData(BrushShape.Square, 3, 25)]
    public void A_brush_covers_a_disc_or_a_square_and_stays_on_the_map(BrushShape shape, int size, int count)
    {
        var map = EditableMap.New(16, 16, 2);
        var brush = new Brush(shape, size);

        Assert.Equal(count, brush.Corners(map, 8, 8).Count);
        Assert.Equal(count, brush.Cells(map, new CellPos(8, 8)).Count);
        Assert.All(brush.Corners(map, 0, 16), c => Assert.True(map.ContainsCorner(c.X, c.Y)));
        Assert.All(brush.Cells(map, new CellPos(15, 0)), c => Assert.True(map.ContainsCell(c)));
        Assert.Contains((8 + size - 1, 8), brush.Corners(map, 8, 8));
    }

    [Fact]
    public void Raise_lifts_every_corner_once_per_stroke_and_stops_at_fourteen()
    {
        var session = Session();
        var brush = new Brush(BrushShape.Circle, 3);

        Stroke(session, EditTool.Raise, brush, (10, 10), (10, 10), (11, 10), (10, 10));
        Assert.Equal(1, session.Map.CornerLevel(10, 10));
        Assert.Equal(1, session.Map.CornerLevel(13, 10));

        Stroke(session, EditTool.Raise, brush, (10, 10));
        Assert.Equal(2, session.Map.CornerLevel(10, 10));
        Assert.Equal(1, session.Map.CornerLevel(13, 10));

        for (var i = 0; i < 20; i++)
        {
            Stroke(session, EditTool.Raise, Dot, (5, 5));
            Stroke(session, EditTool.Lower, Dot, (6, 6));
        }

        Assert.Equal(CornerHeightGrid.MaxLevel, session.Map.CornerLevel(5, 5));
        Assert.Equal(0, session.Map.CornerLevel(6, 6));
    }

    [Fact]
    public void Flatten_levels_to_the_corner_the_stroke_began_on()
    {
        var session = Session();
        Stroke(session, EditTool.Raise, new Brush(BrushShape.Square, 3), (10, 10));
        Stroke(session, EditTool.Raise, Dot, (10, 10));

        Stroke(session, EditTool.Flatten, new Brush(BrushShape.Square, 2), (12, 10), (11, 10), (10, 10));

        Assert.Equal(1, session.Map.CornerLevel(10, 10));
        Assert.Equal(1, session.Map.CornerLevel(13, 11));
        Assert.Equal(0, session.Map.CornerLevel(14, 10));
    }

    [Fact]
    public void Smooth_turns_a_cliff_under_the_brush_into_slopes_and_leaves_the_rest()
    {
        var session = Session();
        Stroke(session, EditTool.Raise, new Brush(BrushShape.Square, 6), (16, 16));
        Stroke(session, EditTool.Raise, new Brush(BrushShape.Square, 6), (16, 16));
        Stroke(session, EditTool.Raise, new Brush(BrushShape.Square, 6), (16, 16));
        var before = Levels(session.Map);
        var brush = new Brush(BrushShape.Circle, 4);

        Stroke(session, EditTool.Smooth, brush, (21, 16));

        var grid = session.ViewGrid();
        var under = brush.Corners(session.Map, 21, 16).ToHashSet();
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                if (under.Contains((x, y)) && under.Contains((x + 1, y)) && under.Contains((x, y + 1)) && under.Contains((x + 1, y + 1)))
                {
                    Assert.NotEqual(CellShape.Cliff, grid.Heights.ShapeAt(new CellPos(x, y)));
                }
            }
        }

        var after = Levels(session.Map);
        for (var i = 0; i < after.Length; i++)
        {
            if (!under.Contains((i % 33, i / 33)))
            {
                Assert.Equal(before[i], after[i]);
            }
        }
    }

    [Fact]
    public void Land_paints_cells_and_their_texture_adding_a_legend_code_when_needed()
    {
        var relief = EditableMap.FromDefinition(RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "relief_test.rtsmap"))));
        var session = new MapEditSession(relief);
        Assert.DoesNotContain(relief.Legend, e => e.Material == "asphalt");

        Stroke(session, EditTool.Land, new Brush(BrushShape.Square, 2), LandType.Road, (12, 12));

        var asphalt = Assert.Single(relief.Legend, e => e.Material == "asphalt");
        Assert.Equal(LandType.Road, relief.LandAt(new CellPos(11, 13)));
        Assert.Equal(asphalt.Code, relief.TextureAt(new CellPos(13, 11)));
        Assert.Equal(LandType.Clear, relief.LandAt(new CellPos(14, 12)));
        Assert.True(relief.ToDefinition().ToMapGrid().CanEnter(new CellPos(12, 12), Locomotor.Wheel));
    }

    [Fact]
    public void Undo_takes_back_a_stroke_and_redo_does_it_again()
    {
        var session = Session();
        var flat = Levels(session.Map);
        Stroke(session, EditTool.Raise, new Brush(BrushShape.Circle, 3), (8, 8));
        Stroke(session, EditTool.Land, Dot, LandType.Rough, (3, 3));
        var painted = Levels(session.Map);
        var version = session.Version;

        Assert.True(session.Undo());
        Assert.Equal(LandType.Clear, session.Map.LandAt(new CellPos(3, 3)));
        Assert.True(session.Undo());
        Assert.Equal(flat, Levels(session.Map));
        Assert.False(session.Undo());
        Assert.True(session.Version > version);

        Assert.True(session.Redo());
        Assert.Equal(painted, Levels(session.Map));
        Assert.Equal(1, session.History.RedoCount);

        Stroke(session, EditTool.Raise, Dot, (20, 20));
        Assert.Equal(0, session.History.RedoCount);
        Assert.False(session.Redo());
    }

    [Fact]
    public void A_stroke_that_changes_nothing_leaves_no_step_and_the_history_keeps_two_hundred()
    {
        var session = Session();
        Stroke(session, EditTool.Lower, Dot, (4, 4));
        Assert.Equal(0, session.History.UndoCount);

        for (var i = 0; i < UndoStack.Depth + 5; i++)
        {
            Stroke(session, EditTool.Raise, Dot, (i % 30, i / 30));
        }

        Assert.Equal(UndoStack.Depth, session.History.UndoCount);
        while (session.Undo())
        {
        }

        // The five oldest strokes cannot be taken back any more.
        Assert.Equal(5, Levels(session.Map).Count(level => level == 1));
    }

    [Fact]
    public void A_hill_with_a_ramp_is_drawn_climbed_and_saved()
    {
        var session = Session();
        var hill = new Brush(BrushShape.Circle, 6);
        for (var i = 0; i < 4; i++)
        {
            Stroke(session, EditTool.Raise, hill, (16, 16));
        }

        Assert.Equal(4, session.Map.CornerLevel(16, 16));
        Assert.Equal(PathStatus.Unreachable, AStar.FindPath(session.ViewGrid(), new CellPos(2, 16), new CellPos(16, 16), Locomotor.Track).Status);

        // Smooth the west edge of the hill into a ramp.
        Stroke(session, EditTool.Smooth, new Brush(BrushShape.Circle, 4), (11, 16), (12, 16), (13, 16));
        var grid = session.ViewGrid();
        var path = AStar.FindPath(grid, new CellPos(2, 16), new CellPos(16, 16), Locomotor.Track);

        Assert.Equal(PathStatus.Found, path.Status);
        Assert.Contains(path.Cells, cell => grid.Heights.ShapeAt(cell) == CellShape.Slope);
        Assert.All(path.Cells, cell => Assert.NotEqual(CellShape.Cliff, grid.Heights.ShapeAt(cell)));

        session.Map.Name = "My hill";
        session.Map.Id = UserMapPaths.IdFromName(session.Map.Name);
        var text = RtsMapWriter.Write(session.Map.ToDefinition());
        var read = RtsMapReader.Read(text);
        Assert.True(read.ContentEquals(session.Map.ToDefinition()));
        Assert.Equal("my-hill", read.Id);
    }

    [Theory]
    [InlineData("My hill", "my-hill")]
    [InlineData("  Bridges  &  Rivers_2 ", "bridges-rivers-2")]
    [InlineData("!!!", "map")]
    public void A_map_is_saved_among_the_players_maps_by_an_id_from_its_name(string name, string id)
    {
        Assert.Equal(id, UserMapPaths.IdFromName(name));
        Assert.True(MapNames.IsId(UserMapPaths.IdFromName(name)));
        Assert.Equal($"user://maps/{id}.rtsmap", UserMapPaths.SavePath(UserMapPaths.IdFromName(name)));
    }

    [Fact]
    public void A_saved_map_sits_in_the_catalog_next_to_the_maps_of_the_game()
    {
        var mine = EditableMap.New(32, 32, 2);
        mine.Name = "My hill";
        mine.Id = UserMapPaths.IdFromName(mine.Name);
        var files = Directory.GetFiles(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps"), "*.rtsmap")
            .Select(path => ($"res://data/maps/{Path.GetFileName(path)}", File.ReadAllText(path)))
            .Append((UserMapPaths.SavePath(mine.Id), RtsMapWriter.Write(mine.ToDefinition())));

        var (maps, errors) = Rts.Client.Maps.MapCatalog.Load(files);

        Assert.Empty(errors);
        Assert.Equal(new[] { "My hill", "Relief test", "Sandbox" }, maps.Select(m => m.Map.Name));
        Assert.Equal("user://maps/my-hill.rtsmap", maps[0].File);
        Assert.Equal(Rts.Client.Maps.MapCatalog.UserDirectory, UserMapPaths.UserDirectory);
    }
}
