using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Tests.Map;

namespace Rts.Simulation.Tests.Pathfinding;

/// <summary>A* of docs/implementation/M2_unit_control.md, 4.4, checked against reference maps and against Dijkstra.</summary>
public sealed class AStarTests
{
    private static readonly Locomotor[] AllLocomotors = [Locomotor.Foot, Locomotor.Track, Locomotor.Wheel];

    private static MapGrid Load(string name) => TestMapParserTests.Load($"paths/{name}").Grid;

    /// <summary>Checks that the path is a legal walk on the map and that its cost is the sum of its steps.</summary>
    internal static void AssertValid(MapGrid map, CellPath path, Locomotor locomotor)
    {
        var cost = 0;
        for (var i = 1; i < path.Cells.Length; i++)
        {
            var from = path.Cells[i - 1];
            var to = path.Cells[i];
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            Assert.True(IntMathMax(dx, dy) == 1, $"Cells {from} and {to} are not neighbours.");
            Assert.True(AStar.CanStep(map, from, dx, dy, locomotor), $"Illegal step {from} → {to} for {locomotor}.");
            cost += AStar.StepCost(locomotor, map.LandAt(to), dx != 0 && dy != 0);
        }

        Assert.Equal(cost, path.Cost);
    }

    private static int IntMathMax(int dx, int dy) => System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));

    [Theory]
    [InlineData(Locomotor.Foot, LandType.Clear, 100, 141)]
    [InlineData(Locomotor.Wheel, LandType.Road, 83, 118)]
    [InlineData(Locomotor.Wheel, LandType.Rough, 133, 188)]
    [InlineData(Locomotor.Foot, LandType.Rough, 111, 157)]
    [InlineData(Locomotor.Track, LandType.Beach, 125, 176)]
    [InlineData(Locomotor.Foot, LandType.Ice, 125, 176)]
    [InlineData(Locomotor.Track, LandType.Deep, 0, 0)]
    public void Step_cost_divides_by_the_speed_of_the_entered_land(Locomotor locomotor, LandType land, int straight, int diagonal)
    {
        Assert.Equal(straight, AStar.StepCost(locomotor, land, diagonal: false));
        Assert.Equal(diagonal, AStar.StepCost(locomotor, land, diagonal: true));
    }

    [Fact]
    public void Open_field_takes_diagonals_then_a_straight_line()
    {
        var path = AStar.FindPath(Load("open_field.txt"), new CellPos(0, 0), new CellPos(9, 5), Locomotor.Track);

        Assert.Equal(PathStatus.Found, path.Status);
        Assert.Equal(5 * 141 + 4 * 100, path.Cost);
        Assert.Equal(10, path.Cells.Length);
    }

    [Fact]
    public void Path_from_a_cell_to_itself_is_that_cell()
    {
        var path = AStar.FindPath(Load("open_field.txt"), new CellPos(3, 3), new CellPos(3, 3), Locomotor.Foot);

        Assert.Equal((PathStatus.Found, 0, 1), (path.Status, path.Cost, path.Cells.Length));
    }

    [Theory]
    [InlineData("open_field.txt", 0, 0, 9, 5)]
    [InlineData("wall_gap.txt", 0, 0, 9, 0)]
    [InlineData("maze.txt", 0, 0, 14, 14)]
    [InlineData("u_trap.txt", 5, 4, 11, 4)]
    [InlineData("corner_trees.txt", 1, 1, 2, 2)]
    [InlineData("road_detour.txt", 0, 2, 12, 2)]
    [InlineData("rough_band.txt", 0, 0, 9, 0)]
    [InlineData("narrow_pass.txt", 0, 0, 12, 6)]
    public void Reference_maps_give_the_cheapest_path(string name, int sx, int sy, int gx, int gy)
    {
        var map = Load(name);
        foreach (var locomotor in AllLocomotors)
        {
            var path = AStar.FindPath(map, new CellPos(sx, sy), new CellPos(gx, gy), locomotor);

            Assert.Equal(PathStatus.Found, path.Status);
            Assert.Equal(new CellPos(gx, gy), path.End);
            Assert.Equal(Dijkstra.Distances(map, new CellPos(sx, sy), locomotor)[gx, gy], path.Cost);
            AssertValid(map, path, locomotor);
        }
    }

    [Fact]
    public void Wall_is_crossed_through_its_gap()
    {
        var path = AStar.FindPath(Load("wall_gap.txt"), new CellPos(0, 0), new CellPos(9, 0), Locomotor.Foot);

        Assert.Contains(new CellPos(4, 8), path.Cells);
    }

    [Fact]
    public void Narrow_pass_is_used()
    {
        var map = Load("narrow_pass.txt");
        var path = AStar.FindPath(map, new CellPos(0, 0), new CellPos(12, 6), Locomotor.Wheel);

        Assert.Contains(new CellPos(6, 3), path.Cells);
        Assert.DoesNotContain(path.Cells, cell => map.IsBlocked(cell));
    }

    [Fact]
    public void No_squeezing_between_diagonal_trees()
    {
        var path = AStar.FindPath(Load("corner_trees.txt"), new CellPos(1, 1), new CellPos(2, 2), Locomotor.Foot);

        Assert.True(path.Cells.Length > 2, "The direct diagonal between the trees must not be taken.");
        Assert.False(AStar.CanStep(Load("corner_trees.txt"), new CellPos(1, 1), 1, 1, Locomotor.Foot));
    }

    [Fact]
    public void Wheels_take_the_road_and_tracks_drive_straight()
    {
        var map = Load("road_detour.txt");

        var wheels = AStar.FindPath(map, new CellPos(0, 2), new CellPos(12, 2), Locomotor.Wheel);
        var tracks = AStar.FindPath(map, new CellPos(0, 2), new CellPos(12, 2), Locomotor.Track);

        Assert.Contains(wheels.Cells, cell => map.LandAt(cell) == LandType.Road);
        Assert.DoesNotContain(tracks.Cells, cell => map.LandAt(cell) == LandType.Road);
        Assert.Equal(1200, tracks.Cost);
        Assert.True(wheels.Cost < 1200);
    }

    [Fact]
    public void Wheels_go_around_rough_land_and_infantry_walks_through()
    {
        var map = Load("rough_band.txt");

        var wheels = AStar.FindPath(map, new CellPos(0, 0), new CellPos(9, 0), Locomotor.Wheel);
        var foot = AStar.FindPath(map, new CellPos(0, 0), new CellPos(9, 0), Locomotor.Foot);

        Assert.DoesNotContain(wheels.Cells, cell => map.LandAt(cell) == LandType.Rough);
        Assert.Contains(foot.Cells, cell => map.LandAt(cell) == LandType.Rough);
    }

    [Fact]
    public void Unreachable_island_leads_to_the_nearest_shore()
    {
        var map = Load("island.txt");

        var path = AStar.FindPath(map, new CellPos(0, 0), new CellPos(5, 5), Locomotor.Foot);

        Assert.Equal(PathStatus.Unreachable, path.Status);
        // Closest reachable cells by octile distance: (5, 1), (1, 5), (9, 5), (5, 9), all at 400.
        // (5, 1) and (1, 5) are equally cheap from (0, 0); (5, 1) has the lower cell index.
        Assert.Equal(new CellPos(5, 1), path.End);
        AssertValid(map, path, Locomotor.Foot);
    }

    [Fact]
    public void Goal_on_rock_leads_next_to_the_rock()
    {
        var map = Load("wall_gap.txt");

        var path = AStar.FindPath(map, new CellPos(0, 3), new CellPos(4, 3), Locomotor.Track);

        Assert.Equal(PathStatus.Unreachable, path.Status);
        Assert.Equal(new CellPos(3, 3), path.End);
    }

    [Fact]
    public void Node_limit_stops_the_search_with_the_best_cell_so_far()
    {
        var map = Load("maze.txt");

        var path = AStar.FindPath(map, new CellPos(0, 0), new CellPos(14, 14), Locomotor.Foot, maxExpansions: 50);

        Assert.Equal(PathStatus.LimitReached, path.Status);
        Assert.Equal(50, path.Expanded);
        AssertValid(map, path, Locomotor.Foot);
    }

    [Fact]
    public void Start_outside_the_map_is_a_programming_error()
    {
        var map = Load("open_field.txt");

        Assert.Throws<ArgumentOutOfRangeException>(() => AStar.FindPath(map, new CellPos(-1, 0), new CellPos(1, 1), Locomotor.Foot));
        Assert.Throws<ArgumentOutOfRangeException>(() => AStar.FindPath(map, new CellPos(0, 0), new CellPos(10, 0), Locomotor.Foot));
        Assert.Throws<ArgumentOutOfRangeException>(() => AStar.FindPath(map, new CellPos(0, 0), new CellPos(1, 0), Locomotor.Foot, maxExpansions: 0));
    }

    [Fact]
    public void Start_may_be_on_an_impassable_cell()
    {
        // A unit already standing there, e.g. under a tree that grew in M7.
        var map = Load("corner_trees.txt");

        var path = AStar.FindPath(map, new CellPos(2, 1), new CellPos(4, 4), Locomotor.Foot);

        Assert.Equal(PathStatus.Found, path.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1000)]
    public void Advancing_in_small_budgets_gives_the_same_path(int budget)
    {
        var map = Load("maze.txt");
        var whole = AStar.FindPath(map, new CellPos(0, 0), new CellPos(14, 14), Locomotor.Foot);

        var search = new Pathfinder(map).StartSearch(new CellPos(0, 0), new CellPos(14, 14), Locomotor.Foot);
        var calls = 0;
        while (!search.Advance(budget))
        {
            calls++;
            Assert.Null(search.Result);
        }

        Assert.Equal(whole.Cells.ToArray(), search.Result!.Cells.ToArray());
        Assert.Equal((whole.Status, whole.Cost, whole.Expanded), (search.Result.Status, search.Result.Cost, search.Result.Expanded));
        Assert.True(budget > 1 || calls >= whole.Expanded - 1);
        Assert.True(search.Advance(budget), "A finished search stays finished.");
    }

    [Fact]
    public void Searches_in_progress_do_not_disturb_each_other()
    {
        var map = Load("maze.txt");
        var pathfinder = new Pathfinder(map);
        var a = pathfinder.StartSearch(new CellPos(0, 0), new CellPos(14, 14), Locomotor.Foot);
        var b = pathfinder.StartSearch(new CellPos(14, 0), new CellPos(0, 14), Locomotor.Wheel);

        while (!(a.IsDone & b.IsDone))
        {
            a.Advance(3);
            b.Advance(5);
            // A third search reuses pooled buffers while the others are still running.
            pathfinder.FindPath(new CellPos(0, 0), new CellPos(4, 4), Locomotor.Track);
        }

        Assert.Equal(AStar.FindPath(map, new CellPos(0, 0), new CellPos(14, 14), Locomotor.Foot).Cells.ToArray(), a.Result!.Cells.ToArray());
        Assert.Equal(AStar.FindPath(map, new CellPos(14, 0), new CellPos(0, 14), Locomotor.Wheel).Cells.ToArray(), b.Result!.Cells.ToArray());
    }

    [Fact]
    public void Random_maps_match_dijkstra_for_every_locomotor()
    {
        var random = new TestRandom(5);
        var lands = Enum.GetValues<LandType>();
        for (var round = 0; round < 200; round++)
        {
            var map = new MapGrid(32, 32);
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var cell = new CellPos(x, y);
                    var roll = random.NextInt(100);
                    map.SetLand(cell, roll < 45 ? LandType.Clear : lands[random.NextInt(lands.Length)]);
                    map.SetBlocked(cell, random.NextInt(100) < 8);
                }
            }

            var locomotor = AllLocomotors[round % 3];
            var start = new CellPos(random.NextInt(32), random.NextInt(32));
            var goal = new CellPos(random.NextInt(32), random.NextInt(32));
            var distances = Dijkstra.Distances(map, start, locomotor);

            var path = AStar.FindPath(map, start, goal, locomotor);

            AssertValid(map, path, locomotor);
            Assert.Equal(distances[path.End.X, path.End.Y], path.Cost);
            if (distances[goal.X, goal.Y] >= 0)
            {
                Assert.Equal(PathStatus.Found, path.Status);
                Assert.Equal(distances[goal.X, goal.Y], path.Cost);
            }
            else
            {
                // Unreachable: the end is the reachable cell closest to the goal by heuristic, then by cost, then by index.
                Assert.Equal(PathStatus.Unreachable, path.Status);
                var best = Enumerable.Range(0, 32 * 32)
                    .Select(i => new CellPos(i % 32, i / 32))
                    .Where(c => distances[c.X, c.Y] >= 0)
                    .OrderBy(c => AStar.Heuristic(locomotor, c, goal))
                    .ThenBy(c => distances[c.X, c.Y])
                    .ThenBy(c => c.Y * 32 + c.X)
                    .First();
                Assert.Equal(best, path.End);
            }
        }
    }

    [Fact]
    public void Same_search_twice_gives_the_same_path()
    {
        var map = TestMapParserTests.Load("all_land_types.txt").Grid;

        var first = AStar.FindPath(map, new CellPos(0, 0), new CellPos(23, 11), Locomotor.Wheel);
        var second = AStar.FindPath(map, new CellPos(0, 0), new CellPos(23, 11), Locomotor.Wheel);

        Assert.Equal(first.Cells.ToArray(), second.Cells.ToArray());
    }
}
