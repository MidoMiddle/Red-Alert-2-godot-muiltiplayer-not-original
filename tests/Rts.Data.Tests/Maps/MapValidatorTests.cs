using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Maps;

/// <summary>The checks of the map editor (docs/implementation/M7_maps.md, 4.13, step 12) and a map built from scratch.</summary>
public sealed class MapValidatorTests
{
    private static readonly Brush Dot = new(BrushShape.Circle, 1);

    private static void Stroke(MapEditSession session, EditTool tool, Brush brush, EditOptions? options, params (int X, int Y)[] points)
    {
        session.BeginStroke(tool, brush, options);
        foreach (var (x, y) in points)
        {
            session.Paint(x, y, new CellPos(x, y));
        }

        session.EndStroke();
    }

    /// <summary>40 x 40 for the given players, starts where asked, ore beside every start.</summary>
    private static MapEditSession Arena(int players, params (int X, int Y)[] starts)
    {
        var session = new MapEditSession(EditableMap.New(40, 40, players));
        foreach (var (x, y) in starts)
        {
            Stroke(session, EditTool.Start, Dot, null, (x, y));
            Stroke(session, EditTool.Ore, new Brush(BrushShape.Square, 2), null, (x + 4, y));
        }

        return session;
    }

    private static List<MapProblem> Check(MapEditSession session) => MapValidator.Check(session.Map.ToDefinition());

    [Fact]
    public void A_fair_map_has_no_problems()
    {
        Assert.Empty(Check(Arena(2, (8, 8), (30, 30))));
    }

    [Fact]
    public void Starts_must_match_the_players_stand_on_ground_keep_off_the_edge_and_reach_each_other()
    {
        var missing = Arena(3, (8, 8), (30, 30));
        Assert.Contains(Check(missing), p => p is { Level: ProblemLevel.Error, Cell: null } && p.Message.Contains("2 starts for 3 players"));

        var edge = Arena(2, (1, 8), (30, 30));
        Assert.Contains(Check(edge), p => p.Level == ProblemLevel.Error && p.Cell == new CellPos(1, 8) && p.Message.Contains("edge"));

        var wet = Arena(2, (8, 8), (30, 30));
        Stroke(wet, EditTool.Water, Dot, null, (30, 30));
        Assert.Contains(Check(wet), p => p.Level == ProblemLevel.Error && p.Cell == new CellPos(30, 30) && p.Message.Contains("cannot stand"));

        // A ring of cliffs round start 2.
        var walled = Arena(2, (8, 8), (30, 30));
        for (var i = 0; i < 2; i++)
        {
            Stroke(walled, EditTool.Cliff, new Brush(BrushShape.Square, 1), null, (24, 24), (36, 24), (36, 36), (24, 36), (24, 24));
        }

        var problems = Check(walled);
        Assert.Contains(problems, p => p.Level == ProblemLevel.Error && p.Cell == new CellPos(30, 30) && p.Message.Contains("cannot be reached"));
        Assert.True(MapValidator.HasErrors(problems));
    }

    [Fact]
    public void Broken_bridges_and_buildings_are_errors_at_their_place()
    {
        var map = Arena(2, (8, 8), (30, 30)).Map.ToDefinition();
        var broken = new MapDefinition(map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players,
            map.Starts, map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, map.Objects,
            [new BridgePlacement("bridge_wood_low", new CellPos(20, 5), new CellPos(22, 5))],
            [new StructurePlacement("neutral_house_a", new CellPos(15, 15), 0, 0), new StructurePlacement("tech_castle", new CellPos(3, 30), 0, 0)]);

        var problems = MapValidator.Check(broken);

        Assert.Contains(problems, p => p.Level == ProblemLevel.Error && p.Cell == new CellPos(20, 5) && p.Message.Contains("not water"));
        Assert.Contains(problems, p => p.Level == ProblemLevel.Error && p.Cell == new CellPos(3, 30) && p.Message.Contains("unknown building"));
        Assert.DoesNotContain(problems, p => p.Cell == new CellPos(15, 15));
    }

