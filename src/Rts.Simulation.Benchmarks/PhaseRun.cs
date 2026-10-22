using System.Diagnostics;
using Rts.Data;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Hashing;
using Rts.Simulation.Systems;

namespace Rts.Simulation.Benchmarks;

/// <summary>
/// <c>--phases</c>: the load scenario tick by tick with each phase of <see cref="GameSimulation.Step"/> timed on its own
/// (commands, path searches, movement, index, hash), to find where a peak comes from. The order is the one of Step.
/// </summary>
public static class PhaseRun
{
    public static void Run(string repoRoot)
    {
        var map = LoadScenario.LoadMap(repoRoot);
        var rules = GameData.LoadDirectory(Path.Combine(repoRoot, "game", GameData.Folder)).Rules;
        for (var round = 0; round <= LoadRun.WarmUpRounds; round++)
        {
            var simulation = LoadScenario.Start(map, rules);
            var world = simulation.World;
            var events = new List<SimEvent>();
            var commands = new List<Command>();
            // The orders are applied by hand, outside the command queue, so they can be timed.
            commands.AddRange(LoadScenario.Orders(simulation));
            var names = new[] { "commands", "paths", "movement", "index", "hash" };
            var total = new double[names.Length];
            var peak = new double[names.Length];
            var peakTick = new int[names.Length];
            for (var tick = 0; tick < (round == LoadRun.WarmUpRounds ? LoadRun.MovingTicks : 20); tick++)
            {
                var times = new double[names.Length];
                var start = Stopwatch.GetTimestamp();
                if (tick == 0)
                {
                    foreach (var command in commands)
                    {
                        CommandProcessor.Apply(world, command, events);
                    }
                }

                times[0] = Lap(ref start);
                world.PathRequests.Process(world, events);
                times[1] = Lap(ref start);
                MovementSystem.Update(world, events);
                times[2] = Lap(ref start);
                world.Spatial.Sync(world);
                world.RemoveDead();
                world.CurrentTick++;
                times[3] = Lap(ref start);
                StateHasher.Compute(world);
                times[4] = Lap(ref start);
                events.Clear();
                for (var i = 0; i < names.Length; i++)
                {
                    total[i] += times[i];
                    if (times[i] > peak[i])
                    {
                        peak[i] = times[i];
                        peakTick[i] = tick;
                    }
                }
            }

            if (round == LoadRun.WarmUpRounds)
            {
                for (var i = 0; i < names.Length; i++)
                {
                    Console.WriteLine($"{names[i],-9} mean {total[i] / LoadRun.MovingTicks:0.000} ms · peak {peak[i]:0.000} ms on tick {peakTick[i]}");
                }
            }
        }
    }

    private static double Lap(ref long start)
    {
        var now = Stopwatch.GetTimestamp();
        var ms = Stopwatch.GetElapsedTime(start, now).TotalMilliseconds;
        start = now;
        return ms;
    }
}
