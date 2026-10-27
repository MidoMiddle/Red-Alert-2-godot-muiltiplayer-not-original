using System.Diagnostics;
using Rts.Data;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Benchmarks;

/// <summary>
/// <c>--load</c>: plays the load scenario with a stopwatch around every tick and prints the mean, the 99th percentile and the
/// peak, the order tick on its own, A* nodes and memory allocated per tick (docs/implementation/M2_unit_control.md, 4.15).
/// BenchmarkDotNet gives averages; the peak needs this.
/// </summary>
public static class LoadRun
{
    public const int IdleTicks = 300;
    public const int MovingTicks = 1500;
    public const int WarmUpRounds = 40;

    public static void Run(string repoRoot)
    {
        var rules = GameData.LoadDirectory(Path.Combine(repoRoot, "game", GameData.Folder)).Rules;
        var map = LoadScenario.LoadMap(repoRoot, rules);
        // Warm the JIT first: a method is optimised after about 30 calls, and an order runs the group code once per group,
        // so a cold run would time unoptimised code on the order tick. A match runs many orders; this is what it sees.
        for (var round = 0; round < WarmUpRounds; round++)
        {
            Measure(map, rules, ordered: true, 20, print: false);
        }

        Measure(map, rules, ordered: true, 300, print: false);

        Measure(map, rules, ordered: false, IdleTicks, print: true);
        Measure(map, rules, ordered: true, MovingTicks, print: true);
        MeasureMaze(map);
    }

    private static void Measure(TestMap map, Rts.Simulation.Rules.GameRules rules, bool ordered, int ticks, bool print)
    {
        var simulation = LoadScenario.Start(map, rules);
        if (ordered)
        {
            LoadScenario.OrderAcrossTheMap(simulation);
        }

        var times = new double[ticks];
        long nodes = 0;
        var peakNodes = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < ticks; i++)
        {
            var start = Stopwatch.GetTimestamp();
            simulation.Step();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var expanded = simulation.World.PathRequests.ExpandedLastTick;
            nodes += expanded;
            peakNodes = System.Math.Max(peakNodes, expanded);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        if (!print)
        {
            return;
        }

        var sorted = times.OrderBy(t => t).ToArray();
        var arrived = simulation.World.Entities.OfType<Unit>().Count(u => u.MoveTarget is null && u.Path is null && !u.PathPending);
        Console.WriteLine(ordered ? $"Across the map, {ticks} ticks:" : $"Idle, {ticks} ticks:");
        Console.WriteLine($"  tick mean {times.Average():0.000} ms · p99 {sorted[(int)(ticks * 0.99)]:0.000} ms · peak {sorted[^1]:0.000} ms");
        if (ordered)
        {
            Console.WriteLine($"  order tick {times[0]:0.000} ms · rest peak {times.Skip(1).Max():0.000} ms");
            Console.WriteLine($"  A* nodes {nodes} total · peak {peakNodes}/tick · units standing at the end {arrived}/300");
        }

        Console.WriteLine($"  allocated {allocated / ticks / 1024.0:0.0} KB per tick");
    }

    private static void MeasureMaze(TestMap map)
    {
        var pathfinder = new Pathfinder(map.Grid);
        CellPath path = pathfinder.FindPath(LoadScenario.MazeStart, LoadScenario.MazeGoal, Locomotor.Foot);
        var runs = 50;
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < runs; i++)
        {
            path = pathfinder.FindPath(LoadScenario.MazeStart, LoadScenario.MazeGoal, Locomotor.Foot);
        }

        var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds / runs;
        Console.WriteLine($"A* into the maze: {ms:0.000} ms · {path.Status} · {path.Cells.Length} cells · {path.Expanded} nodes · {ms * 1e6 / path.Expanded:0} ns per node");
    }
}