    [Fact]
    public void Ore_must_be_near_every_start_and_about_even()
    {
        var bare = new MapEditSession(EditableMap.New(40, 40, 2));
        Stroke(bare, EditTool.Start, Dot, null, (8, 8));
        Stroke(bare, EditTool.Start, Dot, null, (30, 30));
        Assert.Equal(2, Check(bare).Count(p => p.Level == ProblemLevel.Warning && p.Message.Contains("No ore")));
        Assert.False(MapValidator.HasErrors(Check(bare)));

        var uneven = Arena(2, (8, 8), (30, 30));
        Stroke(uneven, EditTool.Ore, new Brush(BrushShape.Square, 3), new EditOptions { Ore = 9 }, (8, 12));
        Assert.Contains(Check(uneven), p => p.Level == ProblemLevel.Warning && p.Message.Contains("differs"));
    }

    [Fact]
    public void A_start_with_fewer_drills_near_it_than_another_is_a_warning()
    {
        // M8 step 10b: the fields that grow back are fair like the ore.
        var session = Arena(2, (8, 8), (30, 30));
        Stroke(session, EditTool.Objects, Dot, new EditOptions { ObjectType = "ore_source", Density = 100 }, (14, 14));
        Assert.Contains(Check(session), p => p.Level == ProblemLevel.Warning && p.Message == "The starts have 0 to 1 ore drills within 12 cells");

        Stroke(session, EditTool.Objects, Dot, new EditOptions { ObjectType = "ore_source", Density = 100 }, (24, 24));
        Assert.DoesNotContain(Check(session), p => p.Message.Contains("drills"));

        Stroke(session, EditTool.Objects, Dot, new EditOptions { ObjectType = "gem_source", Density = 100 }, (25, 25));
        Assert.Contains(Check(session), p => p.Message == "The starts have 0 to 1 gem drills within 12 cells");
    }

    [Fact]
    public void A_source_takes_the_ore_off_its_cell_keeps_it_bare_and_ore_under_one_is_a_warning()
    {
        var session = Arena(2, (8, 8), (30, 30));
        Stroke(session, EditTool.Ore, new Brush(BrushShape.Square, 3), null, (20, 20));
        Stroke(session, EditTool.Objects, Dot, new EditOptions { ObjectType = "ore_source", Density = 100 }, (20, 20));
        Stroke(session, EditTool.Ore, new Brush(BrushShape.Square, 3), null, (20, 20));

        var map = session.Map.ToDefinition();
        Assert.Equal(0, map.Ore[20 * map.Width + 20]);
        Assert.NotEqual(0, map.Ore[20 * map.Width + 21]);
        Assert.Contains(map.Objects, o => o.Type == "ore_source" && o.Cell == new CellPos(20, 20));
        Assert.DoesNotContain(MapValidator.Check(map), p => p.Message.Contains("under"));

        // Saved and read back, the source is an object of the map and of its grid.
        var read = RtsMapReader.Read(RtsMapWriter.Write(map));
        Assert.True(read.ContentEquals(map));
        Assert.Equal(new CellPos(20, 20), Assert.Single(read.ToMapGrid().Sources).Cell);

        // Undone, the source gives its cell its ore back (the second ore stroke changed nothing, so it left no step to undo).
        Assert.True(session.Undo());
        Assert.NotEqual(0, session.Map.ToDefinition().Ore[20 * map.Width + 20]);

        var ore = map.Ore.ToArray();
        ore[20 * map.Width + 20] = 5;
        var underOre = new MapDefinition(map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players,
            map.Starts, map.TextureLegend, map.Heights, map.Land, map.Texture, ore.ToImmutableArray(), map.Objects, map.Bridges, map.Structures);
        Assert.Contains(MapValidator.Check(underOre), p => p.Level == ProblemLevel.Warning && p.Cell == new CellPos(20, 20) && p.Message.Contains("under the ore_source"));
    }

    [Fact]
    public void With_three_players_the_starts_should_be_about_as_far_from_their_neighbours()
    {
        Assert.DoesNotContain(Check(Arena(3, (6, 20), (30, 8), (30, 32))), p => p.Message.Contains("far"));
        Assert.Contains(Check(Arena(3, (6, 6), (12, 6), (32, 32))), p => p.Level == ProblemLevel.Warning && p.Message.Contains("far"));
    }

