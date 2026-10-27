using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps.Editing;

/// <summary>The tools of M7 step 12 (docs/implementation/M7_maps.md, 4.13): cliff, water, road, ore, objects, bridge, building, start, eraser.</summary>
public sealed class MapEditingToolsTests
{
    private static readonly Brush Dot = new(BrushShape.Circle, 1);

    private static MapEditSession Session(int size = 32, int players = 2) => new(EditableMap.New(size, size, players), GameDataFiles.Rules.MapTypes);

    /// <summary>One stroke through the given cells; height tools use the same numbers as corners.</summary>
    private static string? Stroke(MapEditSession session, EditTool tool, Brush brush, EditOptions? options, params (int X, int Y)[] points)
    {
        session.BeginStroke(tool, brush, options);
        foreach (var (x, y) in points)
        {
            session.Paint(x, y, new CellPos(x, y));
        }

        session.EndStroke();
        return session.Message;
    }

    /// <summary>A lake of deep water from (10, 10) to (14, 14) on a new map, with its beach.</summary>
    private static MapEditSession WithLake()
    {
        var session = Session();
        Stroke(session, EditTool.Water, new Brush(BrushShape.Square, 3), new EditOptions { Water = LandType.Deep }, (12, 12));
        return session;
    }

    [Fact]
    public void Cliff_raises_two_levels_once_per_stroke()
    {
        var session = Session();

        Stroke(session, EditTool.Cliff, new Brush(BrushShape.Square, 2), null, (10, 10), (10, 10), (11, 10));

        Assert.Equal(2, session.Map.CornerLevel(10, 10));
        Assert.Equal(2, session.Map.CornerLevel(12, 11));
        Assert.Equal(CellShape.Cliff, session.ViewGrid().Heights.ShapeAt(new CellPos(8, 10)));
    }

    [Fact]
    public void Water_has_its_depth_and_a_beach_but_roads_and_rock_stay()
    {
        var session = Session();
        Stroke(session, EditTool.Land, Dot, new EditOptions { Land = LandType.Road }, (8, 12));
        Stroke(session, EditTool.Land, Dot, new EditOptions { Land = LandType.Rock }, (16, 12));

        Stroke(session, EditTool.Water, new Brush(BrushShape.Square, 3), new EditOptions { Water = LandType.Deep }, (12, 12));

        Assert.Equal(LandType.Deep, session.Map.LandAt(new CellPos(12, 12)));
        Assert.Equal(LandType.Deep, session.Map.LandAt(new CellPos(14, 14)));
        Assert.Equal(LandType.Beach, session.Map.LandAt(new CellPos(9, 12)));
        Assert.Equal(LandType.Beach, session.Map.LandAt(new CellPos(15, 15)));
        Assert.Equal(LandType.Clear, session.Map.LandAt(new CellPos(16, 16)));
        Assert.Equal(LandType.Road, session.Map.LandAt(new CellPos(8, 12)));
        Assert.Equal(LandType.Rock, session.Map.LandAt(new CellPos(16, 12)));
        Assert.Equal(session.Map.CodeFor("water"), session.Map.TextureAt(new CellPos(12, 12)));
        Assert.Equal(session.Map.CodeFor("shore"), session.Map.TextureAt(new CellPos(9, 12)));

        Stroke(session, EditTool.Water, Dot, new EditOptions { Water = LandType.Shallow }, (20, 20));
        Assert.Equal(LandType.Shallow, session.Map.LandAt(new CellPos(20, 20)));
    }

    [Fact]
    public void A_fast_road_stroke_leaves_no_gaps_and_is_as_wide_as_the_brush()
    {
        var session = Session();

        Stroke(session, EditTool.Road, new Brush(BrushShape.Circle, 2), null, (2, 5), (20, 5));

        for (var x = 1; x <= 21; x++)
        {
            Assert.Equal(LandType.Road, session.Map.LandAt(new CellPos(x, 5)));
            Assert.Equal(LandType.Road, session.Map.LandAt(new CellPos(x, 4)));
            Assert.Equal(LandType.Road, session.Map.LandAt(new CellPos(x, 6)));
        }

        Assert.Equal(LandType.Clear, session.Map.LandAt(new CellPos(10, 7)));
        Assert.Equal(1, session.History.UndoCount);
    }

    [Fact]
    public void A_road_stops_at_water()
    {
        var session = WithLake();

        Stroke(session, EditTool.Road, Dot, null, (2, 12), (25, 12));

        Assert.Equal(LandType.Road, session.Map.LandAt(new CellPos(8, 12)));
        Assert.Equal(LandType.Deep, session.Map.LandAt(new CellPos(12, 12)));
        Assert.Equal(LandType.Road, session.Map.LandAt(new CellPos(20, 12)));
    }

