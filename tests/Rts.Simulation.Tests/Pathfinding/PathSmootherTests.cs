using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Pathfinding;

/// <summary>Path smoothing of docs/implementation/M2_unit_control.md, 4.5.</summary>
public sealed class PathSmootherTests
{
    private static readonly Locomotor[] AllLocomotors = [Locomotor.Foot, Locomotor.Track, Locomotor.Wheel];

    private static MapGrid Load(string name) => TestMapParserTests.Load(name).Grid;

    private static CellPath SmoothPath(MapGrid map, int sx, int sy, int gx, int gy, Locomotor locomotor) =>
        PathSmoother.Smooth(map, AStar.FindPath(map, new CellPos(sx, sy), new CellPos(gx, gy), locomotor), locomotor);

    /// <summary>
    /// Reference for <see cref="GridLine"/>, tests only: cells whose closed square meets the segment between the two centres,
    /// found geometrically with doubles (Liang–Barsky clipping).
    /// </summary>
    private static HashSet<CellPos> TouchedCells(CellPos from, CellPos to)
    {
        double x0 = from.X + 0.5, y0 = from.Y + 0.5, x1 = to.X + 0.5, y1 = to.Y + 0.5;
        var touched = new HashSet<CellPos>();
        for (var x = System.Math.Min(from.X, to.X) - 1; x <= System.Math.Max(from.X, to.X) + 1; x++)
        {
            for (var y = System.Math.Min(from.Y, to.Y) - 1; y <= System.Math.Max(from.Y, to.Y) + 1; y++)
            {
                if (SegmentMeetsBox(x0, y0, x1, y1, x, y, x + 1, y + 1))
                {
                    touched.Add(new CellPos(x, y));
                }
            }
        }

        return touched;
    }

    private static bool SegmentMeetsBox(double x0, double y0, double x1, double y1, double left, double top, double right, double bottom)
    {
        double tMin = 0, tMax = 1;
        double[] p = [-(x1 - x0), x1 - x0, -(y1 - y0), y1 - y0];
        double[] q = [x0 - left, right - x0, y0 - top, bottom - y0];
        for (var i = 0; i < 4; i++)
        {
            if (p[i] == 0)
            {
                if (q[i] < 0)
                {
                    return false;
                }

                continue;
            }

            var t = q[i] / p[i];
            if (p[i] < 0)
            {
                tMin = System.Math.Max(tMin, t);
            }
            else
            {
                tMax = System.Math.Min(tMax, t);
            }
        }

        return tMin <= tMax + 1e-12;
    }

    /// <summary>Every segment of the smoothed path is drivable and never slower than the slowest cell of the part it replaces.</summary>
    private static void AssertSmoothedPathValid(MapGrid map, CellPath path, Locomotor locomotor)
    {
        var cells = path.Cells;
        var waypoints = path.Waypoints;
        Assert.Equal(cells[0], waypoints[0]);
        Assert.Equal(cells[^1], waypoints[^1]);
        Assert.True(waypoints.Length <= cells.Length);

        var position = 0;
        for (var w = 1; w < waypoints.Length; w++)
        {
            var from = position;
            position = cells.IndexOf(waypoints[w], from + 1);
            Assert.True(position > from, "Waypoints must be path cells in order.");

            var slowest = Enumerable.Range(from + 1, position - from).Min(i => map.SpeedPercent(cells[i], locomotor));
            foreach (var cell in TouchedCells(waypoints[w - 1], waypoints[w]).Where(c => c != waypoints[w - 1]))
            {
                var percent = map.SpeedPercent(cell, locomotor);
                Assert.True(percent > 0, $"Segment {waypoints[w - 1]} → {waypoints[w]} touches impassable {cell}.");
                if (position - from > 1)
                {
                    Assert.True(percent >= slowest, $"Segment {waypoints[w - 1]} → {waypoints[w]} crosses slower {cell}.");
                }
            }
        }
    }

