using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>
/// Where a building of a player may stand (docs/implementation/M3_base_economy.md, 4.8, 6): on the map, on flat buildable land, on free
/// cells with no unit, and near the player's base.
/// </summary>
public sealed class PlacementRulesTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    /// <summary>A test map of clear land with the given codes in the given cells, and extra lines (bridges, objects) after it.</summary>
    private static string Rows(int width, int height, (int X, int Y, char Code)[] cells, params string[] lines)
    {
        var rows = new char[height][];
        for (var y = 0; y < height; y++)
        {
            rows[y] = Enumerable.Repeat('.', width).ToArray();
        }

        foreach (var (x, y, code) in cells)
        {
            rows[y][x] = code;
        }

        return string.Join("\n", rows.Select(row => string.Join(" ", row)).Concat(lines));
    }

    private static GameWorld World(string text) => new(seed: 1, TestMapParser.Parse(text, TestRules.Standard).Grid, TestRules.Standard);

    private static GameWorld Open() => World(Rows(20, 12, []));

    private static Building Yard(GameWorld world, int x, int y, PlayerId? owner = null, bool ready = true) =>
        world.CreateBuilding(owner ?? Player1, TestRules.BuildingDef("construction_yard"), new CellPos(x, y), ready, null);

    private static PlacementProblem Check(GameWorld world, string id, int x, int y, bool needsBase = true, EntityId ignoreUnit = default) =>
        PlacementRules.Check(world, Player1, TestRules.BuildingDef(id), new CellPos(x, y), needsBase, ignoreUnit);

    [Theory]
    [InlineData(7, 2, PlacementProblem.None)]      // two cells between the yard (2…4) and the reactor
    [InlineData(8, 2, PlacementProblem.TooFar)]    // three cells between
    [InlineData(7, 7, PlacementProblem.None)]      // two cells across a diagonal count as two
    [InlineData(8, 7, PlacementProblem.TooFar)]
    [InlineData(5, 5, PlacementProblem.None)]      // touching at a corner
    public void A_building_stands_within_two_cells_of_the_base(int x, int y, PlacementProblem expected)
    {
        var world = Open();
        Yard(world, 2, 2);

        Assert.Equal(expected, Check(world, "reactor", x, y));
    }

    [Theory]
    [InlineData(9, PlacementProblem.None)]      // four cells between the yard (2…4) and the bunker: the sentry bunker's adjacent is 4
    [InlineData(10, PlacementProblem.TooFar)]
    public void A_sentry_bunker_stands_within_four_cells_of_the_base(int x, PlacementProblem expected)
    {
        var world = Open();
        Yard(world, 2, 2);

        Assert.Equal(expected, Check(world, "sentry_bunker", x, 2));
    }

    [Fact]
    public void A_defense_extends_no_base_not_even_for_another_defense()
    {
        var world = Open();
        Yard(world, 2, 2);
        Assert.Equal(PlacementProblem.None, Check(world, "tesla_coil", 7, 2));
        world.CreateBuilding(Player1, TestRules.BuildingDef("tesla_coil"), new CellPos(7, 2), ready: true, null);

        // Two cells from the coil (7…8), six from the yard: as in RA2 (BaseNormal=no) a defense is no base.
        Assert.Equal(PlacementProblem.TooFar, Check(world, "reactor", 11, 2));
        Assert.Equal(PlacementProblem.TooFar, Check(world, "tesla_coil", 10, 2));
    }

    [Fact]
    public void A_growing_building_already_counts_as_the_base()
    {
        var world = Open();
        Yard(world, 2, 2, ready: false);

        Assert.Equal(PlacementProblem.None, Check(world, "reactor", 5, 2));
    }

    [Fact]
    public void A_wall_does_not_extend_the_base()
    {
        var world = Open();
        Yard(world, 2, 2);
        world.CreateBuilding(Player1, TestRules.BuildingDef("wall"), new CellPos(6, 2), ready: true, null);

        // One cell from the wall, three from the yard.
        Assert.Equal(PlacementProblem.TooFar, Check(world, "reactor", 8, 2));
    }

    [Fact]
    public void Another_players_base_is_not_a_base()
    {
        var world = Open();
        Yard(world, 2, 2, Player2);

        Assert.Equal(PlacementProblem.TooFar, Check(world, "reactor", 5, 2));
        Assert.Equal(PlacementProblem.None, Check(world, "reactor", 5, 2, needsBase: false));
    }

    [Theory]
    [InlineData('~')]
    [InlineData('W')]
    [InlineData('#')]
    [InlineData('i')]
    public void Water_rock_and_ice_take_no_building(char code)
    {
        var world = World(Rows(20, 12, [(11, 6, code)]));

        Assert.Equal(PlacementProblem.NotBuildable, Check(world, "reactor", 10, 5, needsBase: false));
    }

    [Theory]
    [InlineData(',')]
    [InlineData('=')]
    [InlineData('b')]
    public void Rough_ground_road_and_beach_take_a_building(char code)
    {
        var world = World(Rows(20, 12, [(11, 6, code)]));

        Assert.Equal(PlacementProblem.None, Check(world, "reactor", 10, 5, needsBase: false));
    }

    [Fact]
    public void A_slope_takes_no_building()
    {
        var world = Open();
        world.ChangeMap([new SetCornerLevelEdit(11, 6, 1)], null);

        Assert.Equal(PlacementProblem.NotFlat, Check(world, "reactor", 10, 5, needsBase: false));
        Assert.Equal(PlacementProblem.None, Check(world, "reactor", 13, 5, needsBase: false));
    }

    [Fact]
    public void A_tree_a_fence_a_bridge_or_a_building_takes_the_place()
    {
        var world = World(Rows(
            20, 12, [(3, 8, 'T'), (10, 5, '~'), (11, 5, '~')], "object fence_wood 16 8", "bridge bridge_wood_low 10 5 11 5"));
        Yard(world, 2, 2);

        Assert.Equal(PlacementProblem.Taken, Check(world, "reactor", 2, 7, needsBase: false));
        Assert.Equal(PlacementProblem.Taken, Check(world, "reactor", 15, 7, needsBase: false));
        Assert.Equal(PlacementProblem.Taken, Check(world, "reactor", 10, 4, needsBase: false));
        Assert.Equal(PlacementProblem.Taken, Check(world, "reactor", 4, 4, needsBase: false));
    }

    [Fact]
    public void A_unit_in_the_way_stops_the_building_unless_it_is_the_one_ignored()
    {
        var world = Open();
        var tank = world.SpawnForTest(Player1, new CellPos(11, 6).Center, speed: 0, UnitType.HeavyTank);
        world.SpawnForTest(Player2, new CellPos(15, 6).Center, speed: 0);

        Assert.Equal(PlacementProblem.Units, Check(world, "reactor", 10, 5, needsBase: false));
        Assert.Equal(PlacementProblem.None, Check(world, "reactor", 10, 5, needsBase: false, ignoreUnit: tank.Id));
        Assert.Equal(PlacementProblem.Units, Check(world, "reactor", 14, 5, needsBase: false, ignoreUnit: tank.Id));
    }

    [Theory]
    [InlineData(19, 0)]
    [InlineData(-1, 3)]
    [InlineData(5, 11)]
    public void A_footprint_off_the_map_is_refused(int x, int y)
    {
        Assert.Equal(PlacementProblem.OutsideMap, Check(Open(), "reactor", x, y, needsBase: false));
    }
}
