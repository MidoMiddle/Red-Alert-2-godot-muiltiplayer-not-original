using Rts.Client.Controls;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The placement preview (docs/implementation/M3_base_economy.md, 4.17): the footprint under the pointer, the colour of each cell, and a
/// line of walls as the command would place it.
/// </summary>
public sealed class PlacementPreviewTests
{
    private static readonly PlayerId Player1 = new(1);

    /// <summary>A yard at (2, 2)–(4, 4), its technologies worked out.</summary>
    private static GameSimulation WithYard(int credits = 10_000, params CellPos[] water)
    {
        var map = new MapGrid(40, 30, TestRules.Standard.Land);
        foreach (var cell in water)
        {
            map.SetLand(cell, LandType.Deep);
        }

        var simulation = new GameSimulation(new SimulationSettings(1, map) { StartingCredits = credits }, TestRules.Standard);
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("construction_yard"), new CellPos(2, 2), true, null);
        simulation.Step();
        return simulation;
    }

    private static PreviewState[] States(IReadOnlyList<PreviewCell> cells) => cells.Select(cell => cell.State).ToArray();

    [Theory]
    [InlineData(1, 1, 10, 10)]
    [InlineData(2, 2, 10, 10)]
    [InlineData(3, 3, 9, 9)]
    [InlineData(3, 4, 9, 9)]
    public void The_centre_of_the_footprint_is_under_the_pointer(int width, int height, int x, int y)
    {
        Assert.Equal(new CellPos(x, y), PlacementPreview.CornerOf(new CellSize(width, height), new CellPos(10, 10).Center));
    }

    [Fact]
    public void A_footprint_by_the_base_is_green()
    {
        var simulation = WithYard();
        var cells = PlacementPreview.Footprint(simulation.World, Player1, TestRules.BuildingDef("reactor"), new CellPos(6, 2));

        Assert.Equal([new CellPos(6, 2), new CellPos(7, 2), new CellPos(6, 3), new CellPos(7, 3)], cells.Select(cell => cell.Cell));
        Assert.All(cells, cell => Assert.Equal(PreviewState.Ok, cell.State));
    }

    [Fact]
    public void Only_the_cells_with_a_problem_are_red()
    {
        var simulation = WithYard(10_000, new CellPos(7, 3));
        var cells = PlacementPreview.Footprint(simulation.World, Player1, TestRules.BuildingDef("reactor"), new CellPos(6, 2));

        Assert.Equal([PreviewState.Ok, PreviewState.Ok, PreviewState.Ok, PreviewState.Refused], States(cells));
        Assert.Equal(PlacementProblem.NotBuildable, PlacementRules.Check(simulation.World, Player1, TestRules.BuildingDef("reactor"), new CellPos(6, 2)));
    }

    [Fact]
    public void Over_the_yard_the_taken_cells_are_red()
    {
        var simulation = WithYard();
        var cells = PlacementPreview.Footprint(simulation.World, Player1, TestRules.BuildingDef("reactor"), new CellPos(4, 4));

        Assert.Equal([PreviewState.Refused, PreviewState.Ok, PreviewState.Ok, PreviewState.Ok], States(cells));
    }

    [Fact]
    public void Too_far_from_the_base_the_whole_footprint_is_red()
    {
        var simulation = WithYard();
        var cells = PlacementPreview.Footprint(simulation.World, Player1, TestRules.BuildingDef("reactor"), new CellPos(20, 20));

        Assert.All(cells, cell => Assert.Equal(PreviewState.Refused, cell.State));
    }

    [Fact]
    public void A_footprint_over_the_edge_is_red_where_it_leaves_the_map()
    {
        var simulation = WithYard();
        var cells = PlacementPreview.Footprint(simulation.World, Player1, TestRules.BuildingDef("reactor"), new CellPos(39, 5));

        Assert.Equal(PreviewState.Refused, cells.Single(cell => cell.Cell == new CellPos(40, 5)).State);
    }

    [Theory]
    [InlineData(5, 3, 9, 3, 4)]
    [InlineData(5, 3, 5, 0, 3)]
    [InlineData(5, 3, 8, 6, 3)]
    [InlineData(5, 3, 7, 9, 6)]
    [InlineData(5, 3, 5, 3, 0)]
    public void A_wall_line_goes_straight_or_diagonal(int fromX, int fromY, int toX, int toY, int length)
    {
        var line = PlacementPreview.WallLine(new CellPos(fromX, fromY), new CellPos(toX, toY));

        Assert.Equal(length, line.Length);
        if (length > 0)
        {
            Assert.DoesNotContain(new CellPos(fromX, fromY), line);
            var last = line[^1];
            Assert.True(last.X == toX || last.Y == toY);
        }
    }

    [Fact]
    public void A_wall_line_is_at_most_64_cells()
    {
        Assert.Equal(PlaceWallsCommand.MaxCells, PlacementPreview.WallLine(new CellPos(0, 0), new CellPos(200, 0)).Length);
    }

    [Fact]
    public void A_wall_preview_shows_water_red_and_what_the_credits_do_not_reach_grey()
    {
        // From a wall at (5, 3): (6, 3) takes the ready wall, (7, 3) is water, (8, 3) and (9, 3) are paid, the rest unpaid.
        var simulation = WithYard(credits: 250, new CellPos(7, 3));
        var cells = PlacementPreview.WallLine(new CellPos(5, 3), new CellPos(12, 3));

        var preview = PlacementPreview.Wall(simulation.World, Player1, TestRules.BuildingDef("wall"), cells);

        Assert.Equal(
            [PreviewState.Ok, PreviewState.Refused, PreviewState.Ok, PreviewState.Ok, PreviewState.Unpaid, PreviewState.Unpaid, PreviewState.Unpaid],
            States(preview));
    }

    [Fact]
    public void A_wall_preview_goes_on_beyond_the_base_from_its_own_segments()
    {
        var simulation = WithYard();
        var preview = PlacementPreview.Wall(
            simulation.World, Player1, TestRules.BuildingDef("wall"), PlacementPreview.WallLine(new CellPos(5, 3), new CellPos(25, 3)));

        Assert.All(preview, cell => Assert.Equal(PreviewState.Ok, cell.State));
    }
}
