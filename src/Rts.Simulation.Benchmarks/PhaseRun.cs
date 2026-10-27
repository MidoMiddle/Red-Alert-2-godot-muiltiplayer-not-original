using System.Diagnostics;
using Rts.Data;

namespace Rts.Simulation.Benchmarks;

/// <summary>
/// <c>--phases</c>: a run tick by tick with each phase of <see cref="GameSimulation.Step"/> timed on its own, through its probe (M4 step
/// 15): the vision of what appeared, commands, path searches, the economy, targets, pursuit, weapons, projectiles, damage, movement,
/// the index, ore and technologies, death and vision, the removal of the dead, the hash — to find where a peak comes from and what
/// allocates. First the load scenario of M2 (movement only), then the general battle of M4.
/// </summary>
public static class PhaseRun
{
    public static void Run(string repoRoot)
    {
        var rules = GameData.LoadDirectory(Path.Combine(repoRoot, "game", GameData.Folder)).Rules;
        var map = LoadScenario.LoadMap(repoRoot, rules);
        GameSimulation Load()
        {
            var simulation = LoadScenario.Start(map, rules);
            LoadScenario.OrderAcrossTheMap(simulation);
            return simulation;
        }

        GameSimulation Battle()
        {
            var simulation = BattleScenario.Start(map.Grid.Clone(), rules, hashInterval: 15, BattleBases.Build);
            BattleScenario.Charge(simulation);
            return simulation;
        }

        for (var round = 0; round < BattleRun.WarmUpRounds; round++)
        {
            Measure(Load, 300, print: null);
            Measure(Battle, 600, print: null);
        }

        Measure(Load, LoadRun.MovingTicks, "load scenario of M2, the four groups across the map");
        Measure(Battle, BattleScenario.Ticks, "general battle of M4, 150 against 150");
    }

    private static void Measure(Func<GameSimulation> start, int ticks, string? print)
    {
        var simulation = start();
        var names = new List<string>();
        var total = new Dictionary<string, double>();
        var peak = new Dictionary<string, (double Ms, int Tick)>();
        var bytes = new Dictionary<string, long>();
        var lap = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        simulation.Probe = name =>
        {
            var now = Stopwatch.GetTimestamp();
            var ms = Stopwatch.GetElapsedTime(lap, now).TotalMilliseconds;
            var heap = GC.GetAllocatedBytesForCurrentThread();
            if (!total.ContainsKey(name))
            {
                names.Add(name);
                total[name] = 0;
                peak[name] = (0, 0);
                bytes[name] = 0;
            }

            total[name] += ms;
            bytes[name] += heap - allocated;
            if (ms > peak[name].Ms)
            {
                peak[name] = (ms, simulation.CurrentTick);
            }

            lap = Stopwatch.GetTimestamp();
            allocated = GC.GetAllocatedBytesForCurrentThread();
        };

        for (var tick = 0; tick < ticks; tick++)
        {
            lap = Stopwatch.GetTimestamp();
            allocated = GC.GetAllocatedBytesForCurrentThread();
            simulation.Step();
        }

        if (print is null)
        {
            return;
        }

        Console.WriteLine($"{print}, {ticks} ticks (per tick: mean, peak at tick; bytes allocated)");
        foreach (var name in names)
        {
            Console.WriteLine($"  {name,-18} mean {total[name] / ticks,7:0.000} ms  peak {peak[name].Ms,7:0.000} ms at {peak[name].Tick,5}  {bytes[name] / ticks,8:0} B");
        }

        Console.WriteLine($"  {"all",-18} mean {total.Values.Sum() / ticks,7:0.000} ms  {bytes.Values.Sum() / ticks,8:0} B");
    }
}