    [Fact]
    public void The_maps_of_the_game_have_no_errors_but_the_sandbox_has_no_starts()
    {
        var root = Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps");
        var relief = MapValidator.Check(RtsMapReader.Read(File.ReadAllText(Path.Combine(root, "relief_test.rtsmap"))));
        var sandbox = MapValidator.Check(RtsMapReader.Read(File.ReadAllText(Path.Combine(root, "sandbox.rtsmap"))));

        Assert.DoesNotContain(relief, p => p.Level == ProblemLevel.Error);
        Assert.Equal("0 starts for 2 players", Assert.Single(sandbox, p => p.Level == ProblemLevel.Error).Message);
    }

    [Fact]
    public void A_map_built_from_scratch_passes_the_checks_saves_and_plays()
    {
        var session = new MapEditSession(EditableMap.New(48, 48, 2));
        session.Map.Name = "From scratch";
        session.Map.Id = UserMapPaths.IdFromName(session.Map.Name);

        // A river right across the map with its beaches, and a bridge over it.
        Stroke(session, EditTool.Water, new Brush(BrushShape.Square, 4), new EditOptions { Water = LandType.Deep }, (0, 23), (47, 23));
        Stroke(session, EditTool.Bridge, Dot, new EditOptions(), (24, 19), (24, 27));
        Assert.Null(session.Message);

        // A hill with a ramp, a wood, a road, a house, ore and the starts.
        for (var i = 0; i < 3; i++)
        {
            Stroke(session, EditTool.Raise, new Brush(BrushShape.Circle, 5), null, (36, 9));
        }

        Stroke(session, EditTool.Smooth, new Brush(BrushShape.Circle, 3), null, (31, 9), (32, 9));
        Stroke(session, EditTool.Objects, new Brush(BrushShape.Circle, 4), new EditOptions { Density = 40 }, (8, 38));
        Stroke(session, EditTool.Road, Dot, null, (12, 8), (24, 8), (24, 18));
        Stroke(session, EditTool.Building, Dot, new EditOptions { BuildingType = "neutral_house_a" }, (16, 11));
        Stroke(session, EditTool.Start, Dot, null, (10, 8));
        Stroke(session, EditTool.Start, Dot, null, (38, 40));
        Stroke(session, EditTool.Ore, new Brush(BrushShape.Square, 2), null, (6, 12));
        Stroke(session, EditTool.Ore, new Brush(BrushShape.Square, 2), null, (42, 36));

        var map = session.Map.ToDefinition();
        Assert.Empty(MapValidator.Check(map));
        var read = RtsMapReader.Read(RtsMapWriter.Write(map));
        Assert.True(read.ContentEquals(map));

        // Play it as «Play» does: the squads at the starts, the blue one sent to the red start across the bridge.
        var grid = read.ToMapGrid();
        var simulation = new GameSimulation(new SimulationSettings(seed: 12, grid), GameDataFiles.Rules);
        var units = StartSquads.Place(read, grid, GameDataFiles.Rules);
        var sequences = new Dictionary<PlayerId, uint>();
        foreach (var unit in units)
        {
            sequences.TryGetValue(unit.Owner, out var sequence);
            simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequence, unit.Type, unit.Cell.Center));
            sequences[unit.Owner] = sequence + 1;
        }

        simulation.Step();
        var blue = simulation.World.Entities.OfType<Unit>().Where(u => u.Owner == new PlayerId(1)).Select(u => u.Id).ToImmutableArray();
        Assert.NotEmpty(blue);
        simulation.Enqueue(new MoveCommand(new PlayerId(1), simulation.CurrentTick, sequences[new PlayerId(1)], blue, new CellPos(38, 36).Center));
        var crossed = false;
        for (var tick = 0; tick < 1500 && !crossed; tick++)
        {
            simulation.Step();
            crossed = simulation.World.Entities.OfType<Unit>().Any(u => u.Owner == new PlayerId(1) && grid.BridgeAt(u.Position.ToCell()) is not null);
        }

        Assert.True(crossed, "A blue unit drove onto the bridge.");
    }
}