    [Fact]
    public void Ore_and_gems_go_on_dry_land_only()
    {
        var session = WithLake();

        Stroke(session, EditTool.Ore, new Brush(BrushShape.Square, 3), new EditOptions { Ore = 7 }, (12, 8));
        Stroke(session, EditTool.Ore, Dot, new EditOptions { Ore = 11 }, (20, 20));

        Assert.Equal(7, session.Map.OreAt(new CellPos(12, 7)));
        Assert.Equal(7, session.Map.OreAt(new CellPos(12, 9)));
        Assert.Equal(0, session.Map.OreAt(new CellPos(12, 10)));
        Assert.Equal(11, session.Map.OreAt(new CellPos(20, 20)));
        Assert.Equal(11, session.Map.ToDefinition().Ore[20 * 32 + 20]);
    }

    [Fact]
    public void Objects_fill_free_cells_by_density_the_same_way_every_time_and_undo_takes_them_back()
    {
        var full = WithLake();
        var square = new Brush(BrushShape.Square, 4);

        Stroke(full, EditTool.Objects, square, new EditOptions { ObjectType = "pine_tree", Density = 100 }, (12, 12));

        // 7 x 7 cells under the brush; the lake (5 x 5) is left out, its beach is not.
        Assert.Equal(49 - 25, full.Map.Objects.Count);
        Assert.Contains(full.Map.Objects, o => o.Cell == new CellPos(9, 9));
        Assert.All(full.Map.Objects, o => Assert.InRange(o.Variant, 0, 3));
        Assert.DoesNotContain(full.Map.Objects, o => o.Cell == new CellPos(12, 12));
        Assert.True(full.Map.ToDefinition().ToMapGrid(GameDataFiles.Rules.MapTypes).Objects.Count > 0);

        Stroke(full, EditTool.Objects, square, new EditOptions { ObjectType = "boulder", Density = 100 }, (12, 12));
        Assert.DoesNotContain(full.Map.Objects, o => o.Type == "boulder");

        var a = Session();
        var b = Session();
        Stroke(a, EditTool.Objects, square, new EditOptions { Density = 40 }, (12, 12));
        Stroke(b, EditTool.Objects, square, new EditOptions { Density = 40 }, (12, 12));
        Assert.Equal(a.Map.Objects, b.Map.Objects);
        Assert.InRange(a.Map.Objects.Count, 5, 45);

        Stroke(b, EditTool.Objects, square, new EditOptions { Density = 0 }, (20, 20));
        Assert.Equal(a.Map.Objects.Count, b.Map.Objects.Count);

        Assert.True(a.Undo());
        Assert.Empty(a.Map.Objects);
        Assert.True(a.Redo());
        Assert.Equal(b.Map.Objects, a.Map.Objects);
    }

    [Fact]
    public void A_bridge_goes_from_shore_to_shore_over_water_or_not_at_all()
    {
        var session = WithLake();
        var before = session.Map.ToDefinition();

        Assert.NotNull(Stroke(session, EditTool.Bridge, Dot, null, (8, 12), (16, 13)));
        Assert.NotNull(Stroke(session, EditTool.Bridge, Dot, null, (2, 2), (6, 2)));
        Assert.NotNull(Stroke(session, EditTool.Bridge, Dot, null, (12, 12), (16, 12)));
        Assert.True(session.Map.ToDefinition().ContentEquals(before));
        Assert.Equal(0, session.History.UndoCount - 1);

        Assert.Null(Stroke(session, EditTool.Bridge, Dot, null, (9, 12), (15, 12)));

        var bridge = Assert.Single(session.Map.Bridges);
        Assert.Equal((new CellPos(10, 12), new CellPos(14, 12)), (bridge.From, bridge.To));
        Assert.Single(session.Map.ToDefinition().ToMapGrid(GameDataFiles.Rules.MapTypes).Bridges);
        Assert.NotNull(Stroke(session, EditTool.Bridge, Dot, null, (9, 12), (15, 12)));

        Assert.True(session.Undo());
        Assert.Empty(session.Map.Bridges);
    }

    /// <summary>A new 20 x 12 map with plateaus at level 4 west of corner column 6 and east of 14, a dry valley between them.</summary>
    private static MapEditSession WithGorge()
    {
        var text = RtsMapWriter.Write(EditableMap.New(20, 12, 2).ToDefinition());
        var flat = string.Concat(Enumerable.Repeat("    " + new string('0', 21) + "\n", 13));
        var gorge = string.Concat(Enumerable.Repeat("    " + new string('4', 7) + new string('0', 7) + new string('4', 7) + "\n", 13));
        Assert.Contains(flat, text);
        return new MapEditSession(EditableMap.FromDefinition(RtsMapReader.Read(text.Replace(flat, gorge))), GameDataFiles.Rules.MapTypes);
    }