    /// <summary>
    /// Cells the unit's centre actually drives through along the smoothed path: points sampled inside each segment.
    /// Unlike <see cref="TouchedCells"/>, a cell only touched at a corner by a diagonal step does not count.
    /// </summary>
    private static IEnumerable<CellPos> DrivenCells(CellPath path)
    {
        const int Samples = 97;
        for (var w = 1; w < path.Waypoints.Length; w++)
        {
            var a = path.Waypoints[w - 1];
            var b = path.Waypoints[w];
            for (var k = 0; k <= Samples; k++)
            {
                var t = (double)k / Samples;
                var x = a.X + 0.5 + (b.X - a.X) * t;
                var y = a.Y + 0.5 + (b.Y - a.Y) * t;
                yield return new CellPos((int)System.Math.Floor(x), (int)System.Math.Floor(y));
            }
        }
    }

    [Fact]
    public void Line_cells_of_simple_segments()
    {
        Assert.Equal([new CellPos(2, 3)], GridLine.Cells(new CellPos(2, 3), new CellPos(2, 3)));
        Assert.Equal([new CellPos(0, 0), new CellPos(1, 0), new CellPos(2, 0), new CellPos(3, 0)], GridLine.Cells(new CellPos(0, 0), new CellPos(3, 0)));
        Assert.Equal([new CellPos(1, 3), new CellPos(1, 2), new CellPos(1, 1)], GridLine.Cells(new CellPos(1, 3), new CellPos(1, 1)));
    }

    [Fact]
    public void Diagonal_through_corners_touches_both_side_cells()
    {
        Assert.Equal(
            [new CellPos(0, 0), new CellPos(1, 0), new CellPos(0, 1), new CellPos(1, 1), new CellPos(2, 1), new CellPos(1, 2), new CellPos(2, 2)],
            GridLine.Cells(new CellPos(0, 0), new CellPos(2, 2)));
    }

    [Fact]
    public void Gentle_slope_steps_mostly_along_x()
    {
        Assert.Equal(
            [new CellPos(0, 0), new CellPos(1, 0), new CellPos(2, 0), new CellPos(2, 1), new CellPos(3, 1), new CellPos(4, 1)],
            GridLine.Cells(new CellPos(0, 0), new CellPos(4, 1)));
    }

    [Fact]
    public void Line_cells_match_the_geometric_reference()
    {
        var random = new TestRandom(9);
        for (var i = 0; i < 10_000; i++)
        {
            var from = new CellPos(random.NextInt(21), random.NextInt(21));
            var to = new CellPos(random.NextInt(21), random.NextInt(21));

            var cells = GridLine.Cells(from, to);

            Assert.Equal(cells.Count, cells.Distinct().Count());
            Assert.True(TouchedCells(from, to).SetEquals(cells), $"Segment {from} → {to}.");
        }
    }

    [Fact]
    public void Open_field_becomes_one_straight_line()
    {
        var path = SmoothPath(Load("paths/open_field.txt"), 0, 0, 9, 5, Locomotor.Track);

        Assert.Equal([new CellPos(0, 0), new CellPos(9, 5)], path.Waypoints.ToArray());
        Assert.Equal(10, path.Cells.Length);
    }

    [Theory]
    [InlineData("paths/corner_trees.txt", 1, 1, 2, 2)]
    [InlineData("paths/wall_gap.txt", 0, 0, 9, 0)]
    [InlineData("paths/u_trap.txt", 5, 4, 11, 4)]
    [InlineData("paths/narrow_pass.txt", 0, 0, 12, 6)]
    [InlineData("paths/maze.txt", 0, 0, 14, 14)]
    [InlineData("paths/road_detour.txt", 0, 2, 12, 2)]
    [InlineData("paths/rough_band.txt", 0, 0, 9, 0)]
    [InlineData("all_land_types.txt", 0, 0, 23, 11)]
    public void Smoothed_reference_paths_cut_no_corners(string name, int sx, int sy, int gx, int gy)
    {
        var map = Load(name);
        foreach (var locomotor in AllLocomotors)
        {
            var path = SmoothPath(map, sx, sy, gx, gy, locomotor);

            AssertSmoothedPathValid(map, path, locomotor);
        }
    }