    [Fact]
    public void A_high_bridge_goes_from_plateau_to_plateau_over_the_valley()
    {
        var session = WithGorge();
        var high = new EditOptions { BridgeType = "bridge_concrete_high" };

        // Not straight, and not from a plateau: the load rules tell why.
        Assert.NotNull(Stroke(session, EditTool.Bridge, Dot, high, (5, 5), (14, 6)));
        Assert.Contains("shore", Stroke(session, EditTool.Bridge, Dot, high, (7, 5), (14, 5)) ?? "");
        Assert.Empty(session.Map.Bridges);

        Assert.Null(Stroke(session, EditTool.Bridge, Dot, high, (5, 5), (14, 5)));

        var bridge = Assert.Single(session.Map.Bridges);
        Assert.Equal(("bridge_concrete_high", new CellPos(6, 5), new CellPos(13, 5)), (bridge.Type, bridge.From, bridge.To));
        var grid = session.Map.ToDefinition().ToMapGrid(GameDataFiles.Rules.MapTypes);
        Assert.Equal(24, grid.DeckCount);
        Assert.True(session.Undo());
        Assert.Empty(session.Map.Bridges);
    }

    [Fact]
    public void A_building_stands_on_flat_free_dry_ground_only()
    {
        var session = WithLake();
        Stroke(session, EditTool.Raise, Dot, null, (25, 25));
        var house = new EditOptions { BuildingType = "neutral_house_a", Facing = 2 };

        Assert.Null(Stroke(session, EditTool.Building, Dot, house, (3, 3)));
        Assert.Contains("taken", Stroke(session, EditTool.Building, Dot, house, (4, 4)));
        Assert.Contains("Deep", Stroke(session, EditTool.Building, Dot, house, (12, 12)) ?? "");
        Assert.Contains("flat", Stroke(session, EditTool.Building, Dot, house, (24, 24)) ?? "");
        Assert.Contains("unknown", Stroke(session, EditTool.Building, Dot, house with { BuildingType = "castle" }, (20, 3)) ?? "");

        var placed = Assert.Single(session.Map.Structures);
        Assert.Equal(("neutral_house_a", new CellPos(3, 3), 0, 2), (placed.Type, placed.Cell, placed.Owner, placed.Facing));
        Assert.True(session.Map.ToDefinition().ToMapGrid(GameDataFiles.Rules.MapTypes).HasBuilding(new CellPos(4, 4)));
        Assert.True(session.Undo());
        Assert.Empty(session.Map.Structures);
    }

    [Fact]
    public void Starts_take_the_lowest_free_number_up_to_the_players()
    {
        var session = Session(players: 2);

        Assert.Null(Stroke(session, EditTool.Start, Dot, null, (5, 5)));
        Assert.Null(Stroke(session, EditTool.Start, Dot, null, (25, 25)));
        Assert.Contains("all 2", Stroke(session, EditTool.Start, Dot, null, (15, 15)));
        Assert.Equal(new[] { 1, 2 }, session.Map.Starts.Select(s => s.Slot));

        Stroke(session, EditTool.Eraser, Dot, new EditOptions { Erase = EraseLayer.Starts }, (5, 5));
        Assert.Null(Stroke(session, EditTool.Start, Dot, null, (6, 6)));
        Assert.Equal(new MapStart(1, new CellPos(6, 6)), session.Map.Starts.Single(s => s.Slot == 1));
    }

    [Fact]
    public void The_eraser_takes_only_its_layer()
    {
        var session = WithLake();
        Stroke(session, EditTool.Bridge, Dot, null, (9, 12), (15, 12));
        Stroke(session, EditTool.Objects, Dot, null, (20, 20));
        Stroke(session, EditTool.Building, Dot, new EditOptions { BuildingType = "neutral_shed" }, (21, 20));
        Stroke(session, EditTool.Ore, Dot, null, (22, 20));
        Stroke(session, EditTool.Start, Dot, null, (20, 21));
        var everything = new Brush(BrushShape.Square, 4);

        Stroke(session, EditTool.Eraser, everything, new EditOptions { Erase = EraseLayer.Objects }, (21, 21));
        Assert.Empty(session.Map.Objects);
        Assert.Single(session.Map.Structures);
        Assert.Equal(5, session.Map.OreAt(new CellPos(22, 20)));

        Stroke(session, EditTool.Eraser, everything, new EditOptions { Erase = EraseLayer.Ore }, (21, 21));
        Assert.Equal(0, session.Map.OreAt(new CellPos(22, 20)));
        Assert.Single(session.Map.Structures);

        Stroke(session, EditTool.Eraser, Dot, new EditOptions { Erase = EraseLayer.Bridges }, (12, 12));
        Assert.Empty(session.Map.Bridges);

        Stroke(session, EditTool.Eraser, everything, new EditOptions { Erase = EraseLayer.All }, (21, 21));
        Assert.Empty(session.Map.Structures);
        Assert.Empty(session.Map.Starts);
        Assert.Equal(LandType.Deep, session.Map.LandAt(new CellPos(12, 12)));
    }
}