    [Fact]
    public void Wall_gap_path_bends_at_the_gap()
    {
        var path = SmoothPath(Load("paths/wall_gap.txt"), 0, 0, 9, 0, Locomotor.Foot);

        Assert.True(path.Waypoints.Length < path.Cells.Length);
        Assert.Contains(path.Waypoints, cell => cell.Y >= 7);
    }

    [Fact]
    public void Wheels_stay_on_the_road()
    {
        var map = Load("paths/road_detour.txt");

        var path = SmoothPath(map, 0, 2, 12, 2, Locomotor.Wheel);

        // Apart from the clear start and goal cells, the jeep drives on road only: smoothing does not pull it off.
        var driven = DrivenCells(path).Where(cell => cell != new CellPos(0, 2) && cell != new CellPos(12, 2)).ToList();
        Assert.NotEmpty(driven);
        Assert.All(driven, cell => Assert.Equal(LandType.Road, map.LandAt(cell)));
    }

    [Fact]
    public void Wheels_still_go_around_rough_land()
    {
        var map = Load("paths/rough_band.txt");

        var wheels = SmoothPath(map, 0, 0, 9, 0, Locomotor.Wheel);
        var foot = SmoothPath(map, 0, 0, 9, 0, Locomotor.Foot);

        Assert.DoesNotContain(DrivenCells(wheels), cell => map.LandAt(cell) == LandType.Rough);
        Assert.Equal([new CellPos(0, 0), new CellPos(9, 0)], foot.Waypoints.ToArray());
    }

    [Fact]
    public void Maze_keeps_waypoints_only_at_turns()
    {
        var path = SmoothPath(Load("paths/maze.txt"), 0, 0, 14, 14, Locomotor.Foot);

        var turns = 0;
        for (var i = 1; i < path.Cells.Length - 1; i++)
        {
            var a = (path.Cells[i].X - path.Cells[i - 1].X, path.Cells[i].Y - path.Cells[i - 1].Y);
            var b = (path.Cells[i + 1].X - path.Cells[i].X, path.Cells[i + 1].Y - path.Cells[i].Y);
            turns += a == b ? 0 : 1;
        }

        Assert.InRange(path.Waypoints.Length, 2, turns + 2);
    }

    [Fact]
    public void Random_maps_give_valid_smoothed_paths()
    {
        var random = new TestRandom(13);
        var lands = Enum.GetValues<LandType>();
        for (var round = 0; round < 200; round++)
        {
            var map = new MapGrid(32, 32);
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var cell = new CellPos(x, y);
                    map.SetLand(cell, random.NextInt(100) < 55 ? LandType.Clear : lands[random.NextInt(lands.Length)]);
                    map.SetBlocked(cell, random.NextInt(100) < 6);
                }
            }

            var locomotor = AllLocomotors[round % 3];
            var path = SmoothPath(map, random.NextInt(32), random.NextInt(32), random.NextInt(32), random.NextInt(32), locomotor);

            AssertSmoothedPathValid(map, path, locomotor);
        }
    }

    [Fact]
    public void Unit_drives_the_straight_line_across_open_field()
    {
        var world = new GameWorld(seed: 1, Load("paths/open_field.txt"));
        var player = new PlayerId(1);
        var conscript = world.SpawnUnit(player, UnitType.Conscript, new CellPos(0, 0).Center);
        var target = new CellPos(9, 5).Center;
        CommandProcessor.Apply(world, new MoveCommand(player, 0, 0, [conscript.Id], target));

        var events = new List<SimEvent>();
        var ticks = 0;
        while (conscript.MoveTarget is not null)
        {
            ticks++;
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.CurrentTick++;
        }

        // Straight line: 256·√106 ≈ 2636 leptons at 24 per tick; the staircase (5 diagonals + 4 straights) would be ≈ 2834.
        Assert.Equal(target, conscript.Position);
        Assert.InRange(ticks, 110, 111);
    }
}
